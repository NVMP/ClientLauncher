using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using System.Text.RegularExpressions;
using System.IO;
using ClientLauncher.Core;

namespace ClientLauncher
{
    //-------------------------------------------------
    // Finds the machine's Fallout: New Vegas storage
    // location. 
    //-------------------------------------------------
    public static class FalloutFinder
    {
        //-------------------------------------------------
        // Gets the Steam installation path.
        //-------------------------------------------------
        public static string SteamFolder()
        {
            RegistryKey steamKey = Registry.LocalMachine.OpenSubKey("Software\\Valve\\Steam") ?? Registry.LocalMachine.OpenSubKey("Software\\Wow6432Node\\Valve\\Steam");
            return steamKey.GetValue("InstallPath").ToString();
        }

        //-------------------------------------------------
        // Gathers all stores library folders in the 
        // config.vdf file.
        //-------------------------------------------------
        public static List<string> LibraryFolders()
        {
            List<string> folders = new List<string>();

            string steamFolder = SteamFolder();
            folders.Add(steamFolder);

            string configFile = steamFolder + "\\config\\config.vdf";

            Regex regex = new Regex("BaseInstallFolder[^\"]*\"\\s*\"([^\"]*)\"");
            using (StreamReader reader = new StreamReader(configFile))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    Match match = regex.Match(line);
                    if (match.Success)
                    {
                        folders.Add(Regex.Unescape(match.Groups[1].Value));
                    }
                }
            }

            return folders;
        }

        //-------------------------------------------------
        // Searches all library folders for FO:NV install
        // folder. Returns empty string on missing.
        //-------------------------------------------------
        public static string GameDir(LocalStorage storage)
        {
            var appFolders = LibraryFolders().Select(x => x + "\\SteamApps\\common");

            if (storage.GamePathOverride != null)
            {
                if (Directory.Exists(storage.GamePathOverride))
                {
                    return storage.GamePathOverride;
                }
            }

            foreach (var folder in appFolders)
            {
                try
                {
                    var matches = Directory.GetDirectories(folder, "Fallout New Vegas");
                    if (matches.Length >= 1)
                    {
                        return matches[0];
                    }

                    matches = Directory.GetDirectories(folder, "Fallout New Vegas enplczru");
                    if (matches.Length >= 1)
                        return matches[0];
                }
                catch (DirectoryNotFoundException)
                {
                    //continue;
                }

            }

            return null;
        }
    }
}
