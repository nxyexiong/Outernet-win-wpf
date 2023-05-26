using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;

namespace Outernet
{
    public class Configs
    {
        private const string _configsFileName = "configs.json";
        private static readonly NLog.Logger _logger = Logger.IsLoggerInited() ? LogManager.GetCurrentClassLogger() : null;

        public string ServerIp { get; set; } = string.Empty;
        public int ServerPort { get; set; } = 0;
        public string Username { get; set; } = string.Empty;
        public string Secret { get; set; } = string.Empty;


        public static void SaveConfigs(Configs configs)
        {
            try
            {
                _logger?.Info("Configs.SaveConfigs");
                var jsonStr = JsonConvert.SerializeObject(configs);
                File.WriteAllText(_configsFileName, jsonStr);
            }
            catch (Exception ex)
            {
                _logger?.Error($"Configs.SaveConfigs, error: {ex}");
            }
        }

        public static Configs LoadConfigs()
        {
            try
            {
                _logger?.Info("Configs.LoadConfigs");
                var jsonStr = File.ReadAllText(_configsFileName);
                return JsonConvert.DeserializeObject<Configs>(jsonStr);
            }
            catch (Exception ex)
            {
                _logger?.Error($"Configs.LoadConfigs, error: {ex}");
                return new Configs();
            }
        }
    }
}
