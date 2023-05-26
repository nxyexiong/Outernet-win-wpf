using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Outernet
{
    public class Tun
    {
        private static readonly NLog.Logger _logger = Logger.IsLoggerInited() ? LogManager.GetCurrentClassLogger() : null;
        private IntPtr _adapter;
        private IntPtr _session;

        public Tun()
        {
            _adapter = IntPtr.Zero;
            _session = IntPtr.Zero;
        }

        public bool Init()
        {
            try
            {
                if (_adapter != IntPtr.Zero) return true;

                _adapter = WintunOpenAdapter("Outernet");
                if (_adapter == IntPtr.Zero)
                {
                    var guid = new Guid("c55970ca-5f6d-443d-8e50-2862939b5b0f");
                    _adapter = WintunCreateAdapter("Outernet", "Outernet", ref guid);
                    if (_adapter == IntPtr.Zero)
                    {
                        int error = Marshal.GetLastWin32Error();
                        _logger?.Error($"Tun.Init, WintunCreateAdapter failed: {error}");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger?.Error($"Tun.Init, error: {ex}");
                return false;
            }
        }

        public bool Uninit()
        {
            try
            {
                if (_session != IntPtr.Zero)
                {
                    if (!CloseSession())
                    {
                        _logger?.Error("Tun.Uninit, CloseSession failed, return false");
                        return false;
                    }
                }

                if (_adapter != IntPtr.Zero)
                {
                    WintunCloseAdapter(_adapter);
                    _adapter = IntPtr.Zero;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger?.Error($"Tun.Uninit, error: {ex}");
                return false;
            }
        }

        public bool StartSession()
        {
            try
            {
                if (_adapter == IntPtr.Zero)
                {
                    _logger?.Error($"Tun.StartSession, _adapter is null, return false");
                    return false;
                }

                _session = WintunStartSession(_adapter, 0x400000);
                if (_session == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    _logger?.Error($"Tun.StartSession, WintunStartSession failed: {error}");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger?.Error($"Tun.StartSession, error: {ex}");
                return false;
            }
        }

        public bool CloseSession()
        {
            try
            {
                if (_session != IntPtr.Zero)
                {
                    WintunEndSession(_session);
                    int error = Marshal.GetLastWin32Error();
                    if (error != 0)
                    {
                        _logger?.Error($"Tun.CloseSession, WintunEndSession failed: {error}");
                        return false;
                    }
                    _session = IntPtr.Zero;
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger?.Error($"Tun.CloseSession, error: {ex}");
                return false;
            }
        }

        public WritePacketResult WritePacket(OtBuffer buf)
        {
            return WritePacket(buf.GetBuf(), buf.GetLen());
        }

        public WritePacketResult WritePacket(byte[] bytes, int len)
        {
            try
            {
                if (_session == IntPtr.Zero)
                {
                    _logger?.Error($"Tun.WritePacket, session is null, return false");
                    return WritePacketResult.Error;
                }
                var packet = WintunAllocateSendPacket(_session, (uint)len);
                if (packet == IntPtr.Zero)
                {
                    var error = Marshal.GetLastWin32Error();
                    if (error == 111)
                    {
                        return WritePacketResult.Overflow;
                    }
                    else
                    {
                        _logger?.Error($"Tun.WritePacket, WintunAllocateSendPacket failed: {error}");
                        return WritePacketResult.Error;
                    }
                }
                Marshal.Copy(bytes, 0, packet, len);
                WintunSendPacket(_session, packet);
                return WritePacketResult.Ok;
            }
            catch (Exception ex)
            {
                _logger?.Error($"Tun.WritePacket, error: {ex}");
                return WritePacketResult.Error;
            }
        }

        // return null if failed
        public ReadPacketResult ReadPacket(out byte[] bytes)
        {
            try
            {
                bytes = null;
                if (_session == IntPtr.Zero)
                {
                    // too many logs
                    //_logger?.Error($"Tun.ReadPacket, session is null, return false");
                    return ReadPacketResult.Error;
                }
                var packet = WintunReceivePacket(_session, out uint packetSize);
                if (packet == IntPtr.Zero)
                {
                    var error = Marshal.GetLastWin32Error();
                    if (error == 259)
                    {
                        return ReadPacketResult.NoData;
                    }
                    else
                    {
                        _logger?.Error($"Tun.ReadPacket, WintunReceivePacket failed: {error}");
                        return ReadPacketResult.Error;
                    }
                }
                bytes = new byte[packetSize];
                Marshal.Copy(packet, bytes, 0, (int)packetSize);
                WintunReleaseReceivePacket(_session, packet);
                return ReadPacketResult.Ok;
            }
            catch (Exception ex)
            {
                _logger?.Error($"Tun.ReadPacket, error: {ex}");
                bytes = null;
                return ReadPacketResult.Error;
            }
        }

        public enum WritePacketResult
        {
            Ok,
            Overflow,
            Error,
        }

        public enum ReadPacketResult
        {
            Ok,
            NoData,
            Error,
        }

        [DllImport("wintun.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr WintunCreateAdapter(string name, string tunnelType, ref Guid requestedGuid);

        [DllImport("wintun.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr WintunOpenAdapter(string name);

        [DllImport("wintun.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern void WintunCloseAdapter(IntPtr adapter);

        [DllImport("wintun.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr WintunStartSession(IntPtr adapter, uint capacity);

        [DllImport("wintun.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern void WintunEndSession(IntPtr session);

        [DllImport("wintun.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr WintunAllocateSendPacket(IntPtr session, uint packetSize);

        [DllImport("wintun.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern void WintunSendPacket(IntPtr sessionHandle, IntPtr packet);

        [DllImport("wintun.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr WintunReceivePacket(IntPtr session, out uint packetSize);

        [DllImport("wintun.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern void WintunReleaseReceivePacket(IntPtr session, IntPtr packet);
    }
}
