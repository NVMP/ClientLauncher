using ENet.Managed;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ClientLauncher.Core.XNative
{
    public class ServerProbe
    {
        static readonly uint ConnectionProbeHeader = 0x8008A000; // keep in sync with xnative

        private ENetHost Host;
        private IPEndPoint Address;
        private ENetPeer Peer;

        public class ProbeStatus
        {
            public enum ProbeState
            {
                Unreachable,
                AwaitingReply,
                Replied
            };

            public ProbeState State { get; set; }

            public ICollection<string> CSVEntries { get; set; }
        }

        public ServerProbe(Dtos.DtoGameServer serverInfo)
        {
            var hostEntry = Dns.GetHostEntry(serverInfo.IP);
            Address = new IPEndPoint(hostEntry.AddressList.Where(s => s.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).FirstOrDefault(), serverInfo.Port);

            IPEndPoint listenEndPoint = null;
            Host = new ENetHost(listenEndPoint, 1, 1);
        }

        public void Shutdown()
        {
            Host.Dispose();
        }

        public ProbeStatus Connect()
        {
            Peer = Host.Connect(Address, 1, ConnectionProbeHeader);

            var result = new ProbeStatus { State = ProbeStatus.ProbeState.Unreachable };
            bool service = true;

            var connectionEvent = Host.Service(TimeSpan.FromMilliseconds(1250));
            if (connectionEvent.Type != ENetEventType.Connect)
            {
                return result;
            }

            result.State = ProbeStatus.ProbeState.AwaitingReply;

            while (service)
            {
                var serviceEvent = Host.Service(TimeSpan.FromMilliseconds(250));

                switch (serviceEvent.Type)
                {
                    case ENetEventType.Disconnect:
                    {
                        Shutdown();
                        service = false;
                        break;
                    }
                    case ENetEventType.Receive:
                    {
                        result.State = ProbeStatus.ProbeState.Replied;

                        if (serviceEvent.Packet != null)
                        {
                            if (serviceEvent.Packet.Data.Length <= 1024)
                            {
                                var buffer = Encoding.ASCII.GetString(serviceEvent.Packet.Data.ToArray());
                                result.CSVEntries = buffer.Split(',');
                            }
                        }

                        break;
                    }
                    default: { break; }
                }
            }

            return result;
        }
    }
}
