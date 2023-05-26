using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using NLog;

namespace Outernet
{
    public class SysHelper
    {
        private static readonly NLog.Logger _logger = Logger.IsLoggerInited() ? LogManager.GetCurrentClassLogger() : null;
        private bool _networkInited;
        private Interface? _tunIf;
        private Interface? _curIf;
        private string _serverAddr;
        private string _ipv4Addr;
        private string _ipv4Gateway;
        private string _ipv4NetMask;

        public SysHelper()
        {
            _logger?.Info("SysHelper.SysHelper");
            _networkInited = false;
            _tunIf = null;
            _curIf = null;
            _serverAddr = string.Empty;
            _ipv4Addr = string.Empty;
            _ipv4Gateway = string.Empty;
            _ipv4NetMask = string.Empty;
        }

        public static IEnumerable<Interface> GetInterfaces()
        {
            try
            {
                var ret = new List<Interface>();

                var bufferSize = 0;
                GetAdaptersInfo(IntPtr.Zero, ref bufferSize);
                var buffer = Marshal.AllocHGlobal(bufferSize);
                var result = GetAdaptersInfo(buffer, ref bufferSize);
                if (result == 0)
                {
                    var adapterInfoPtr = buffer;
                    while (adapterInfoPtr != IntPtr.Zero)
                    {
                        var adapterInfo = Marshal.PtrToStructure<IP_ADAPTER_INFO>(adapterInfoPtr);

                        var iface = new Interface
                        {
                            Guid = adapterInfo.AdapterName,
                            Index = (int)adapterInfo.Index,
                            Addr = adapterInfo.IpAddressList.IpAddress.Address,
                            Mask = adapterInfo.IpAddressList.IpMask.Address,
                            Gateway = adapterInfo.GatewayList.IpAddress.Address
                        };
                        ret.Add(iface);

                        adapterInfoPtr = adapterInfo.Next;
                    }
                }
                else
                {
                    _logger?.Error($"SysHelper.GetInterfaces, GetAdaptersInfo failed, result: {result}");
                }

                Marshal.FreeHGlobal(buffer);
                return ret;
            }
            catch (Exception ex)
            {
                _logger?.Error($"SysHelper.GetInterfaces, error: {ex}");
                return new List<Interface>();
            }
        }

