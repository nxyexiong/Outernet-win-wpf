using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Outernet
{
    public class Utils
    {
        private static readonly NLog.Logger _logger = Logger.IsLoggerInited() ? LogManager.GetCurrentClassLogger() : null;
        public static string Ipv4ToStr(uint ipv4, bool isBigEnd)
        {
            var ipAddressBytes = BitConverter.GetBytes(ipv4);
            if (!isBigEnd) Array.Reverse(ipAddressBytes);
            IPAddress ipAddress = new IPAddress(ipAddressBytes);
            return ipAddress.ToString();
        }

        public static void Exec(string cmd)
        {
            _logger?.Info($"Utils.Exec, cmd: {cmd}");
            var process = new Process();
            process.StartInfo.FileName = "cmd.exe";
            process.StartInfo.Arguments = "/c " + cmd;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.Start();

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            _logger?.Info($"Utils.Exec, output: {output}");
            _logger?.Info($"Utils.Exec, error: {error}");
        }

        // returns null if failed
        public static byte[] Sha256(byte[] bytes)
        {
            byte[] ret = null;
            using (SHA256 sha256 = SHA256.Create())
                ret = sha256.ComputeHash(bytes);
            return ret;
        }
    }

    public class SingleThreadTaskScheduler : TaskScheduler
    {
        private readonly Thread executionThread;
        private readonly BlockingCollection<Task> taskQueue = new BlockingCollection<Task>();
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        public SingleThreadTaskScheduler()
        {
            executionThread = new Thread(ExecuteTasks);
            executionThread.Start();
            Application.Current.Exit += ApplicationExitHandler;
        }

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            return taskQueue.ToArray();
        }

        protected override void QueueTask(Task task)
        {
            taskQueue.Add(task);
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            return executionThread == Thread.CurrentThread && TryExecuteTask(task);
        }

        private void ExecuteTasks()
        {
            try
            {
                foreach (var task in taskQueue.GetConsumingEnumerable(cancellationTokenSource.Token))
                {
                    TryExecuteTask(task);
                }
            }
            catch (OperationCanceledException)
            {
                // The operation was canceled, so exit gracefully.
            }
        }

        private void ApplicationExitHandler(object sender, ExitEventArgs e)
        {
            Shutdown();
        }

        private void Shutdown()
        {
            cancellationTokenSource.Cancel();
            executionThread.Join();
            taskQueue.CompleteAdding();
            Application.Current.Exit -= ApplicationExitHandler;
        }
    }
}
