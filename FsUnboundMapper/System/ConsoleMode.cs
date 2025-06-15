namespace FsUnboundMapper.System
{
    public static class ConsoleMode
    {
        private static ConsoleModeManager Instance = new ConsoleModeManager();

        // Prevents users from freezing the program when clicking on the console in windows.
        /// <summary>
        /// Lock several things on the console and save the previous mode.
        /// </summary>
        public static void LockConsole()
            => Instance.LockConsole();

        // Restores the previous state for users calling the program from a terminal.
        /// <summary>
        /// Unlock quick edit on the console and restore the previous mode if successfully locked prior.
        /// </summary>
        public static void UnlockConsole()
            => Instance.UnlockConsole();
    }
}
