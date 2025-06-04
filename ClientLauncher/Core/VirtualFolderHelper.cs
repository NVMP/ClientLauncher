using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ClientLauncher.Core
{
    public class VirtualFolderHelper
    {
        private static readonly string MPVirtualFolderName = $"nvmp\\vdata";
        private static readonly string MPVirtualFolderWatermarkFileName = "nvmp_mapped.txt";

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct FILETIME
        {
            public uint DateTimeLow;
            public uint DateTimeHigh;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BY_HANDLE_FILE_INFORMATION
        {
            public uint FileAttributes;
            public FILETIME CreationTime;
            public FILETIME LastAccessTime;
            public FILETIME LastWriteTime;
            public uint VolumeSerialNumber;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }


        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess,
                                               uint dwShareMode, IntPtr lpSecurityAttributes,
                                               uint dwCreationDisposition, uint dwFlagsAndAttributes,
                                               IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandle(IntPtr hFile, out BY_HANDLE_FILE_INFORMATION lpFileInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        private uint GetNumFileLinkCount(string filename)
        {
            // I hate this...
            IntPtr hFileHandle = CreateFile(filename, (uint)FileAccess.Read, (uint)FileShare.Read, IntPtr.Zero, (uint)FileMode.Open, (uint)FileAttributes.Archive, IntPtr.Zero);
            if ((int)hFileHandle == -1)
            {
                return 0;
            }

            uint iResult = 0;

            if (GetFileInformationByHandle(hFileHandle, out BY_HANDLE_FILE_INFORMATION info))
            {
                iResult = info.NumberOfLinks;
            }

            CloseHandle(hFileHandle);

            return iResult;
        }

        /// <summary>
        /// The server folder name to use for the virtual folder helper. This is where all our custom data content is stored.
        /// </summary>
        public string ServerFolderName { get; set; }

        /// <summary>
        /// Where the current game installation is at.
        /// </summary>
        public string GameDirectory { get; set; }

        /// <summary>
        /// Builds the unique folder name for the server.
        /// </summary>
        public string UniqueFolderName
        {
            get
            {
                return Path.Combine(GameDirectory, MPVirtualFolderName, ServerFolderName);
            }
        }

        /// <summary>
        /// A quick accessor to the standard game data folder.
        /// </summary>
        public string GameDataFolder
        {
            get
            {
                return Path.Combine(GameDirectory, $"Data");
            }
        }

        public bool DoesFileExist(string filename, out string path)
        {
            string virtualPath = Path.Combine(UniqueFolderName, filename);
            if (File.Exists(virtualPath))
            {
                path = virtualPath;
                return true;
            }

            string dataPath = Path.Combine(GameDataFolder, filename);
            if (File.Exists(dataPath))
            {
                path = dataPath;
                return true;
            }

            path = null;
            return false;
        }

        /// <summary>
        /// Sets up the virtual folder for the specified server, and creates any missing folders in the tree.
        /// All previous virtual mappings are removed if this sever is not the same as the previous one used.
        /// </summary>
        public void Initialize()
        {
            if (!Directory.Exists(MPVirtualFolderName))
                Directory.CreateDirectory(MPVirtualFolderName);

            if (!Directory.Exists(UniqueFolderName))
                Directory.CreateDirectory(UniqueFolderName);

            // Check the watermark file
            string watermarkFile = Path.Combine(GameDataFolder, MPVirtualFolderWatermarkFileName);
            bool bRequiresFolderVirtualWipe = false;
            if (File.Exists(watermarkFile))
            {
                try
                {
                    var previousServerVirtualName = File.ReadAllText(watermarkFile);
                    if (previousServerVirtualName != UniqueFolderName)
                        bRequiresFolderVirtualWipe = true;

                } catch
                {
                    bRequiresFolderVirtualWipe = true;
                }
            }
            else
            {
                // No file exists, so purge it all
                bRequiresFolderVirtualWipe = true;
            }

            if (bRequiresFolderVirtualWipe)
            {
                try
                {
                    if (!UnmapAllVirtualFiles())
                    {
                        MessageBox.Show("Unable to unmap previous server files from disk! The server may complain about invalid mod revisions.", "NV: Multiplayer");
                    }
                    File.WriteAllText(watermarkFile, UniqueFolderName);
                }
                catch { }
            }
        }

        internal class Mapping
        {
            public string Original { get; set; }
            public string NewDest { get; set; }
        }

        internal bool MapFiles(IEnumerable<Mapping> files)
        {
            try
            {
                var mkLinkCommands = new List<string>();
                foreach (var file in files)
                {
                    // Remove any files from the IO to make this succeed
                    try
                    {
                        if (File.Exists(file.NewDest))
                        {
                            File.Delete(file.NewDest);
                        }
                    }
                    catch { }

                    mkLinkCommands.Add($"mklink /h \"{file.NewDest}\" \"{file.Original}\"");
                }

                var mappingCmd = new Process();
                mappingCmd.StartInfo.FileName = "cmd.exe";
                mappingCmd.StartInfo.UseShellExecute = true;
                mappingCmd.StartInfo.CreateNoWindow = true;
                mappingCmd.StartInfo.Arguments = $"/c " + String.Join(" && ", mkLinkCommands);
                mappingCmd.StartInfo.WorkingDirectory = Directory.GetCurrentDirectory();

                bool result = mappingCmd.Start();
                if (result)
                {
                    mappingCmd.WaitForExit(); // Make sure this process is completed before starting NV:MP
                }

                return result && mappingCmd.ExitCode == 0;
            } catch (Exception e)
            {
                Trace.WriteLine(e);
            }

            return false;
        }

        /// <summary>
        /// Unmaps ALL virtual files in the game data folder.
        /// </summary>
        internal bool UnmapAllVirtualFiles()
        {
            var files = Directory.GetFiles(GameDataFolder, "*", SearchOption.TopDirectoryOnly);
            foreach (string file in files)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.Exists)
                {
                    // Remove any file that is a hard link, we don't support these anymore but worth purging
                    // Remove any file that has more than one hard link, meaning it is likely mapped from another location
                    if (fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint) || GetNumFileLinkCount(file) > 1)
                    {
                        // Unlink
                        try
                        {
                            File.Delete(file);
                        }
                        catch
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Maps ALL the server files into the game data folder. If virtual mapping cannot be completed, then the server files
        /// are instead copied over to the game data folder as a backup.
        /// *files* are relative to Data/, so do not include it.
        /// </summary>
        public bool MapAllServerFiles(IEnumerable<string> files)
        {
            // Remove all mappings firstly.
            UnmapAllVirtualFiles();

            // Validate that all the files passed in exist in the virtual data folder, or if they are in the data
            // folder then there is no need to map them.
            var toMapFiles = new List<string>();

            foreach (var file in files)
            {
                if (!File.Exists(Path.Combine(UniqueFolderName, file)))
                {
                    if (!File.Exists(Path.Combine(GameDataFolder, file)))
                    {
                        Trace.WriteLine($"{file} not found in the virtual folder, or the game folder");
                        return false;
                    }
                }
                else
                {
                    // Add it for mapping
                    toMapFiles.Add(file);
                }
            }

            // Now all the files are within the virtual folder, we want to build a list of mappings to attempt.
            // The mapping may fail for numerous reasons, the user might not have administrator privs, they might just say no to the UAC prompt, or they
            // might have some other sort of I/O error that prevents the mapping from working.
            //
            // So if this process fails, we fall back to a direct copy.
            var virtualMappings = new List<Mapping>();

            foreach (var file in toMapFiles)
            {
                var mapping = new Mapping
                {
                    Original = Path.Combine(UniqueFolderName, file),
                    NewDest = Path.Combine(GameDataFolder, file)
                };

                virtualMappings.Add(mapping);
            }

            Trace.WriteLine($"Mapping files...");
            if (!MapFiles(virtualMappings))
            {
                Trace.WriteLine($"Falling back to hard copy...");

                // This failed. Purge all the links we've made just in-case they reside
                UnmapAllVirtualFiles();

                foreach (var file in toMapFiles)
                {
                    try
                    {
                        Trace.WriteLine($"Copying {file}...");
                        File.Copy(Path.Combine(UniqueFolderName, file), Path.Combine(GameDataFolder, file), true);
                    }
                    catch { }
                }
            }

            Trace.WriteLine($"Complete");
            return true;
        }
    }
}
