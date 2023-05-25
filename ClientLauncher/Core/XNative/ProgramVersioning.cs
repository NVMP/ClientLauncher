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
                    return "PUBLIC";
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
                if (CurrentVersion.ToLower().EndsWith("_beta"))
                {
                    return BuildChannel.BetaDeployedBuild;
                }

                return BuildChannel.PublicAutomaticVersionControl;
#endif
            }
        }

        public bool IsCurrentBuildStale = false;

        private readonly string GameDir;

        public string BuildFilename => ".nvmp_version";
        public string BuildFilenameFull => $"{GameDir}\\{BuildFilename}";


        internal string InternalCachedCurrentVersion = null;
        public string CurrentVersion
        {
            get
            {
                if (InternalCachedCurrentVersion == null)
                {
                    if (File.Exists(BuildFilenameFull))
                    {
                        InternalCachedCurrentVersion = File.ReadAllText(BuildFilenameFull).Trim();
                    }
                }

                if (InternalCachedCurrentVersion == null)
                {
                    return "UnknownIOVersion";
                }

                return InternalCachedCurrentVersion;
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

        internal string GithubAPIURL
        {
            get
            {
                if (CurrentBuildChannel == BuildChannel.BetaDeployedBuild)
                {
                    return XNativeConfig.GithubAPI_BetaReleasesLatest;
                }
                return XNativeConfig.GithubAPI_ReleasesLatest;
            }
        }

        /// <summary>
        /// Queries the release information about the mod on GitHub. This call can throw a number of exceptions,
        /// as it does various online queries - and de-serialization operations
        /// </summary>
        /// <returns></returns>
        internal GitHubRelease GetLatestProgramRelease()
        {
            string TempCacheFilepath = $"{Path.GetTempPath()}\\{GetPublicBuildString(CurrentBuildChannel)}.{XNativeConfig.GithubAPI_CacheFilename}";
            var serialiser = new JavaScriptSerializer();

            // Get the releases information from GitHub
            try
            {
                using (var wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "NVMP/X");
                    var json = wc.DownloadString(GithubAPIURL);
                    try
                    {
                        File.WriteAllText(TempCacheFilepath, json);
                    }
                    catch (Exception) { }
                    return serialiser.Deserialize<GitHubRelease>(json);
                }
            }
            catch (WebException e)
            {
                // HTTP download failed, try to use a cached file and report this installation as stale
                IsCurrentBuildStale = true;

                if (!File.Exists(TempCacheFilepath))
                {
                    // Propagate, can't use the file as it doesn't exist
                    throw e;
                }

                if ((DateTimeOffset.UtcNow -  File.GetLastWriteTimeUtc(TempCacheFilepath)).TotalDays >= XNativeConfig.GithubAPI_CacheMaxDaysAllowed)
                {
                    // Propagate, can't use the file as it's been stale for too long. 
                    throw e;
                }

                // Go for the file  
                var json = File.ReadAllText(TempCacheFilepath);
                return serialiser.Deserialize<GitHubRelease>(json);
            }
        }

        public GitHubRelease LatestRelease { get; }

        public bool IsOutOfDate => LatestRelease != null && CurrentVersion != LatestRelease.tag_name;

        public string CurrentVersionAndChannel
        {
            get
            {
                var tags = new List<string>();

                if (IsOutOfDate)
                {
                    tags.Add("[Update {LatestRelease.tag_name} Available]");
                }

                if (IsCurrentBuildStale)
                {
                    tags.Add("[Offline]");
                }

                return $"{GetPublicBuildString(CurrentBuildChannel)}__{CurrentVersion} {String.Join(" ", tags)}";
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
#if !DEBUG
                MessageBox.Show("Failed to parse latest release from patching services");
#endif
            }
        }
    }
}
