using System;
using System.IO;

namespace FsUnboundMapper.Binder
{
    internal static class BinderKeys
    {
        public static string GetName(GameType game, PlatformType platform)
        {
            string gameStr;
            switch (game)
            {
                case GameType.ArmoredCoreV:
                    gameStr = "ArmoredCore5";
                    break;
                case GameType.ArmoredCoreVerdictDay:
                    gameStr = "ArmoredCoreVerdictDay";
                    break;
                default:
                    throw new NotSupportedException($"{nameof(GameType)} {game} is currently not supported in method: {nameof(GetName)}");
            }

            string platformStr;
            switch (platform)
            {
                case PlatformType.PlayStation3:
                    platformStr = "PS3";
                    break;
                case PlatformType.Xbox360:
                    platformStr = "X360";
                    break;
                default:
                    throw new NotSupportedException($"{nameof(PlatformType)} {platform} is currently not supported in method: {nameof(GetName)}");
            }

            return $"{gameStr}_{platformStr}";
        }

        public static string GetAssetDirectory(GameType game, PlatformType platform)
            => Path.Combine(Program.AppDataFolder, "Assets", "BinderKeys", GetName(game, platform));
    }
}
