using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace ClientLauncher.Core.XNative
{
#if !NEXUS_CANDIDATE
    /// <summary>
    /// Helpers to extract .zip files onto disk.
    /// For Nexus submissions this is completely stripped as to follow Nexus submission terms of service.
    /// </summary>
    public static class ZipArchiveExtensions
    {
        public static void ExtractToDirectory(this ZipArchive archive, string destinationDirectoryName, bool overwrite)
        {
            if (!overwrite)
            {
                archive.ExtractToDirectory(destinationDirectoryName);
                return;
            }

            DirectoryInfo di = Directory.CreateDirectory(destinationDirectoryName);
            string destinationDirectoryFullPath = di.FullName;

            foreach (ZipArchiveEntry file in archive.Entries)
            {
                string completeFileName = Path.GetFullPath(Path.Combine(destinationDirectoryFullPath, file.FullName));

                if (!completeFileName.StartsWith(destinationDirectoryFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException("Trying to extract file outside of destination directory. ");
                }

                if (file.Name == "")
                {// Assuming Empty for Directory
                    Directory.CreateDirectory(Path.GetDirectoryName(completeFileName));
                    continue;
                }

                try
                {
                    file.ExtractToFile(completeFileName, true);
                }
                catch (Exception e)
                {
                    var StaticDLLs = new string[]
                    {
                        // These files will hardly change, and we can remove this patch if desired in the future.
                        // We cannot patch them after the first update, as they are files loaded by the assembly themselves.
                        // In future, maybe we unload them?
                        "EOSSDK-Win32-Shipping.dll",
                        "discord_game_sdk.dll"
                    };

                    if (!StaticDLLs.Contains(file.Name))
                    {
                        throw e;
                    }
                }
            }
        }
    }
#endif
}
