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
        public static string GithubAPI_ReleasesLatest = "https://api.github.com/repos/NVMP/client-release/releases/latest";

#if !NEXUS_CANDIDATE
        // Used to make a spawned application known of its forked state as so it wont create infinite forks and create a fork bomb.
        public static string Patching_ForkingVariable = "WontYouForkOff";
#endif
    }
}
