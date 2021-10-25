using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Web.Script.Serialization;
using System.Windows;
using System.Threading;
using System.IO.Compression;

namespace ClientLauncher.Core.XNative
{
    public class GithubPatchService
    {
        private MainWindow           ParentWindow;
        private Windows.PatchDisplay ModalWindow;
        private ManualResetEvent mre = new ManualResetEvent(false);

        private string     Root;
        private string     VersionFileName;

        private string CurrentActiveVersion;

        static private string GithubAPI_ReleasesLatest = "https://api.github.com/repos/NVMP/client-release/releases/latest";
        static private string ForkVariable = "WontYouForkOff";

        public class GitHubRelease
        {
            public GitHubRelease()
            {
            }

            public class Asset
            {
                public Asset()
                {
                }

                public string name;
                public string browser_download_url;
            }

            public string tag_name;
            public IEnumerable<Asset> assets;
        }

        public GithubPatchService(MainWindow parent, string root)
        {
#if !DEBUG
            if (Environment.GetEnvironmentVariable(ForkVariable) == null)
            {
                if (ForkClone())
                {
                    return;
                }
            }
#endif

            ParentWindow = parent;
            VersionFileName = ".nvmp_version";
            Root = root;
            CurrentActiveVersion = ReadBinaryVersion();

            ParentWindow.SetPatchVersion(CurrentActiveVersion);
        }

        public bool ForkClone()
        {
            if (Debugger.IsAttached)
                return false;

            Trace.WriteLine("Forking process..");

            string FileNameTemp;
            try
            {

                // Copy current process into temporary location.
                FileNameTemp = Path.GetTempPath() + "NVMP.exe";
                Trace.WriteLine("Temp file is " + FileNameTemp);
            } catch (Exception e)
            {
                MessageBox.Show("Could not pre-patch executable, the temporary path query failed!\n\nDetails: \n" + e.Message, "New Vegas: Multiplayer");
                return false;
            }

            string CurrentProcessPath = System.Reflection.Assembly.GetEntryAssembly().Location;
            Trace.WriteLine("Current file is " + CurrentProcessPath);
            try
            {

                File.Copy(CurrentProcessPath, FileNameTemp, true);
            }
            catch (Exception e)
            {
                // Try to fall back to a local exe
                FileNameTemp = Directory.GetCurrentDirectory() + "\\nvmp_launcher.temp.exe";
                try
                {
                    File.Copy(CurrentProcessPath, FileNameTemp, true);
                } catch (Exception _e)
                {
                    MessageBox.Show($"Could not pre-patch executable, copying process to {FileNameTemp} from {CurrentProcessPath} failed (temp path also failed)!\n\nDetails: \n" + e.Message + "\n\n" + _e.Message,
                        "New Vegas: Multiplayer");
                }
            }

            try
            {
                // Start the fork.
                using (Process fork = new Process())
                {
                    fork.StartInfo.FileName = FileNameTemp;
                    fork.StartInfo.UseShellExecute = false;
                    fork.StartInfo.CreateNoWindow = true;
                    fork.StartInfo.EnvironmentVariables.Add(ForkVariable, CurrentProcessPath);
                    fork.Start();
                }
            }
            catch (Exception e)
            {
                MessageBox.Show($"Could not pre-patch executable, starting new process {FileNameTemp} failed!\n\nDetails: \n" + e.Message,
                    "New Vegas: Multiplayer");
                return false;
            }

            try
            {
                Process.GetCurrentProcess().Kill();
            }
            catch (Exception e)
            {
                MessageBox.Show($"Could not pre-patch executable, killing current process failed!\n\nDetails: \n" + e.Message,
                    "New Vegas: Multiplayer");
                return false;
            }

            return true;
        }

        //-----------------------------------------
        // Restarts the launcher via its original
        // patcher process.
        //-----------------------------------------
        public void Restart()
        {
            string process;
            process = Environment.GetEnvironmentVariable(ForkVariable);

            if (process == null)
            {
                process = System.Reflection.Assembly.GetEntryAssembly().Location;
                Trace.WriteLine("Falling back to assembly location, fork variable not set.");
            }

            if (!File.Exists(process))
            {
                MessageBox.Show("Failed to launch original patcher, please make sure file " + process + " exists on your system");
                Process.GetCurrentProcess().Kill();
                return;
            }

            using (Process fork = new Process())
            {
                fork.StartInfo.FileName = process;
                fork.StartInfo.UseShellExecute = false;
                fork.StartInfo.CreateNoWindow = true;
                fork.StartInfo.EnvironmentVariables.Remove(ForkVariable);
                fork.Start();
            }

            Process.GetCurrentProcess().Kill();
        }


