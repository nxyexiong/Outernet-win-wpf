using NLog;
using NLog.Config;
using NLog.Targets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Outernet
{
    public class Logger
    {
        private static bool s_isLoggerInited = false;
        private static readonly object s_isLoggerInitedLock = new object();

        public static bool IsLoggerInited()
        {
            lock (s_isLoggerInitedLock)
            {
                if (!s_isLoggerInited)
                {
                    s_isLoggerInited = true;
                    InitLogger();
                }
            }
            return true;
        }

        private static void InitLogger()
        {
            var config = new LoggingConfiguration();
            var fileTarget = new FileTarget("file")
            {
                FileName = "${processname}.log",
                ArchiveFileName = "${processname}_{#}.log",
                ArchiveEvery = FileArchivePeriod.Day,
                ArchiveNumbering = ArchiveNumberingMode.Rolling,
                MaxArchiveFiles = 7,
                Layout = "${longdate} ${level:uppercase=true} ${message}"
            };
            config.AddTarget(fileTarget);
#if DEBUG
            var minLevel = LogLevel.Debug;
#else
            var minLevel = LogLevel.Info;
#endif
            var fileRule = new LoggingRule("*", minLevel, fileTarget);
            config.LoggingRules.Add(fileRule);
            LogManager.Configuration = config;
        }
    }
}
