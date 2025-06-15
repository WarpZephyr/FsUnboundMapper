using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace FsUnboundMapper
{
    internal class AppInfo
    {
        public readonly string Platform;
        public readonly string Version;
#if DEBUG
        public const bool IsDebug = true;
#else
        public const bool IsDebug = false;
#endif
        public const string AppName = Program.AppName;
        public readonly string AppFilePath;
        public readonly string AppDirectory;

        public AppInfo()
        {
            Platform = GetPlatform();
            Version = GetVersion();
            AppFilePath = Environment.ProcessPath ?? "Unknown";
            AppDirectory = AppDomain.CurrentDomain.BaseDirectory;
        }

        private static string GetVersion()
        {
            Assembly executingAssembly = Assembly.GetExecutingAssembly();
            var version = executingAssembly.GetName().Version;
            if (version != null)
            {
                return version.ToString();
            }
            else
            {
                return "0.0.0.0";
            }
        }

        private static string GetPlatform()
        {
            string platform;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                platform = "Linux";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
            {
                platform = "FreeBSD";
            }
            else
            {
                return Environment.OSVersion.ToString();
            }

            var osVersion = Environment.OSVersion;
            string servicePack = osVersion.ServicePack;
            return string.IsNullOrEmpty(servicePack) ?
               $"{platform} {osVersion.Version}" :
               $"{platform} {osVersion.Version.ToString(3)} {servicePack}";
        }
    }
}