        public string ReadBinaryVersion()
        {
            if (Root == null)
            {
                throw new Exception("Root cannot be null");
            }

            if (File.Exists($"{Root}\\{VersionFileName}"))
            {
                return File.ReadAllText($"{Root}\\{VersionFileName}");
            }
            return null;
        }

        public void UpdateBinaryVersion()
        {
            if (Root == null)
            {
                throw new Exception("Root cannot be null");
            }
            File.WriteAllText($"{Root}\\{VersionFileName}", CurrentActiveVersion);
        }

        private void ModalThread()
        {
            ModalWindow = new Windows.PatchDisplay();
            ModalWindow.Show();

            mre.Set();
            System.Windows.Threading.Dispatcher.Run();
        }

        private void OpenModal()
        {
            Thread NewWindowThread = new Thread(new ThreadStart(ModalThread));
            NewWindowThread.SetApartmentState(ApartmentState.STA);
            NewWindowThread.IsBackground = true;
            NewWindowThread.Start();
        }

        private void CloseModalSafe()
        {
            if (ModalWindow.Dispatcher.CheckAccess())
                ModalWindow.Close();
            else
                ModalWindow.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new ThreadStart(ModalWindow.Close));
        }

        private void SetModalStatus(Windows.PatchDisplay.EPatchStatus status, string message = "", int value = 0)
        {
            if (ModalWindow.Dispatcher.CheckAccess())
                ModalWindow.SetStatus(status, message, value);
            else
            {
                ModalWindow.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal,
                    new ThreadStart(() => ModalWindow.SetStatus(status, message, value)));
            }
        }

        public void Patch(bool ForceOver = false)
        {
            OpenModal();

            // Wait for the modal to establish before piping result.
            mre.WaitOne();

            // See if we need to force a download, or that if there's no current active version on file
            bool needsDownload = ForceOver || CurrentActiveVersion == null;

            // Get the releases information from GitHub
            using (var wc = new WebClient())
            {
                ServicePointManager.Expect100Continue = true;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                wc.Headers.Add("User-Agent", "NVMP/X");

                GitHubRelease releases = null;
                try
                {
                    SetModalStatus(Windows.PatchDisplay.EPatchStatus.kPatchStatus_DownloadingManifest, "", 1);

                    var serialiser = new JavaScriptSerializer();

                    var json = wc.DownloadString(GithubAPI_ReleasesLatest);
                    releases = serialiser.Deserialize<GitHubRelease>(json);

                    if (releases.tag_name != CurrentActiveVersion)
                    {
                        // New version
                        needsDownload = true;
                    }
                }
                catch (Exception e)
                {
                    CloseModalSafe();
                    MessageBox.Show("Failed to check remote manifest to patch, please check your internet connection\n" + e.Message);
                    return;
                }

                if (needsDownload)
                {
                    Directory.CreateDirectory($"{Root}\\.nvmp_patch");

                    try
                    {
                        foreach (var file in releases.assets)
                        {
                            SetModalStatus(Windows.PatchDisplay.EPatchStatus.kPatchStatus_Downloading, file.name);
                            wc.DownloadFile(file.browser_download_url, $"{Root}\\.nvmp_patch\\${file.name}");
                        }

                        foreach (var file in releases.assets)
                        {
                            SetModalStatus(Windows.PatchDisplay.EPatchStatus.kPatchStatus_Applying, file.name);

                            ZipArchive zipFile = ZipFile.OpenRead($"{Root}\\.nvmp_patch\\${file.name}");
                            if (zipFile != null)
                            {
                                zipFile.ExtractToDirectory(Root, true);
                            }
                            zipFile.Dispose();
                        }
                    }
                     catch (Exception e)
                    {
                        CloseModalSafe();
                        MessageBox.Show("Patch failure\n" + e.Message);
                        return;
                    }

                    Directory.Delete($"{Root}\\.nvmp_patch", true);
                    CurrentActiveVersion = releases.tag_name;
                    UpdateBinaryVersion();

                    SetModalStatus(Windows.PatchDisplay.EPatchStatus.kPatchStatus_Restarting);
                    Thread.Sleep(2000);
                    Restart();
                    return;
                }
            }

            CloseModalSafe();
        }
    }
}
