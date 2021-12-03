using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using System.Text.RegularExpressions;
using System.IO;
using ClientLauncher.Core;
using System;

namespace ClientLauncher
{
    /// <summary>
    /// Finds the machine's Fallout: New Vegas storage
    /// location. 
    /// </summary>
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

            try
            {
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
            } catch (Exception)
            {
            }

            return folders;
        }

        //-------------------------------------------------
        // Searches all library folders for FO:NV install
        // folder. Returns empty string on missing.
        //-------------------------------------------------
        public static string GameDir(LocalStorage storage)
        {
            if (storage.GamePathOverride != null)
            {
                if (Directory.Exists(storage.GamePathOverride))
                {
                    return storage.GamePathOverride;
                }
            }

            return Directory.GetCurrentDirectory();
        }
    }
}
