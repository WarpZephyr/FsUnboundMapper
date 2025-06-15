using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace FsUnboundMapper.Exceptions
{
    public class PathException : Exception
    {
        public PathException() { }
        public PathException(string message) : base(message) { }
        public PathException(string message, Exception inner) : base(message, inner) { }

        public static void ThrowIfNotFile([NotNull] string? filePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            if (!File.Exists(filePath))
            {
                throw new PathException($"A file did not exist at: {filePath}");
            }
        }

        public static void ThrowIfNotDirectory([NotNull] string? directoryPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
            if (!Directory.Exists(directoryPath))
            {
                throw new PathException($"A directory did not exist at: {directoryPath}");
            }
        }

        public static void ThrowIfNotFileOrDirectory([NotNull] string? path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                throw new PathException($"Neither a file nor directory exists at: {path}");
            }
        }
    }
}
