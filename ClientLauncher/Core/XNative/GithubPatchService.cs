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
#if !NEXUS_CANDIDATE
    /// <summary>
    /// This is the automatic downloader and patching service that allows us to keep NV:MP up to date remotely.
    /// For Nexus submissions this is completely stripped as to follow Nexus submission terms of service.
    /// </summary>
    public class GithubPatchService
    {
        private MainWindow           ParentWindow;
        private Windows.PatchDisplay ModalWindow;
        private ManualResetEvent mre = new ManualResetEvent(false);

        private string Root;

        public GithubPatchService(MainWindow parent, string root)
        {
#if !DEBUG
            if (Environment.GetEnvironmentVariable(XNativeConfig.Patching_ForkingVariable) == null)
            {
                if (ForkClone())
                {
                    return;
                }
            }
#endif

            ParentWindow = parent;
            Root = root;
        }

        public bool ForkClone()
        {
            if (Debugger.IsAttached)
                return false;

            Trace.WriteLine("Forking process..");

            // Clean up temp files
            try
            {
                File.Delete(Path.GetTempPath() + "nvmp_launcher.exe");
            }
            catch { }
            try
            {
                File.Delete(Directory.GetCurrentDirectory() + "\\nvmp_launcher.temp.exe");
            }
            catch { }

            string FileNameTemp;
            try
            {

                // Copy current process into temporary location.
                FileNameTemp = Path.GetTempPath() + "nvmp_launcher.exe";
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
                    fork.StartInfo.WorkingDirectory = Directory.GetCurrentDirectory();
                    fork.StartInfo.EnvironmentVariables.Add(XNativeConfig.Patching_ForkingVariable, CurrentProcessPath);
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
            string process = Environment.GetEnvironmentVariable(XNativeConfig.Patching_ForkingVariable);

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
                fork.StartInfo.EnvironmentVariables.Remove(XNativeConfig.Patching_ForkingVariable);
                fork.Start();
            }

            Process.GetCurrentProcess().Kill();
        }

        /// <summary>
        /// Updates the version on file with the current version parsed. The reason this is done here and not inside ProgramVersioning
        /// is because we don't want program versioning having any responsibility for updating what patch the game client is at.
        /// </summary>
        public void UpdateBinaryVersion(string version)
        {
            if (Root == null)
            {
                throw new Exception("Root cannot be null");
            }

            File.WriteAllText(ParentWindow.ProgramVersion.BuildFilenameFull, version);
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

        internal void PostPatchScripts()
        {
            // Ensure there is a start-menu entry
            string ClientPath = Root + "\\nvmp_launcher.exe";
            string CommonStartMenuPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);
            string ApplicationStartMenuPath = Path.Combine(CommonStartMenuPath, "Programs", "NVMP");

            if (!Directory.Exists(ApplicationStartMenuPath))
                Directory.CreateDirectory(ApplicationStartMenuPath);

            string ShortcutLocation = Path.Combine(ApplicationStartMenuPath, "New Vegas Multiplayer.lnk");

            if (!File.Exists(ShortcutLocation))
            {
                IWshRuntimeLibrary.WshShell Shell = new IWshRuntimeLibrary.WshShell();
                IWshRuntimeLibrary.IWshShortcut Shortcut = (IWshRuntimeLibrary.IWshShortcut)Shell.CreateShortcut(ShortcutLocation);
                Shortcut.Description = "NV:MP Game Client";
                Shortcut.TargetPath = ClientPath;
                Shortcut.Save();
            }
        }

        public void Patch(bool ForceOver = false)
        {
            OpenModal();

            // Wait for the modal to establish before piping result.
            mre.WaitOne();

            // See if we need to force a download, or that if there's no current active version on file
            bool needsDownload = ForceOver || ParentWindow.ProgramVersion.CurrentVersion == null || ParentWindow.ProgramVersion.IsOutOfDate;

            // Get the releases information from GitHub
            using (var wc = new WebClient())
            {
                if (needsDownload)
                {
                    if (ParentWindow.ProgramVersion.LatestRelease == null)
                    {
                        CloseModalSafe();
                        MessageBox.Show("Patching Services are currently unavailable");
                        return;
                    }

                    Directory.CreateDirectory($"{Root}\\.nvmp_patch");

                    try
                    {
                        foreach (var file in ParentWindow.ProgramVersion.LatestRelease.assets)
                        {
                            SetModalStatus(Windows.PatchDisplay.EPatchStatus.kPatchStatus_Downloading, file.name);
                            wc.DownloadFile(file.browser_download_url, $"{Root}\\.nvmp_patch\\${file.name}");
                        }

                        foreach (var file in ParentWindow.ProgramVersion.LatestRelease.assets)
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
                    UpdateBinaryVersion(ParentWindow.ProgramVersion.LatestRelease.tag_name);

                    try
                    {
                        PostPatchScripts();
                    }
                    catch { } 

                    SetModalStatus(Windows.PatchDisplay.EPatchStatus.kPatchStatus_Restarting);
                    Thread.Sleep(2000);
                    Restart();
                    return;
                }
            }

            CloseModalSafe();
        }
    }
#endif
}