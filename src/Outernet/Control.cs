using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace Outernet
{
    public class Control
    {
        public delegate void OnStateChangedDelegate(ControlState state, string msg);

        private static readonly NLog.Logger _logger = Logger.IsLoggerInited() ? LogManager.GetCurrentClassLogger() : null;
        private const string _networkMask = "255.255.255.0";
        private const int _tunMtu = 1400;
        private const string _mainDns = "1.1.1.1";
        private const string _secDns = "8.8.8.8";

        private SingleThreadTaskScheduler _syncTaskScheduler;
        private readonly OnStateChangedDelegate _onStateChangedDelegate;
        private Task _background_task;
        private bool _shouldStop;
        private Configs _configs;

        public Control(OnStateChangedDelegate onStateChangedDelegate)
        {
            _syncTaskScheduler = new SingleThreadTaskScheduler();
            _onStateChangedDelegate = onStateChangedDelegate;
            _background_task = null;
            _shouldStop = false;
            _configs = null;

            // initial state
            onStateChangedDelegate.Invoke(ControlState.Disconnected, string.Empty);
        }

        public void Start(Configs configs)
        {
            _logger?.Info("Control.Start");
            Task.Factory.StartNew(() =>
            {
                if (_background_task != null) return;
                _configs = configs;
                _shouldStop = false;
                _background_task = Task.Run(() => Loop());
            }, CancellationToken.None, TaskCreationOptions.None, _syncTaskScheduler);
        }

        public void Stop()
        {
            _logger?.Info("Control.Stop");
            Task.Factory.StartNew(() =>
            {
                if (_background_task == null) return;
                _shouldStop = true;
                Task.WaitAll(_background_task);
                _background_task = null;
            }, CancellationToken.None, TaskCreationOptions.None, _syncTaskScheduler);
        }

        public void Loop()
        {
            _logger?.Info("Control.Loop");

            var state = ControlState.Disconnected;
            var tun = new Tun();
            var client = new Client();
            var sysHelper = new SysHelper();

            var tunInited = false;
            var clientInited = false;
            var clientRunning = false;
            var sysHelperInited = false;
            var tunSessionStarted = false;
            var sysHelperRouteAdded = false;

            do
            {
                _logger?.Info("Control.Loop, start to connect");

                // change state to connecting
                state = ControlState.Connecting;
                _onStateChangedDelegate(state, string.Empty);

                // init tun
                if (!tun.Init())
                {
                    _logger?.Error("Control.Loop, tun init failed");
                    state = ControlState.Error;
                    _onStateChangedDelegate(state, "tun init failed");
                    break;
                }
                tunInited = true;

                // init client
                if (!client.InitIpv4(_configs.ServerIp, _configs.ServerPort, _configs.Username, _configs.Secret, tun))
                {
                    _logger?.Error("Control.Loop, client init error");
                    state = ControlState.Error;
                    _onStateChangedDelegate(state, "client init error");
                    break;
                }
                clientInited = true;

                // run client
                if (!client.Run())
                {
                    _logger?.Error("Control.Loop, client run error");
                    state = ControlState.Error;
                    _onStateChangedDelegate(state, "client run error");
                    break;
                }
                clientRunning = true;

                // wait for handshake
                while (client.IsRunning() && !client.IsHandshaked()) Thread.Sleep(100);
                if (client.IsHandshaked()) // handshake success
                {
                    _logger?.Info("Control.Loop, client handshake success, setting up");

                    // change state to setting up
                    state = ControlState.SettingUp;
                    _onStateChangedDelegate(state, string.Empty);

                    // setup system network
                    var tunIp = client.GetTunIp();
                    var dstIp = client.GetDstIp();
                    if (!sysHelper.InitNetworkIpv4(_configs.ServerIp, dstIp, tunIp, _networkMask, _tunMtu, _mainDns, _secDns))
                    {
                        _logger?.Error("Control.Loop, syshelper init network ipv4 failed");
                        state = ControlState.Error;
                        _onStateChangedDelegate(state, "syshelper init network ipv4 failed");
                        break;
                    }
                    sysHelperInited = true;

                    // start tun session
                    if (!tun.StartSession())
                    {
                        _logger?.Error("Control.Loop, tun session start failed");
                        state = ControlState.Error;
                        _onStateChangedDelegate(state, "tun session start failed");
                        break;
                    }
                    tunSessionStarted = true;
                }
                else // timeout
                {
                    _logger?.Info("Control.Loop, client handshake timeout");

                    state = ControlState.Error;
                    _onStateChangedDelegate(state, "client connect timeout");
                    break;
                }

                _logger?.Info("Control.Loop, start to add route");

                // change state to adding route
                state = ControlState.AddingRoute;
                _onStateChangedDelegate(state, string.Empty);

                // add route
                if (!sysHelper.AddRouteWhiteIpv4("0.0.0.0", 0))
                {
                    _logger?.Error("Control.Loop, add route failed");
                    state = ControlState.Error;
                    _onStateChangedDelegate(state, "add route failed");
                    break;
                }
                sysHelperRouteAdded = true;

                _logger?.Info("Control.Loop, connected");

                // change state to connected
                state = ControlState.Connected;
                _onStateChangedDelegate(state, string.Empty);

                // wait for stop
                while (!_shouldStop) Thread.Sleep(100);
            }
            while (false);

            _logger?.Info("Control.Loop, start to disconnect");

            // change state to disconnecting
            state = ControlState.Disconnecting;
            _onStateChangedDelegate(state, string.Empty);

            // uninit sequence
            if (sysHelperRouteAdded)
                sysHelper.DelRouteWhiteIpv4("0.0.0.0", 0);
            if (sysHelperInited)
                sysHelper.UninitNetwork();
            if (clientRunning)
                client.Stop();
            if (clientInited)
                client.Uninit();
            if (tunSessionStarted)
                tun.CloseSession();
            if (tunInited)
                tun.Uninit();

            _logger?.Info("Control.Loop, disconnected");

            // change state to disconnected
            state = ControlState.Disconnected;
            _onStateChangedDelegate(state, string.Empty);

            Stop(); // delete background task in case of error
        }

        public enum ControlState
        {
            Disconnected,
            Connected,
            Connecting,
            SettingUp,
            AddingRoute,
            Disconnecting,
            Error,
        }
    }
}
