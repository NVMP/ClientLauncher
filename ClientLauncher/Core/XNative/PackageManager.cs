using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ClientLauncher.Core.XNative
{
    public class PackageManager
    {
        public static int Revision = 1;

        public enum PackageType
        {
            /// <summary>
            /// Describes a 7ZIP file that requires the 7Za.exe to run
            /// </summary>
            Archive7Z,

            /// <summary>
            /// Describes a flat file that can be just copied to the target destination.
            /// </summary>
            FlatFile
        }

        /// <summary>
        /// Package definition that is supported by the launcher.
        /// </summary>
        public class Package
        {
            /// <summary>
            /// External URL to download on.
            /// </summary>
            public string URL { get; set; }

            /// <summary>
            /// Root relative to the game directory to extract into.
            /// </summary>
            public string DestinationRoot { get; set; }

            /// <summary>
            /// Sets the base name of the package, this allows other packages to override the same files.
            /// </summary>
            public string BaseName { get; set; }

            /// <summary>
            /// Type of package, which effects how the file is interpreted.
            /// </summary>
            public PackageType Type { get; set; }
        }

        /// <summary>
        /// These are the package resolvers that can be modified to add dynamic server packages for players to obtain on launch.
        /// </summary>
        private static Dictionary<string, Package> PackageResolvers = new Dictionary<string, Package>()
        {
            // NVSE
            { "nvse_634", new Package {
                URL = "https://github.com/xNVSE/NVSE/releases/download/6.3.4/nvse_6_3_4.7z",
                Type = PackageType.Archive7Z,
                BaseName = "nvse",
                DestinationRoot = "", // Extract to the root
            } }
        };

        static private WebClient PackageWebClient = new WebClient();

        /// <summary>
        /// Uses 7ZIP to extract a file to disk.
        /// </summary>
        /// <param name="filepath"></param>
        /// <param name="destination"></param>
        /// <returns></returns>
        static protected bool Extract7ZIP(string filepath, string destination)
        {
            try
            {
                var startupInfo = new ProcessStartInfo();
                startupInfo.FileName = "7za.exe";
                startupInfo.CreateNoWindow = true;
                startupInfo.UseShellExecute = false;
                startupInfo.WindowStyle = ProcessWindowStyle.Hidden;
                startupInfo.Arguments = $"x \"{filepath}\" -y -o\"{destination}\"";

                var process = Process.Start(startupInfo);
                process.WaitForExit();
                return true;
            } catch (Exception ex)
            {
                Trace.WriteLine(ex.ToString());
            }

            return false;
        }

        /// <summary>
        /// Performs a lazy check to see if the package is installed. No checksums are checked, so this is just mainly to
        /// avoid redownloading.
        /// </summary>
        /// <param name="packageName"></param>
        /// <param name="gameRoot"></param>
        /// <returns></returns>
        static public bool IsPackageInstalled(string packageName, string gameRoot)
        {
            if (PackageResolvers.TryGetValue(packageName, out var resolver))
            {
                string packageManifest = Path.Combine(gameRoot, resolver.DestinationRoot, $"nvmp.{resolver.BaseName}.package");
                if (File.Exists(packageManifest))
                {
                    // if the base name does not match the manifest url, then it means we need to override this package, meaning redownload.
                    return File.ReadAllText(packageManifest) == $"{resolver.URL}@{Revision}";
                }
            }

            return false;
        }

        static private void SetPackageInstalled(Package resolver, string gameRoot)
        {
            string file = Path.Combine(gameRoot, resolver.DestinationRoot, $"nvmp.{resolver.BaseName}.package");
            try
            {
                File.WriteAllText(file, $"{resolver.URL}@{Revision}");
            }
            catch
            {
            }
        }

        static protected bool ExtractFlatFile(string filepath, string destination)
        {
            try
            {
                File.Copy(filepath, destination);
                return true;
            } catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }

            return false;
        }

        static public bool DownloadAndExtract(string packageName, string gameRoot)
        {
            if (PackageResolvers.TryGetValue(packageName, out var resolver))
            {
                return DownloadAndExtract(resolver, gameRoot);
            }

            return false;
        }

        static protected bool DownloadAndExtract(Package package, string gameRoot)
        {
            // Download the repository
            string tempFileName = Path.GetTempFileName();
            try
            {
                PackageWebClient.DownloadFile(package.URL, tempFileName);
            } catch (Exception ex)
            {
                Trace.WriteLine(ex);
                return false;
            }

            string destination = Path.Combine(gameRoot, package.DestinationRoot);

            // Extract
            bool result = false;
            switch (package.Type)
            {
                case PackageType.Archive7Z:
                    result = Extract7ZIP(tempFileName, destination);
                    break;
                case PackageType.FlatFile:
                    result = ExtractFlatFile(tempFileName, destination);
                    break;
            }

            if (result)
            {
                SetPackageInstalled(package, gameRoot);
            }

            return result;
        }
    }
}
