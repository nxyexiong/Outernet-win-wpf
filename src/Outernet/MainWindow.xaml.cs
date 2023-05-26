using MahApps.Metro.Controls;
using NLog;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

// TODO:
// multi-config
// multi-architecture
// data monitor and persistence

namespace Outernet
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private static readonly NLog.Logger _logger = Logger.IsLoggerInited() ? LogManager.GetCurrentClassLogger() : null;
        private Control.ControlState _state;
        private Control _control;

        public MainWindow()
        {
            InitializeComponent();
            var version = Assembly.GetEntryAssembly().GetName().Version;
            Title = $"Outernet - {version.Major}.{version.Minor}";

            // get Control object
            _control = new Control((state, msg) => OnStateChanged(state, msg));

            // load configs
            var configs = Configs.LoadConfigs();
            ServerAddrTextBox.Text = configs.ServerIp;
            ServerPortTextBox.Text = configs.ServerPort == 0 ? string.Empty : configs.ServerPort.ToString();
            UsernameTextBox.Text = configs.Username;
            SecretTextBox.Text = configs.Secret;
        }

        private void OnStateChanged(Control.ControlState state, string msg)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (!string.IsNullOrEmpty(msg))
                    MessageBox.Show(msg);

                _state = state;
                MainButton.Content = ControlStateToDisplayStr(_state);
                MainButton.IsEnabled = _state == Control.ControlState.Disconnected || _state == Control.ControlState.Connected;
                IsCloseButtonEnabled = _state == Control.ControlState.Disconnected;
            });
        }

        private void OnMainButtonClicked(object sender, RoutedEventArgs e)
        {
            // build configs
            var configs = new Configs();
            if (!int.TryParse(ServerPortTextBox.Text, out var serverPort))
            {
                MessageBox.Show("invalid port");
                return;
            }
            configs.ServerIp = ServerAddrTextBox.Text;
            configs.ServerPort = serverPort;
            configs.Username = UsernameTextBox.Text;
            configs.Secret = SecretTextBox.Text;

            // call control
            if (_state == Control.ControlState.Disconnected)
            {
                Configs.SaveConfigs(configs);
                _control.Start(configs);
            }
            else if (_state == Control.ControlState.Connected)
            {
                _control.Stop();
            }
        }

        private static string ControlStateToDisplayStr(Control.ControlState state)
        {
            if (state == Control.ControlState.Disconnected)
                return "Connect";
            else if (state == Control.ControlState.Connected)
                return "Disconnect";
            else if (state == Control.ControlState.Connecting)
                return "Connecting";
            else if (state == Control.ControlState.SettingUp)
                return "Setting up";
            else if (state == Control.ControlState.AddingRoute)
                return "Adding route";
            else if (state == Control.ControlState.Disconnecting)
                return "Disconnecting";
            return "Error";
        }
    }
}
