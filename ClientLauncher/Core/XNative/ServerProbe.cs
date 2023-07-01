using ENet.Managed;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ClientLauncher.Core.XNative
{
    /// <summary>
    /// Establishes a small connection to the server to probe for mod information, player state, and general connectivity before
    /// handing off to the main server socket.
    /// </summary>
    public class ServerProbe : IDisposable
    {
        static readonly uint ConnectionProbeHeader = 0x8008A001; // keep in sync with xnative

        private ENetHost Host;
        private IPEndPoint Address;
        private ENetPeer Peer;

        public class ProbeStatus
        {
            public enum ProbeState
            {
                Unreachable,
                AwaitingReply,
                ReplyOK,
                ReplyMalformed,
            };

            public ProbeState State { get; set; }

            public NetProbe Result { get; set; }
        }

        public ServerProbe(Dtos.DtoGameServer serverInfo)
        {
            // First try to resolve it as a public IP
            try
            {
                IPAddress ip = IPAddress.Parse(serverInfo.IP);
                Address = new IPEndPoint(ip, serverInfo.Port);
            }
            catch (Exception)
            {
                // Try to resolve it as a host entry instead
                var hostEntry = Dns.GetHostEntry(serverInfo.IP);
                Address = new IPEndPoint(hostEntry.AddressList.Where(s => s.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).FirstOrDefault(), serverInfo.Port);
            }

            IPEndPoint listenEndPoint = null;
            Host = new ENetHost(listenEndPoint, 1, 1);
            Host.CompressWithRangeCoder();
        }

        public void Dispose()
        {
            Host?.Dispose();
        }

        public void Shutdown()
        {
            Host?.Dispose();
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
                        if (result.State == ProbeStatus.ProbeState.AwaitingReply)
                        {
                            result.State = ProbeStatus.ProbeState.Unreachable;
                        }
                        break;
                    }
                    case ENetEventType.Receive:
                    {
                        result.State = ProbeStatus.ProbeState.ReplyMalformed;

                        if (serviceEvent.Packet != null)
                        {
                            try
                            {
                                var packet = NetProbe.Parser.ParseFrom(serviceEvent.Packet.Data.ToArray());
                                result.Result = packet;
                                result.State = ProbeStatus.ProbeState.ReplyOK;
                            }
                            catch (Exception)
                            {
                                result.Result = null;
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
