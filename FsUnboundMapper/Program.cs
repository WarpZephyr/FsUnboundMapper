using FsUnboundMapper.Exceptions;
using FsUnboundMapper.Logging;
using FsUnboundMapper.System;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace FsUnboundMapper
{
    internal static class Program
    {
        internal const string AppName = "FsUnboundMapper";
        internal static readonly AppInfo AppInfo;
        internal static readonly string AppDataFolder;

        static Program()
        {
            AppInfo = new AppInfo();
            AppDataFolder = AppInfo.AppDirectory;
        }

        static void Main(string[] args)
        {
            AppInit(args);
        }

        #region Initialization

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void AppInit(string[] args)
        {
#if !DEBUG
            try
            {
#endif
            // Prevent program from freezing when window is clicked on
            ConsoleMode.LockConsole();
            Log.DirectWriteLine($"Starting {AppName}.");

            try
            {
                // Wrap functionality in try catch so user errors can be caught and show a friendlier message without a stacktrace
                AppRun(args);
            }
            catch (UserErrorException ex)
            {
                Pause(ex.Message);
            }
            finally
            {
                // Release lock on quick edit in case the user called from the command line
                Log.DirectWriteLine($"Closing {AppName}.");
                ConsoleMode.UnlockConsole();
                Dispose();
            }
#if !DEBUG
            }
            catch (Exception ex)
            {
                Pause(ex.Message);
            }
#endif
        }

        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void AppRun(string[] args)
        {
            Log.DirectWriteLine($"Running {AppName}.");

            // Show usage
            if (args.Length < 1)
            {
                throw new UserErrorException(
                    "This program has no UI.\n" +
                    "To use this program you can:\n" +
                    "- Provide an executable:\n" +
                    "   - EBOOT.BIN\n" +
                    "   - EBOOT.elf\n" +
                    "   - default.xex\n" +
                    "- Provide your game folder:\n" +
                    "   - [YOUR FOLDER]\n" +
                    "   - [YOUR FOLDER]\\PS3_GAME\\USRDIR\n" +
                    "   - [YOUR FOLDER]\\USRDIR\n" +
                    "   - PS3_GAME\\USRDIR\n" +
                    "   - USRDIR\n" +
                    "\n" +
                    "To provide something to the program you can:\n" +
                    "- Drag and drop it onto the program exe.\n" +
                    "- Pass it as an argument in a terminal.\n" +
                    "\n" +
                    "This is used to find your game files.\n" +
                    "Multiple things can be provided at once to loose load multiple games if desired.\n" +
                    "Various config options can also be set for most steps the program takes in config.txt.\n" +
                    "\n" +
                    "The currently supported games:\n" +
                    "- Armored Core For Answer\n" +
                    "- Armored Core V\n" +
                    "- Armored Core Verdict Day\n" +
                    "The currently supported platforms:\n" +
                    "- PS3\n" +
                    "- Xbox 360");
            }

            // Process arguments
            foreach (string arg in args)
            {
                Log.WriteLine($"Processing: \"{arg}\"");
                if (Directory.Exists(arg) || File.Exists(arg))
                {
                    var mapper = new UnboundMapper(arg);
                    mapper.Run();
                }
                else
                {
                    Log.WriteLine($"Warning: No file or folder exists at: \"{arg}\"");
                }
            }
        }

        #region Initialization Helpers

        static void Dispose()
        {
            Log.Dispose();
        }

        static void Pause(string message)
        {
            Log.DirectWriteLine(message);
            Pause();
        }

        static void Pause()
        {
            // Discard each key from the buffer so we pause when we want to using Console.ReadKey
            // While there are characters in the input stream 
            while (Console.KeyAvailable)
            {
                // Read them and ignore them
                Console.ReadKey(true); // true hides input
            }

            // Now read the next available character to pause
            Console.ReadKey(true); // true hides input
        }

        #endregion
    }
}
