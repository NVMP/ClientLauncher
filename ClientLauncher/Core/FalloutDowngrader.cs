using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClientLauncher
{
    [Serializable]
    public class PatchManifest
    {
        /// <summary>
        /// File name, for metadata reasons I guess.
        /// </summary>
        [JsonProperty(PropertyName = "file_name")]
        public string FileName { get; set; }

        /// <summary>
        /// SHA1 of the file before the patch.
        /// </summary>
        [JsonProperty(PropertyName = "intake_sha1")]
        public string ExpectedSHA1 { get; set; }

        /// <summary>
        /// SHA1 of the file after the encoded patch is applied.
        /// </summary>
        [JsonProperty(PropertyName = "patched_sha1")]
        public string PatchedSHA1 { get; set; }

        /// <summary>
        /// Encoded patch in Base64. Very verbose, I know.
        /// </summary>
        [JsonProperty(PropertyName = "encoded_patch")]
        public string EncodedPatch { get; set; }
    }

    /// <summary>
    /// I am writing this in preperation for the worst case that Fallout: New Vegas is updated to a new executable very soon, due to Steam
    /// related activity on the depo. This class will be the downgrader. Given a patch file, it will resolve a delta .patch file and apply it
    /// to the target file.
    /// </summary>
    public class FalloutDowngrader
    {
        /// <summary>
        /// Takes the given file, and patches it to the specified patch manifest
        /// </summary>
        /// <param name="file"></param>
        public static bool Patch(FileInfo file, PatchManifest manifest)
        {
            return false;
        }
    }
}
