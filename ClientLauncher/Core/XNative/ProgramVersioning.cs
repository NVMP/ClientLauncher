using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Web.Script.Serialization;
using System.Windows;

namespace ClientLauncher.Core.XNative
{
    /// <summary>
    /// A helper class that discovers the current launcher's version, and provides information about the current
    /// distribution channel this build is delivered on. This helper is not responsible for patching!
    /// </summary>
    public class ProgramVersioning
    {
        public enum BuildChannel
        {
            /// <summary>
            /// Build is delivered from the automatic version control system, which means it can automatically patch itself.
            /// </summary>
            PublicAutomaticVersionControl,

            /// <summary>
            /// Build is delivered as a deployed package as a beta. It cannot automatically patch itself, and is not intended for public use.
            /// </summary>
            BetaDeployedBuild,

            /// <summary>
            /// Build is delivered as a deployed package for Nexus mods. This build cannot self update itself, it must be updated
            /// manually through other means.
            /// </summary>
            PublicNexusDeployedCandidate,
        }

        public static string GetPublicBuildString(BuildChannel channel)
        {
            switch (channel)
            {
                case BuildChannel.PublicAutomaticVersionControl:
                    return "PTC";
                case BuildChannel.BetaDeployedBuild:
                    return "BETA";
                case BuildChannel.PublicNexusDeployedCandidate:
                    return "NEXUS";
            }
            return "UNK";
        }

        public BuildChannel CurrentBuildChannel
        {
            get
            {
#if NEXUS_CANDIDATE
                return BuildChannel.PublicNexusDeployedCandidate;
#elif DEBUG
                return BuildChannel.BetaDeployedBuild;
#else
                return BuildChannel.PublicAutomaticVersionControl;
#endif
            }
        }

        private readonly string GameDir;

        public string BuildFilename => ".nvmp_version";
        public string BuildFilenameFull => $"{GameDir}\\{BuildFilename}";

        public string CurrentVersion
        {
            get
            {
                if (File.Exists( BuildFilenameFull ))
                {
                    return File.ReadAllText( BuildFilenameFull ).Trim();
                }

                return "UnknownIOVersion";
            }
        }

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

        /// <summary>
        /// Queries the release information about the mod on GitHub. This call can throw a number of exceptions,
        /// as it does various online queries - and de-serialization operations
        /// </summary>
        /// <returns></returns>
        internal GitHubRelease GetLatestProgramRelease()
        {
            // Get the releases information from GitHub
            using (var wc = new WebClient())
            {
                wc.Headers.Add("User-Agent", "NVMP/X");

                var serialiser = new JavaScriptSerializer();

                var json = wc.DownloadString(XNativeConfig.GithubAPI_ReleasesLatest);
                return serialiser.Deserialize<GitHubRelease>(json);
            }
        }

        public GitHubRelease LatestRelease { get; }

        public bool IsOutOfDate => LatestRelease != null && CurrentVersion != LatestRelease.tag_name;

        public string CurrentVersionAndChannel
        {
            get
            {
                if (IsOutOfDate)
                {
                    return $"{GetPublicBuildString(CurrentBuildChannel)}__{CurrentVersion} [Update {LatestRelease.tag_name} Available]";
                }

                return $"{GetPublicBuildString(CurrentBuildChannel)}__{CurrentVersion}";
            }
        }

        public ProgramVersioning(string gameDirectory)
        {
            GameDir = gameDirectory;
            LatestRelease = null;

            try
            {
                LatestRelease = GetLatestProgramRelease();
            } catch (WebException e)
            {
                MessageBox.Show($"Could not check for updates, please check your internet connection\n\n{e}");
            }
            catch (Exception)
            {
                MessageBox.Show("Failed to parse latest release from patching services");
            }
        }
    }
}
