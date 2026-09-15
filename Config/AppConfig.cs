using System;
using System.IO;
using System.Text.Json;

namespace TechnoVerseLoader.Config
{
    public class AppConfig
    {
        public const string DefaultProductionUrl = "https://technoverse-backend-production.up.railway.app";
        public string ServerUrl { get => DefaultProductionUrl; set { } }
        public string SavedKey { get; set; } = "";
        public string SavedDiscordId { get; set; } = "";
        public string SavedUsername { get; set; } = "";
        public bool RememberKey { get; set; } = true;
        public bool AutoCloseOnLaunch { get; set; } = false;
        public string SecurityWebhookUrl { get; set; } = "https://discord.com/api/webhooks/1548193737673416755/3Rv-ei09agoUneJSpW5McGxQj9l_vrV6i50zH9pfY7EnyxvPPYvgEHx_SNMM5el5XgD6";

        private static readonly string ConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TechnoVerse"
        );

        private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

        public static string GetPayloadsDirectory()
        {
            string dir = Path.Combine(ConfigDir, "Payloads");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return dir;
        }

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    var conf = JsonSerializer.Deserialize<AppConfig>(json);
                    if (conf != null)
                    {
                        if (string.IsNullOrWhiteSpace(conf.ServerUrl) ||
                            conf.ServerUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
                            conf.ServerUrl.Contains("127.0.0.1"))
                        {
                            conf.ServerUrl = DefaultProductionUrl;
                            conf.Save();
                        }
                        return conf;
                    }
                }
            }
            catch
            {
                // Fallback to default
            }

            var freshConfig = new AppConfig();
            freshConfig.Save();
            return freshConfig;
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(ConfigDir))
                {
                    Directory.CreateDirectory(ConfigDir);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(ConfigPath, json);
            }
            catch
            {
                // Ignore write errors
            }
        }
    }
}
