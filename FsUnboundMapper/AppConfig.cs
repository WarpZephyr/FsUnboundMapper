using FsUnboundMapper.Logging;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FsUnboundMapper
{
    [JsonSourceGenerationOptions(WriteIndented = true,
        GenerationMode = JsonSourceGenerationMode.Metadata,
        IncludeFields = true,
        UseStringEnumConverter = true)]
    [JsonSerializable(typeof(AppConfig))]
    internal partial class AppConfigSerializerContext : JsonSerializerContext
    {
    }

    internal class AppConfig
    {
        #region Static Members

        [JsonIgnore]
        private const string FileName = "config.json";

        [JsonIgnore]
        private static readonly string FolderPath = Program.AppDataFolder;

        [JsonIgnore]
        private static readonly string DataPath = Path.Combine(FolderPath, FileName);

        [JsonIgnore]
        private const int DefaultConfigVersion = 0;

        [JsonIgnore]
        private const int CurrentConfigVersion = 1;

        #endregion

        #region Instance Members

        [JsonIgnore]
        internal static AppConfig Instance { get; private set; } = Load();

        #endregion

        #region Settings

        public int ConfigVersion;
        public GameType ManualGameOverride;
        public PlatformType ManualPlatformOverride;
        public bool SkipUnknownFiles;
        public bool LowercaseFileNames;
        public bool HidePackedFiles;
        public bool ApplyFmodCrashFix;

        #endregion

        public AppConfig()
        {
            ConfigVersion = CurrentConfigVersion;
            ManualGameOverride = GameType.ArmoredCoreVerdictDay;
            ManualPlatformOverride = PlatformType.PlayStation3;
            SkipUnknownFiles = false;
            LowercaseFileNames = true;
            HidePackedFiles = true;
            ApplyFmodCrashFix = true;
        }

        #region IO

        public static AppConfig Load()
        {
            AppConfig config;
            if (!File.Exists(DataPath))
            {
                Log.WriteLine($"Making default app config due to it being missing from expected path: \"{DataPath}\"");
                config = new AppConfig();

                Log.WriteLine("Saving default app config to expected path.");
                config.Save();
            }
            else
            {
                try
                {
                    var options = new JsonSerializerOptions();
                    config = JsonSerializer.Deserialize(File.ReadAllText(DataPath),
                        AppConfigSerializerContext.Default.AppConfig) ?? throw new Exception("JsonConvert returned null when loading config.");

                    // Update config file
                    if (config.ConfigVersion < CurrentConfigVersion)
                    {
                        Log.WriteLine("Replacing outdated config file.");
                        config.ConfigVersion = CurrentConfigVersion;
                        config.Save();
                    }
                }
                catch (Exception ex)
                {
                    Log.WriteLine($"Failed to load app config from expected path \"{DataPath}\": {ex}");
                    Log.WriteLine("Making default app config due to failure to load it from expected path.");
                    config = new AppConfig();

                    Log.WriteLine("Saving default app config to expected path.");
                    config.Save();
                }
            }

            return config;
        }

        public void Save()
        {
            var json = JsonSerializer.Serialize(this, AppConfigSerializerContext.Default.AppConfig);

            try
            {
                Directory.CreateDirectory(FolderPath);
                File.WriteAllText(DataPath, json);
            }
            catch (Exception ex)
            {
                Log.WriteLine($"Failed to save app config to path \"{DataPath}\": {ex}");
            }
        }

        #endregion
    }
}
