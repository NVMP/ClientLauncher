using DiscordRPC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClientLauncher.Core.XNative
{
    public class DiscordRichPresence
    {
        private DiscordRpcClient Client;

        public DiscordRichPresence()
        {
            Client = new DiscordRpcClient("536724603054325760");
            Client.Initialize();
        }

        public void Shutdown()
        {
            if (Client.IsInitialized)
            {
                Client.ClearPresence();
                Client.Dispose();
            }
        }

        public enum RichState
        {
            InActive,
            InGame
        }

        protected RichState State;
        protected Dtos.DtoGameServer Server;

        public void SetState(RichState state, GameActivityMonitor.Activity activity = null)
        {
            State = state;
            Server = activity?.Server;

            if (!Client.IsInitialized)
            {
                return;
            }

            switch (State)
            {
                case RichState.InActive:
                    Client.ClearPresence();
                    break;
                case RichState.InGame:
                    if (Server.IsPrivate)
                    {
                        // Limited information to work with
                        Client.SetPresence(new RichPresence()
                        {
                            Details = $"Playing private CO:OP",
                            State = "In Game",
                            Assets = new Assets()
                            {
                                LargeImageKey = "nvmp-logo-1080x1080"
                            },
                            Timestamps = new Timestamps()
                            {
                                Start = activity.Started
                            }
                        });
                    }
                    else
                    {
                        // Limited information to work with
                        Client.SetPresence(new RichPresence()
                        {
                            Details = $"Playing on \"{Server.Name}\"",
                            State = "In Game",
                            Assets = new Assets()
                            {
                                LargeImageKey = "nvmp-logo-1080x1080"
                            },
                            Timestamps = new Timestamps()
                            {
                                Start = activity.Started
                            }
                        });
                    }
                    break;
            }
        }
    }
}
