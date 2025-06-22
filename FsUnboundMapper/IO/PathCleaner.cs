using System;
using System.IO;

namespace FsUnboundMapper.IO
{
    internal static class PathCleaner
    {
        public static string CreatePath(string baseDir, string name)
        {
            string outPath = Path.Combine(baseDir, name);
            string? outDir = Path.GetDirectoryName(outPath);
            if (string.IsNullOrEmpty(outDir))
                throw new Exception($"Could not get folder name of built output path: {outPath}");

            Directory.CreateDirectory(outDir);
            return outPath;
        }

        public static string CleanComponentPath(string path)
        {
            path = RemovePathRoot(path);
            path = NormalizePathSlashes(path);
            path = SetPathCasing(path);
            return path;
        }

        public static string SetPathCasing(string path)
        {
            if (AppConfig.Instance.LowercaseFileNames)
            {
                path = path.ToLowerInvariant();
            }

            return path;
        }

        public static string RemovePathRoot(string path)
        {
            int rootIndex = path.IndexOf(':');
            if (rootIndex > -1)
                path = path[rootIndex..];

            return path;
        }

        public static string NormalizePathSlashes(string path)
        {
            path = path.Replace('\\', Path.DirectorySeparatorChar);
            path = path.Replace('/', Path.DirectorySeparatorChar);
            path = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            path = path.TrimStart('\\');
            return path;
        }
    }
}
