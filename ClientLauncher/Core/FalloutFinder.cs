using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using System.Text.RegularExpressions;
using System.IO;
using ClientLauncher.Core;
using System;
using System.Windows.Forms;

namespace ClientLauncher
{
    /// <summary>
    /// Finds the machine's Fallout: New Vegas storage
    /// location. 
    /// </summary>
    public static class FalloutFinder
    {
        static private string[][] RegistryKeys =
        {
            new string[] { "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Steam App 22380", "InstallLocation" },
            new string[] { "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Steam App 22490", "InstallLocation" },
            new string[] { "Software\\GOG.com\\Games\\1312824873", "path" },
            new string[] { "Software\\GOG.com\\Games\\1454587428", "path" },
        };

        public static bool IsFolderFalloutNVInstallation(string folder)
        {
            if (!Directory.Exists(folder))
                return false;

            if (File.Exists(Path.Combine(folder, "FalloutNV.exe")))
                return true;

            return false;
        }

        private static string FindDiscoverableGame()
        {
            foreach (var key in RegistryKeys)
            {
                using (var regKey = Registry.LocalMachine.OpenSubKey(key[0]))
                {
                    if (regKey != null)
                    {
                        var value = (string)regKey.GetValue(key[1]);
                        if (value != null)
                        {
                            if (value.Length != 0)
                            {
                                if (IsFolderFalloutNVInstallation(value))
                                    return value;
                            }
                        }
                    }
                }
            }
            return null;
        }

        //-------------------------------------------------
        // Searches all library folders for FO:NV install
        // folder. Returns empty string on missing.
        //-------------------------------------------------
        public static string GameDir(LocalStorage storage)
        {
            // Prioritise the game path in use
            if (storage.GamePathOverride != null)
            {
                if (Directory.Exists(storage.GamePathOverride))
                {
                    return storage.GamePathOverride;
                }
            }

            // Default to the first discovered game on the system
            string discoveredGame = FindDiscoverableGame();
            if (discoveredGame != null)
            {
                return discoveredGame;
            }

            // The ultimate fallback, use the current directory if it's a valid installation
            string current = Directory.GetCurrentDirectory();
            if (IsFolderFalloutNVInstallation(current))
            {
                return current;
            }

            return null;
        }

        //-------------------------------------------------
        // Prompts the user to locate a game directory.
        // Returns NULL if they back out.
        //-------------------------------------------------
        public static string PromptToFindDirectory(string startAtDirectory = null)
        {
            while (true)
            {
                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.Description = "Please locate your Fallout: New Vegas installation to mount NV:MP into";
                    
                    if (startAtDirectory != null)
                    {
                        dialog.SelectedPath = startAtDirectory;
                    }

                    if (dialog.ShowDialog() != DialogResult.OK)
                    {
                        // User cancelled the prompt, so just backout and return null.
                        return null;
                    }

                    // Check if the path is valid or not.
                    if (dialog.SelectedPath != null)
                    {
                        if (!IsFolderFalloutNVInstallation(dialog.SelectedPath))
                        {
                            // Warn them, and then restart the process
                            MessageBox.Show("New Vegas: Multiplayer", "The supplied path does not contain a valid Fallout: New Vegas installation.\n\nPlease select a folder that contains FalloutNV.exe!",
                               MessageBoxButtons.OK, MessageBoxIcon.Error);
                            continue;
                        }

                        return dialog.SelectedPath;
                    }
                }
            }
        }
    }
}
