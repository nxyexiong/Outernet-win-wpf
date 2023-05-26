using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Outernet
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
        }

        private void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            Exception exception = e.ExceptionObject as Exception;
            LogExceptionToFile(exception);
        }

        private void LogExceptionToFile(Exception exception)
        {
            try
            {
                using (StreamWriter writer = File.AppendText("error.log"))
                {
                    writer.WriteLine($"[{DateTime.Now}] Unhandled Exception:");
                    writer.WriteLine(exception.ToString());
                    writer.WriteLine(new string('-', 50));
                }
            }
            catch { }
        }
    }
}
