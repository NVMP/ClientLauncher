using System;
using NetFwTypeLib;
using System.Diagnostics;

namespace ClientLauncher
{
    public class Firewall
    {
        private static string FirewallName = "NVMP Networking";
        private static string FirewallDesc = "Used to allow NVMP services to communicate.";
        
        private static void RemoveExistingRule(ref INetFwPolicy2 policy, string Name)
        {
            INetFwRule existing = null;

            foreach (INetFwRule rule in policy.Rules)
            {
                if (rule.Name == Name)
                {
                    existing = rule;
                    break;
                }
            }

            // Delete existing firewall rules, as this may be invoked by
            // the repair utility.
            if (existing != null)
            {
                Debug.WriteLine("Firewall entry already exists for " + Name + ", removing...");
                policy.Rules.Remove(existing.Name);
            }
        }

        private static void Install(string FalloutEXE, string Suffix, NET_FW_RULE_DIRECTION_ dir)
        {
            string FullFirewallName = FirewallName + " " + Suffix;

            INetFwPolicy2 policy = (INetFwPolicy2)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));

            // Remove existing rules defined by previous calls.
            RemoveExistingRule(ref policy, FullFirewallName);

            // Add an entry for outgoing and incoming connections to NVMP.
            INetFwRule rule = (INetFwRule)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FWRule"));
            rule.Action = NET_FW_ACTION_.NET_FW_ACTION_ALLOW;
            rule.Description = FirewallDesc;
            rule.Direction = dir;
            rule.Enabled = true;
            rule.ApplicationName = FalloutEXE;

            // UDP, NVMP uses full UDP connectivity.
            rule.Protocol = 17;
            rule.Name = FullFirewallName;
            rule.InterfaceTypes = "all";

            Trace.WriteLine("Adding new firewall entry...");
            policy.Rules.Add(rule);
        }

        public static bool InstallRules(string FalloutEXE)
        {
            Install(FalloutEXE, "Incoming", NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_IN);
            Install(FalloutEXE, "Outgoing", NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_OUT);
            
            return true;
        }
    }
}