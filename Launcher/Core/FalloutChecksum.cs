using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.IO;
using System.Diagnostics;

namespace ClientLauncher
{
    public class FalloutChecksum
    {
        public const string FALLOUTNV_STANDARD = "D068F394521A67C6E74FE572F59BD1BE71E855F3";
        public const string FALLOUTNV_RUSSIAN  = "5394B94A18FFA6FA846E1D6033AD7F81919F13AC";
        public const string FALLOUTNV_UNKNOWN_1  = "3980940522F0264ED9AF14AEA1773BB19F5160AB";
        public const string FALLOUTNV_UNKNOWN_2 = "07AFFDA66C89F09B0876A50C77759640BC416673";
        public const string FALLOUTNV_UNKNOWN_3 = "F65049B0957D83E61ECCCACC730015AE77FB4C8B";
        public const string FALLOUTNV_UNKNOWN_4 = "ACA83D5A12A64AF8854E381752FE989692D46E04";
        public const string FALLOUTNV_UNKNOWN_5 = "946D2EABA04A75FF361B8617C7632B49F1EDE9D3";

        public const string FALLOUTNV_4GB_1 = "0021023E37B1AF143305A61B7B29A1811CC7C5FB";
        public const string FALLOUTNV_4GB_2 = "37CAE4E713B6B182311F66E31668D5005D1B9F5B";
        public const string FALLOUTNV_4GB_3 = "600CD576CDE7746FB2CD152FDD24DB97453ED135";
        public const string FALLOUTNV_4GB_4 = "34B65096CAEF9374DD6AA39AF855E43308B417F2";

        protected class HashDescription
        {
            public string Hash { get; set; }
            public string Name { get; set; }
        }

        private static IDictionary<string, List<HashDescription>> HashDatabase = new Dictionary<string, List<HashDescription>>
        {
            { "FalloutNV.exe", new List<HashDescription> {

                // Game Executables
                new HashDescription { Hash = FALLOUTNV_STANDARD, Name = "FalloutNV Steam" },
                new HashDescription { Hash = FALLOUTNV_RUSSIAN, Name = "FalloutNV Steam enplczru" },
                new HashDescription { Hash = FALLOUTNV_UNKNOWN_1, Name = "FalloutNV English" },
                new HashDescription { Hash = FALLOUTNV_UNKNOWN_2, Name = "FalloutNV English" },
                new HashDescription { Hash = FALLOUTNV_UNKNOWN_3, Name = "FalloutNV English" },
                new HashDescription { Hash = FALLOUTNV_UNKNOWN_4, Name = "FalloutNV English" },
                new HashDescription { Hash = FALLOUTNV_UNKNOWN_5, Name = "FalloutNV English" },
                
                // 4GB Launcher Executables
                new HashDescription { Hash = FALLOUTNV_4GB_1, Name = "FalloutNV 4GB" },
                new HashDescription { Hash = FALLOUTNV_4GB_2, Name = "FalloutNV 4GB" },
                new HashDescription { Hash = FALLOUTNV_4GB_3, Name = "FalloutNV 4GB" },
                new HashDescription { Hash = FALLOUTNV_4GB_4, Name = "FalloutNV 4GB" },
            } }
            //{ "steam_api.dll", new List<HashDescription> {
            //    new HashDescription { Hash = "20867924FEAB62E443E90F841822A9B43C8C8B12", Name = "steam_api.dll" }
            //} },
//            { "awesomium.dll", new List<string> { "91BBF94EB4493D7DA15F237143C720CD" } },
        };

        private static IDictionary<string, int> GameIDLookup = new Dictionary<string, int>
        {
            { FALLOUTNV_STANDARD, 22380 },
            { FALLOUTNV_UNKNOWN_1, 22380 },
            { FALLOUTNV_UNKNOWN_2, 22380 },
            { FALLOUTNV_UNKNOWN_3, 22380 },
            { FALLOUTNV_UNKNOWN_4, 22380 },
            { FALLOUTNV_UNKNOWN_5, 22380 },

            { FALLOUTNV_4GB_1,    22380 },
            { FALLOUTNV_4GB_2,    22380 },
            { FALLOUTNV_4GB_3,    22380 },
            { FALLOUTNV_4GB_4,    22380 },

            { FALLOUTNV_RUSSIAN,  22490 }
        };

        public static int LookupGameID(string GameDir)
        {
            int GameID = 0;
            string FileHash = null;

            // Calculate  the executable hash ID.
            using (var InputStream = File.OpenRead(GameDir + "\\FalloutNV.exe"))
            using (var InputBuffer = new BufferedStream(InputStream, 1200000))
            {
                using (var sha1 = new SHA1Managed())
                {
                    byte[] hash = sha1.ComputeHash(InputBuffer);

                    StringBuilder formatted = new StringBuilder(2 * hash.Length);

                    foreach (byte b in hash)
                    {
                        formatted.AppendFormat("{0:X2}", b);
                    }

                    FileHash = formatted.ToString();
                }
            }

            // Get all the current hashes for the FalloutNV.exe file.
            var GameHashes = HashDatabase["FalloutNV.exe"];

            // If there's a match in the GameIDLookup, use the AppID thats in there.
            foreach (HashDescription ExeHash in GameHashes)
            {
                if (FileHash == ExeHash.Hash)
                {
                    GameID = GameIDLookup[ FileHash ];
                }
            }
            
            return GameID;
        }
        
        private static string ValidateFile(string Filename, string DirectoryPath)
        {
            string FullPath = DirectoryPath + "\\" + Filename;

            if (!File.Exists(FullPath))
            {
                Trace.WriteLine("ValidateFile: file doesn't exist (" + FullPath + ")");
                return "missing " + Filename;
            }

            string FileHash = null;
            List<HashDescription> RealHashes = null;

            try
            {
                RealHashes = HashDatabase[ Filename ];
            }
            catch (Exception)
            {
                Trace.WriteLine("ValidateFile: no file entry in database for (" + FullPath + ")");
                return "dberror for " + Filename;
            }

            // Start calculating file hash.
            try {

                using (var InputStream = File.OpenRead(FullPath))
                using (var InputBuffer = new BufferedStream(InputStream, 1200000))
                {
                    using (var sha1 = new SHA1Managed())
                    {
                        byte[] hash = sha1.ComputeHash(InputBuffer);

                        StringBuilder formatted = new StringBuilder(2 * hash.Length);

                        foreach (byte b in hash)
                        {
                            formatted.AppendFormat("{0:X2}", b);
                        }

                        FileHash = formatted.ToString();
                    }
                }

            } catch (Exception e)
            {
                Trace.WriteLine("ValidateFile: file open error, " + e.ToString());
                return "fileopen error: " + Filename;
            }

            bool ValidHash = false;
            foreach (HashDescription HashTest in RealHashes)
            {
                if (FileHash == HashTest.Hash)
                {
                    ValidHash = true;
                    break;
                }
            }

            if (!ValidHash)
            {
                Trace.WriteLine("ValidateFile: hash not met for " + FullPath + ", got (" + FileHash + ")");
                return "invalid file " + Filename;
            }

            return null;
        }

        public static string IsGameCorrectVersion(string DirectoryPath)
        {
            string ErrMessage = null;

            foreach (KeyValuePair<string, List<HashDescription>> HashPair in HashDatabase)
            {
                ErrMessage = ValidateFile(HashPair.Key, DirectoryPath);
                if (ErrMessage != null)
                {
                    return ErrMessage;
                }
            }
            

            return ErrMessage;
        }
    }
}
