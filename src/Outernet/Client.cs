using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Outernet
{
    public class Client
    {
        private const int _bufSize = 2048;
        private const int _connectRetryTimes = 5;

        private static readonly NLog.Logger _logger = Logger.IsLoggerInited() ? LogManager.GetCurrentClassLogger() : null;
        private Task _loopTask = null;
        private bool _inited = false;
        private bool _running = false;
        private byte[] _identification = null;
        private Tun _tun = null;
        private Crypto _crypto = null;
        private Socket _sock = null;
        private IPEndPoint _serverEndPoint = null;
        private bool _handshaked = false;
        private DateTime _handshakeSentTime = DateTime.MinValue;
        private int _handshakeRetryCnt = 0;
        private string _tunIp = null;
        private string _dstIp = null;

        public bool InitIpv4(string ip, int port, string username, string secret, Tun tun)
        {
            try
            {
                if (_inited)
                {
                    _logger?.Error("Client.Init, already inited, return false");
                    return false;
                }

                _tun = tun;

                _crypto = new Crypto(secret);
                _identification = Utils.Sha256(Encoding.ASCII.GetBytes(username));

                var ipAddr = IPAddress.Parse(ip);
                _serverEndPoint = new IPEndPoint(ipAddr, port);
                var localEndPoint = new IPEndPoint(IPAddress.Any, 0);
                _sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                _sock.Bind(localEndPoint);
                _sock.Blocking = false;

                _inited = true;
                return true;
            }
            catch (Exception ex)
            {
                _logger?.Error($"Client.InitIpv4, exception: {ex}");
                return false;
            }
        }

        public void Uninit()
        {
            try
            {
                if (!_inited) return;

                Stop();

                _tun = null;
                _crypto = null;
                _serverEndPoint = null;

                if (_sock != null)
                {
                    _sock.Close();
                    _sock = null;
                }

                _inited = false;
            }
            catch (Exception ex)
            {
                _logger?.Error($"Client.Uninit, exception: {ex}");
            }
        }

        public bool Run()
        {
            try
            {
                if (!_inited)
                {
                    _logger?.Error("Client.Run, not inited yet, return false");
                    return false;
                }

                if (_running)
                {
                    _logger?.Error("Client.Run, already running, return false");
                    return false;
                }

                _running = true;
                _loopTask = Task.Run(() => Loop());

                return true;
            }
            catch (Exception ex)
            {
                _logger?.Error($"Client.Run, exception: {ex}");
                return false;
            }
        }

        public void Stop()
        {
            try
            {
                if (!_running) return;

                _handshaked = false;
                _running = false;
                _loopTask.Wait();
                _loopTask = null;
            }
            catch (Exception ex)
            {
                _logger?.Error($"Client.Stop, exception: {ex}");
            }
        }

        public void Loop()
        {
            try
            {
                while (_running)
                {
                    if (!_handshaked) HandleHandshake();
                    var hasData = false;
                    if (HandleRead()) hasData = true;
                    if (HandleRecv()) hasData = true;
                    if (!hasData) Thread.Sleep(1);
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"Client.Loop, exception: {ex}");
            }
        }

        public bool IsInited() => _inited;

        public bool IsRunning() => _running;

        public bool IsHandshaked() => _handshaked;

        public string GetTunIp() => _tunIp;

        public string GetDstIp() => _dstIp;

        private bool HandleRecv()
        {
            try
            {
                OtBuffer buf;
                var bytes = new byte[_bufSize];
                var receiveEndPoint = (EndPoint)new IPEndPoint(IPAddress.Any, 0);
                if (_sock.Poll(0, SelectMode.SelectRead))
                {
                    var bytesRead = _sock.ReceiveFrom(bytes, ref receiveEndPoint);
                    var raw = new OtBuffer();
                    raw.Copy(bytes, bytesRead);
                    buf = UnwrapData(raw);
                    if (buf == null)
                    {
                        _logger?.Error("Client.HandleRecv, UnwrapData failed, return false");
                        return false;
                    }
                }
                else
                {
                    return false;
                }

                var protocol = new Protocol();
                var parsed = protocol.ParseHeader(buf);
                if (!_handshaked)
                {
                    if (!protocol.Complete || protocol.Cmd != Protocol.CmdType.ServerHandshake)
                    {
                        return true;
                    }
                    _tunIp = Utils.Ipv4ToStr(protocol.TunIp, true);
                    _dstIp = Utils.Ipv4ToStr(protocol.DstIp, true);
                    _handshaked = true;
                    return true;
                }
                else
                {
                    if (!protocol.Complete || protocol.Cmd != Protocol.CmdType.ServerData)
                    {
                        return true;
                    }
                    if (_tun == null)
                    {
                        return false;
                    }
                    buf.RemoveFront(parsed);
                    _tun.WritePacket(buf);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"Client.HandleRecv, exception: {ex}");
                return false;
            }
        }

        private bool HandleRead()
        {
            try
            {
                var protocol = new Protocol();
                protocol.Cmd = Protocol.CmdType.ClientData;
                Array.Copy(_identification, 0, protocol.Identification, 0, 32);
                var buf = protocol.GetHeaderBytes();

                if (_tun == null)
                {
                    _logger?.Error("Client.HandleRead, tun is null, return false;");
                    return false;
                }

                var ret = _tun.ReadPacket(out var bytes);
                if (ret == Tun.ReadPacketResult.Ok)
                {
                    buf.InsertBack(bytes, bytes.Length);
                    var wrapped = WrapData(buf);
                    if (wrapped == null)
                    {
                        _logger?.Error("Client.HandleRead, WrapData failed, return false");
                        return false;
                    }
                    var toSend = new byte[wrapped.GetLen()];
                    Array.Copy(wrapped.GetBuf(), 0, toSend, 0, wrapped.GetLen());
                    _sock.SendTo(toSend, _serverEndPoint);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger?.Error($"Client.HandleRead, exception: {ex}");
                return false;
            }
        }

        private void HandleHandshake()
        {
            try
            {
                var now = DateTime.Now;
                if (now - _handshakeSentTime < TimeSpan.FromSeconds(1)) return;
                _handshakeSentTime = now;

                if (_handshakeRetryCnt >= _connectRetryTimes)
                {
                    _running = false;
                    return;
                }
                _handshakeRetryCnt++;

                // start handshake
                var protocol = new Protocol();
                protocol.Cmd = Protocol.CmdType.ClientHandshake;
                Array.Copy(_identification, 0, protocol.Identification, 0, 32);
                var buf = protocol.GetHeaderBytes();

                var wrapped = WrapData(buf);
                if (wrapped == null)
                {
                    _logger?.Error("Client.HandleHandshake, WrapData failed, return");
                    return;
                }
                var toSend = new byte[wrapped.GetLen()];
                Array.Copy(wrapped.GetBuf(), 0, toSend, 0, wrapped.GetLen());
                _sock.SendTo(toSend, _serverEndPoint);
            }
            catch (Exception ex)
            {
                _logger?.Error($"Client.HandleHandshake, exception: {ex}");
            }
        }

        // return null if failed
        private OtBuffer WrapData(OtBuffer input)
        {
            try
            {
                return _crypto.Encrypt(input);
            }
            catch (Exception ex)
            {
                _logger?.Error($"Client.WrapData, exception: {ex}");
                return null;
            }
        }

        // return null if failed
        private OtBuffer UnwrapData(OtBuffer input)
        {
            try
            {
                return _crypto.Decrypt(input);
            }
            catch (Exception ex)
            {
                _logger?.Error($"Client.UnwrapData, exception: {ex}");
                return null;
            }
        }
    }
}