        public static Interface? GetTunInterface()
        {
            try
            {
                var list = GetInterfaces();
                foreach (var iface in list)
                {
                    if (iface.Guid.Equals("{c55970ca-5f6d-443d-8e50-2862939b5b0f}", StringComparison.OrdinalIgnoreCase))
                    {
                        return iface.Copy();
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger?.Error($"SysHelper.GetTunInterface, error: {ex}");
                return null;
            }
        }

        public static Interface? GetCurInterface()
        {
            try
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.Bind(new IPEndPoint(IPAddress.Any, 0));
                var remoteIPAddress = IPAddress.Parse("8.8.8.8");
                var remotePort = 53;
                socket.Connect(remoteIPAddress, remotePort);

                var localEndPoint = (IPEndPoint)socket.LocalEndPoint;
                var localIPAddress = localEndPoint.Address.ToString();

                socket.Close();

                var list = GetInterfaces();
                foreach (var iface in list)
                {
                    if (iface.Addr == localIPAddress)
                    {
                        return iface.Copy();
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger?.Error($"SysHelper.GetCurInterface, error: {ex}");
                return null;
            }
        }

        public static void FixNetwork()
        {
            try
            {
                _logger?.Info("SysHelper.FixNetwork");
                Utils.Exec("netsh winsock reset");
                Utils.Exec("route -f");
            }
            catch (Exception ex)
            {
                _logger?.Error($"SysHelper.FixNetwork, error: {ex}");
            }
        }

        public static void RestartPc()
        {
            try
            {
                _logger?.Info("SysHelper.RestartPc");
                Utils.Exec("shutdown /r /t 0");
            }
            catch (Exception ex)
            {
                _logger?.Error($"SysHelper.RestartPc, error: {ex}");
            }
        }

        public bool InitNetworkIpv4(
            string serverAddr,
            string ipv4Addr,
            string ipv4Gateway,
            string ipv4NetMask,
            int tunMtu,
            string ipv4MainDnsServer,
            string ipv4SecondaryDnsServer)
        {
            try
            {
                _logger?.Info($"SysHelper.InitNetworkIpv4, serverAddr: {serverAddr}, ipv4Addr: {ipv4Addr}");
                _logger?.Info($"SysHelper.InitNetworkIpv4, ipv4Gateway: {ipv4Gateway}, ipv4NetMask: {ipv4NetMask}, tunMtu: {tunMtu}");
                _logger?.Info($"SysHelper.InitNetworkIpv4, ipv4MainDnsServer: {ipv4MainDnsServer}, ipv4SecondaryDnsServer: {ipv4SecondaryDnsServer}");
                if (_networkInited) return false;

                _serverAddr = serverAddr;
                _ipv4Addr = ipv4Addr;
                _ipv4Gateway = ipv4Gateway;
                _ipv4NetMask = ipv4NetMask;
                _tunIf = GetTunInterface();
                if (_tunIf is null) return false;
                _logger?.Info($"SysHelper.InitNetworkIpv4, _tunIf.Addr: {_tunIf.Value.Addr}");
                _curIf = GetCurInterface();
                if (_curIf is null) return false;
                _logger?.Info($"SysHelper.InitNetworkIpv4, _curIf.Addr: {_curIf.Value.Addr}");

                // set metric != 0
                Utils.Exec($"netsh interface ipv4 set route 0.0.0.0/0 {_curIf.Value.Index} metric=100 store=active");
                // setup tun
                Utils.Exec($"netsh interface ip set address {_tunIf.Value.Index} static {_ipv4Addr} {_ipv4NetMask}");
                Utils.Exec($"netsh interface ipv4 set interface {_tunIf.Value.Index} forwarding=enable metric=0 mtu={tunMtu}");
                // add server route
                Utils.Exec($"netsh interface ipv4 add route {_serverAddr}/32 {_curIf.Value.Index} {_curIf.Value.Gateway} metric=0");
                // setup dns server
                Utils.Exec($"netsh interface ip delete dns {_tunIf.Value.Index} all");
                Utils.Exec($"netsh interface ip set dns {_tunIf.Value.Index} static {ipv4MainDnsServer} validate=no");
                Utils.Exec($"netsh interface ip add dns {_tunIf.Value.Index} {ipv4SecondaryDnsServer} validate=no");
                // delete wins
                Utils.Exec($"netsh interface ip delete wins {_tunIf.Value.Index} all");

                _networkInited = true;
                return true;
            }
            catch (Exception ex)
            {
                _logger?.Error($"SysHelper.InitNetworkIpv4, error: {ex}");
                return false;
            }
        }

        public bool UninitNetwork()
        {
            try
            {
                _logger?.Info("SysHelper.UninitNetwork");
                if (!_networkInited) return false;

                if (_tunIf is null || _curIf is null || string.IsNullOrEmpty(_serverAddr)) return false;

                // recover interface metric
                Utils.Exec($"netsh interface ipv4 set interface {_tunIf.Value.Index} metric=256");
                // delete server route
                Utils.Exec($"netsh interface ipv4 delete route {_serverAddr}/32 {_curIf.Value.Index}");

                _networkInited = false;
                return true;
            }
            catch (Exception ex)
            {
                _logger?.Error($"SysHelper.UninitNetwork, error: {ex}");
                return false;
            }
        }

        // ip = 1.2.3.4, mask = 32
        public bool AddRouteWhiteIpv4(string ip, int mask)
        {
            try
            {
                _logger?.Info($"SysHelper.AddRouteWhiteIpv4, ip: {ip}, mask: {mask}");
                if (!_networkInited) return false;
                if (_tunIf is null || string.IsNullOrEmpty(_ipv4Gateway)) return false;
                Utils.Exec($"netsh interface ipv4 add route {ip}/{mask} {_tunIf.Value.Index} {_ipv4Gateway} metric=0");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.Error($"SysHelper.AddRouteWhiteIpv4, error: {ex}");
                return false;
            }
        }

        // ip = 1.2.3.4, mask = 32
        public bool DelRouteWhiteIpv4(string ip, int mask)
        {
            try
            {
                _logger?.Info($"SysHelper.DelRouteWhiteIpv4, ip: {ip}, mask: {mask}");
                if (!_networkInited) return false;
                if (_tunIf is null || string.IsNullOrEmpty(_ipv4Gateway)) return false;
                Utils.Exec($"netsh interface ipv4 delete route {ip}/{mask} {_tunIf.Value.Index} {_ipv4Gateway}");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.Error($"SysHelper.DelRouteWhiteIpv4, error: {ex}");
                return false;
            }
        }

        // ip = 1.2.3.4, mask = 32
        public bool AddRouteBlackIpv4(string ip, int mask)
        {
            try
            {
                _logger?.Info($"SysHelper.AddRouteBlackIpv4, ip: {ip}, mask: {mask}");
                if (!_networkInited) return false;
                if (_curIf is null) return false;
                Utils.Exec($"netsh interface ipv4 add route {ip}/{mask} {_curIf.Value.Index} {_curIf.Value.Gateway} metric=0");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.Error($"SysHelper.AddRouteBlackIpv4, error: {ex}");
                return false;
            }
        }

        // ip = 1.2.3.4, mask = 32
        public bool DelRouteBlackIpv4(string ip, int mask)
        {
            try
            {
                _logger?.Info($"SysHelper.DelRouteBlackIpv4, ip: {ip}, mask: {mask}");
                if (!_networkInited) return false;
                if (_curIf is null) return false;
                Utils.Exec($"netsh interface ipv4 delete route {ip}/{mask} {_curIf.Value.Index} {_curIf.Value.Gateway}");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.Error($"SysHelper.DelRouteBlackIpv4, error: {ex}");
                return false;
            }
        }

        public struct Interface
        {
            public string Guid { get; set; }
            public int Index { get; set; }
            public string Addr { get; set; }
            public string Mask { get; set; }
            public string Gateway { get; set; }

            public Interface Copy()
            {
                return new Interface
                {
                    Guid = Guid,
                    Index = Index,
                    Addr = Addr,
                    Mask = Mask,
                    Gateway = Gateway,
                };
            }
        }

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern int GetAdaptersInfo(IntPtr pAdapterInfo, ref int pOutBufLen);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct IP_ADAPTER_INFO
        {
            public IntPtr Next;
            public uint ComboIndex;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string AdapterName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 132)]
            public string Description;
            public uint AddressLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[] Address;
            public uint Index;
            public uint Type;
            public uint DhcpEnabled;
            public IntPtr CurrentIpAddress;
            public IP_ADDR_STRING IpAddressList;
            public IP_ADDR_STRING GatewayList;
            public IP_ADDR_STRING DhcpServer;
            public bool HaveWins;
            public IP_ADDR_STRING PrimaryWinsServer;
            public IP_ADDR_STRING SecondaryWinsServer;
            public uint LeaseObtained;
            public uint LeaseExpires;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct IP_ADDR_STRING
        {
            public IntPtr Next;
            public IP_ADDRESS_STRING IpAddress;
            public IP_ADDRESS_STRING IpMask;
            public uint Context;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct IP_ADDRESS_STRING
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
            public string Address;
        }

    }
}
