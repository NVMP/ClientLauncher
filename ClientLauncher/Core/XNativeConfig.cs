using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClientLauncher.Core
{
    public static class XNativeConfig
    {
        // Used to query new updates to NV:MP's backend, and if the build supports it, to automatically patch from
        public static string GithubAPI_ReleasesLatest = "https://api.github.com/repos/NVMP_A/client-release/releases/latest";

        public static string GithubAPI_BetaReleasesLatest = null;

        // On successful download, what is the cache filename to use if future calls fail?
        public static string GithubAPI_CacheFilename = "nvmp.github.cache";

        // How long can a cached API call last on disk before it becomes invalid (to prevent stale installations for an unknown reason)
        public static int GithubAPI_CacheMaxDaysAllowed = 3;

        public static string Exe_PrivateServer = "NVMP_StoryServer.exe";

#if !NEXUS_CANDIDATE
        // Used to make a spawned application known of its forked state as so it wont create infinite forks and create a fork bomb.
        public static string Patching_ForkingVariable = "WontYouForkOff";
#endif
    }
}
