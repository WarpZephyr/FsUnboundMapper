using System.Runtime.CompilerServices;

namespace FsUnboundMapper.Logging
{
    internal static class Log
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write(string value)
            => Logger.Instance.Write(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteLine(string value)
            => Logger.Instance.WriteLine(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteLine()
            => Logger.Instance.WriteLine();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DirectWrite(string value)
            => Logger.Instance.DirectWrite(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DirectWriteLine(string value)
            => Logger.Instance.DirectWriteLine(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DirectWriteLine()
            => Logger.Instance.DirectWriteLine();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Flush()
            => Logger.Instance.Flush();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Dispose()
            => Logger.Instance.Dispose();
    }
}
