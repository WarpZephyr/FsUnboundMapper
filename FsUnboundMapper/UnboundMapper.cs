using FsUnboundMapper.Binder;
using FsUnboundMapper.Binder.Strategy;
using FsUnboundMapper.Cryptography;
using FsUnboundMapper.Exceptions;
using FsUnboundMapper.Logging;
using libps3;
using SoulsFormats;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;

namespace FsUnboundMapper
{
    internal class UnboundMapper
    {
        private readonly string AppRoot;
        private string Root;
        private GameType Game;
        private PlatformType Platform;

        public UnboundMapper(string appRoot, string root)
        {
            AppRoot = appRoot;
            Root = root;
            Game = AppConfig.Instance.ManualGameOverride;
            Platform = AppConfig.Instance.ManualPlatformOverride;
        }

        public Task RunAsync()
        {
            Log.WriteLine("Running automatic detection where applicable...");
            DetectPlatform();
            DetectRoot();
            DetectGame();

            Log.WriteLine($"Determined platform as {Platform} and game as {Game}.");
            Log.WriteLine($"Determined root folder as: {Root}");

            Log.WriteLine($"Mapping {Game}...");
            switch (Game)
            {
                case GameType.ArmoredCoreForAnswer:
                    return RunAcfaAsync();
                case GameType.ArmoredCoreV:
                    return RunAcvAsync();
                case GameType.ArmoredCoreVerdictDay:
                    return RunAcvdAsync();
                default:
                    throw new UserErrorException("Could not determine a valid game.");
            }
        }

        private Task RunAcfaAsync()
        {
            string bindDir = Path.Combine(Root, "bind");
            if (!CheckDirectory(bindDir))
                return Task.CompletedTask;

            throw new NotImplementedException();
        }

        private Task RunAcvAsync()
        {
            string bindDir = Path.Combine(Root, "bind");
            if (!CheckDirectory(bindDir))
                return Task.CompletedTask;

            string headerPath = Path.Combine(bindDir, "dvdbnd5.bhd");
            string dataPath = Path.Combine(bindDir, "dvdbnd.bdt");
            if (!CheckSplitFile(headerPath, dataPath))
                return UnpackScriptsAsync(bindDir);

            return Task.WhenAll([UnpackScriptsAsync(bindDir), UnpackEbls()]);
            async Task UnpackEbls()
            {
                await UnpackEblAsync(headerPath, dataPath);
                await Task.WhenAll([UnpackBootBindersAsync(bindDir), UnpackMissionsAsync(bindDir)]);

                string modelMapDir = Path.Combine(Root, "model", "map");
                if (CheckDirectory(modelMapDir))
                    await PackAcvMapsAsync(modelMapDir);

                string soundDir = Path.Combine(Root, "sound");
                if (AppConfig.Instance.ApplyFmodCrashFix &&
                    CheckDirectory(soundDir))
                    ApplyFmodCrashFix(soundDir, "se_weapon.fsb");

                if (AppConfig.Instance.HidePackedFiles)
                    HideFile(bindDir, "dvdbnd5.bhd");
            }
        }

        private Task RunAcvdAsync()
        {
            string bindDir = Path.Combine(Root, "bind");
            if (!CheckDirectory(bindDir))
                return Task.CompletedTask;

            string headerPath0 = Path.Combine(bindDir, "dvdbnd5_layer0.bhd");
            string dataPath0 = Path.Combine(bindDir, "dvdbnd_layer0.bdt");
            string headerPath1;
            string dataPath1;
            if (!CheckSplitFile(headerPath0, dataPath0))
                return UnpackScriptsAsync(bindDir);

            if (Platform == PlatformType.Xbox360)
            {
                headerPath1 = Path.Combine(bindDir, "dvdbnd5_layer1.bhd");
                dataPath1 = Path.Combine(bindDir, "dvdbnd_layer1.bdt");
                if (!CheckSplitFile(headerPath1, dataPath1))
                    return UnpackScriptsAsync(bindDir);
            }
            else
            {
                headerPath1 = string.Empty;
                dataPath1 = string.Empty;
            }

            return Task.WhenAll([UnpackScriptsAsync(bindDir), UnpackEbls()]);
            async Task UnpackEbls()
            {
                if (Platform == PlatformType.Xbox360)
                    await Task.WhenAll([UnpackEblAsync(headerPath0, dataPath0), UnpackEblAsync(headerPath1, dataPath1)]);
                else
                    await UnpackEblAsync(headerPath0, dataPath0);

                await Task.WhenAll([UnpackBootBindersAsync(bindDir), UnpackMissionsAsync(bindDir)]);
                string soundDir = Path.Combine(Root, "sound");
                if (AppConfig.Instance.ApplyFmodCrashFix &&
                    CheckDirectory(soundDir))
                    ApplyFmodCrashFix(soundDir, "se_weapon.fsb");

                if (AppConfig.Instance.HidePackedFiles)
                {
                    HideFile(bindDir, "dvdbnd5_layer0.bhd");
                    if (Platform == PlatformType.Xbox360)
                    {
                        HideFile(bindDir, "dvdbnd5_layer1.bhd");
                    }
                }
            }
        }

        #region Game Run Helpers

        private Task UnpackBootBindersAsync(string bindDir)
        {
            Log.WriteLine("Unpacking boot binders...");
            string bootPath = Path.Combine(bindDir, "boot.bnd");
            string boot2ndPath = Path.Combine(bindDir, "boot_2nd.bnd");
            bool bootExists = File.Exists(bootPath);
            bool boot2ndExists = File.Exists(boot2ndPath);

            if (bootExists && boot2ndExists)
            {
                return Task.WhenAll([UnpackBinder3Async(bootPath, Root), UnpackBinder3Async(boot2ndPath, Root)]);
            }
            else if (bootExists)
            {
                Log.WriteLine($"Warning: Could not find boot 2nd binder \"boot_2nd.bnd\" for unpacking from path: {boot2ndPath}");
                return UnpackBinder3Async(bootPath, Root);
            }
            else if (boot2ndExists)
            {
                Log.WriteLine($"Warning: Could not find boot binder \"boot.bnd\" for unpacking from path: {bootPath}");
                return UnpackBinder3Async(boot2ndPath, Root);
            }

            return Task.CompletedTask;
        }

        private Task UnpackScriptsAsync(string bindDir)
        {
            Log.WriteLine("Unpacking script binders...");
            string scriptHeaderPath = Path.Combine(bindDir, "script.bhd");
            string scriptDataPath = Path.Combine(bindDir, "script.bdt");
            if (Platform == PlatformType.PlayStation3)
            {
                HandleSdat(scriptHeaderPath);
                HandleSdat(scriptDataPath);
            }

            if (!CheckSplitFile(scriptHeaderPath, scriptDataPath))
                return Task.CompletedTask;

            string aiScriptDir = Path.Combine(Root, "airesource", "script");
            string sceneScriptDir = Path.Combine(Root, "scene");
            return Core();
            async Task Core()
            {
                using var bnd = new BXF3Reader(scriptHeaderPath, scriptDataPath);
                foreach (var file in bnd.Files)
                {
                    string outName = CleanComponentPath(file.Name);
                    string baseDir = outName.EndsWith("scene.lc", StringComparison.InvariantCultureIgnoreCase) ? sceneScriptDir : aiScriptDir;
                    string outPath = CreatePath(baseDir, outName);
                    if (file.CompressedSize == 0 &&
                        file.UncompressedSize == 0)
                    {
                        File.Create(outPath);
                        continue;
                    }
                    else
                    {
                        await File.WriteAllBytesAsync(outPath, bnd.ReadFile(file));
                    }
                }
            }
        }

        private Task UnpackMissionsAsync(string bindDir)
        {
            Log.WriteLine("Unpacking mission binders...");
            string missionBindDir = Path.Combine(bindDir, "mission");
            if (!CheckDirectory(missionBindDir))
                return Task.CompletedTask;

            return Core();
            async Task Core()
            {
                foreach (var file in Directory.EnumerateFiles(missionBindDir, "*.bnd", SearchOption.TopDirectoryOnly))
                {
                    await UnpackBinder3Async(file, Root);
                }
            }
        }

        private static void HideFile(string dir, string name)
        {
            string path = Path.Combine(dir, name);
            if (!File.Exists(path))
            {
                return;
            }

            if (!name.StartsWith('-'))
            {
                Log.WriteLine($"Renaming {name} to ensure game does not find it...");

                string newPath = Path.Combine(dir, $"-{name}");
                File.Move(path, newPath);
                Log.WriteLine($"Renamed {name} to -{name}");
            }
        }

        private Task PackAcvMapsAsync(string dir)
        {
            return Core();
            async Task Core()
            {
                Log.WriteLine("Packing Armored Core V map models and textures...");
                foreach (var directory in Directory.EnumerateDirectories(dir, "m*", SearchOption.TopDirectoryOnly))
                {
                    await PackAcvMapAsync(directory);
                }
            }
        }

        private static Task PackAcvMapAsync(string dir)
        {
            void SetBinderInfo(BND3 bnd)
            {
                bnd.Version = "JP100";
                bnd.Format = SoulsFormats.Binder.Format.IDs | SoulsFormats.Binder.Format.Names1 | SoulsFormats.Binder.Format.Compression;
                bnd.BitBigEndian = true;
                bnd.BigEndian = true;
                bnd.Unk18 = 0;
                for (int i = 0; i < bnd.Files.Count; i++)
                {
                    bnd.Files[i].Flags = SoulsFormats.Binder.FileFlags.Flag1;
                    bnd.Files[i].ID = i;
                }
            }

            string mapID = Path.GetFileName(dir);
            string modelBNDPath = Path.Combine(dir, $"{mapID}_m.dcx.bnd");
            var modelBND = PackBinder3(dir, [".flv", ".hmd", ".smd", ".mlb"]);
            SetBinderInfo(modelBND);

            string textureBNDPath = Path.Combine(dir, $"{mapID}_htdcx.bnd");
            var textureBND = PackBinder3(dir, ".tpf.dcx", "_l.tpf.dcx");
            SetBinderInfo(textureBND);

            byte[] modelBytes = modelBND.Write();
            var modelTask = File.WriteAllBytesAsync(modelBNDPath, modelBytes);

            byte[] textureBytes = textureBND.Write();
            var textureTask = File.WriteAllBytesAsync(textureBNDPath, textureBytes);
            return Task.WhenAll([modelTask, textureTask]);
        }

        private void ApplyFmodCrashFix(string soundDir, string fmodName)
        {
            string seWeaponPath = Path.Combine(soundDir, fmodName);
            var soundFI = new FileInfo(seWeaponPath);
            int expandLength = 20_000_000;
            if (soundFI.Exists && soundFI.Length < expandLength)
            {
                Log.WriteLine($"Expanding {fmodName} to fix fmod crash...");
                Expand(seWeaponPath, expandLength);
            }
        }

        #endregion

        #region File

        static void Expand(string path, int length, int chunkSize = 65536)
        {
            using FileStream fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, chunkSize, FileOptions.SequentialScan);
            int totalChunks = length / chunkSize;
            for (int i = 0; i < totalChunks; i++)
            {
                fs.Write(new byte[chunkSize], 0, chunkSize);
                length -= chunkSize;
            }

            fs.Write(new byte[length], 0, length);
        }

        #endregion

        #region Path

        private bool CheckDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Log.WriteLine($"Warning: Could not find \"{Path.GetFileName(path)}\" folder, game was not unpacked correctly or is missing files.");
                return false;
            }

            return true;
        }

        private bool CheckFile(string path)
        {
            if (!File.Exists(path))
            {
                Log.WriteLine($"Warning: Could not find \"{Path.GetFileName(path)}\" file, game was not unpacked correctly or is missing files.");
                return false;
            }

            return true;
        }

        private bool CheckSplitFile(string headerPath, string dataPath)
            => CheckFile(headerPath) && CheckFile(dataPath);

        private string CreatePath(string baseDir, string name)
        {
            string outPath = Path.Combine(baseDir, name);
            string? outDir = Path.GetDirectoryName(outPath);
            if (string.IsNullOrEmpty(outDir))
                throw new Exception($"Could not get folder name of built output path: {outPath}");

            Directory.CreateDirectory(outDir);
            return outPath;
        }

        private string CleanComponentPath(string path)
        {
            path = RemovePathRoot(path);
            path = NormalizePathSlashes(path);
            path = SetPathCasing(path);
            return path;
        }

        private string SetPathCasing(string path)
        {
            if (AppConfig.Instance.LowercaseFileNames)
            {
                path = path.ToLowerInvariant();
            }

            return path;
        }

        private string RemovePathRoot(string path)
        {
            int rootIndex = path.IndexOf(':');
            if (rootIndex > -1)
                path = path[rootIndex..];

            return path;
        }

        private string NormalizePathSlashes(string path)
        {
            path = path.Replace('\\', Path.DirectorySeparatorChar);
            path = path.Replace('/', Path.DirectorySeparatorChar);
            path = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            path = path.TrimStart('\\');
            return path;
        }

        #endregion

        #region Sdat

        private void HandleSdat(string path)
        {
            if (!File.Exists(path))
            {
                string sdatPath = path + ".sdat";
                if (File.Exists(sdatPath))
                {
                    Log.WriteLine($"Decrypting sdat: {sdatPath}");
                    EDAT.DecryptSdatFile(sdatPath, path);
                }
            }
        }

        #endregion

        #region Binder

        private static BND3 PackBinder3(string dir, Span<string> extensions)
        {
            var binder = new BND3();
            int nameIndex = dir.Length + 1;
            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.TopDirectoryOnly))
            {
                foreach (string extension in extensions)
                {
                    if (file.EndsWith(extension))
                    {
                        var bfile = new BinderFile
                        {
                            Name = file[nameIndex..],
                            Bytes = File.ReadAllBytes(file)
                        };
                        binder.Files.Add(bfile);
                        break;
                    }
                }
            }

            return binder;
        }

        private static BND3 PackBinder3(string dir, string extension, string excludeExtension)
        {
            var binder = new BND3();
            int nameIndex = dir.Length + 1;
            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.TopDirectoryOnly))
            {
                if (file.EndsWith(extension) &&
                    !file.EndsWith(excludeExtension))
                {
                    var bfile = new BinderFile
                    {
                        Name = file[nameIndex..],
                        Bytes = File.ReadAllBytes(file)
                    };

                    binder.Files.Add(bfile);
                }
            }

            return binder;
        }

        private Task UnpackBinder3Async(string path, string destDir)
        {
            BinderReader reader = new BND3Reader(path);
            return UnpackBinderAsync(reader, destDir);
        }

        private Task UnpackSplitBinder3Async(string headerPath, string dataPath, string destDir)
        {
            BinderReader reader = new BXF3Reader(headerPath, dataPath);
            return UnpackBinderAsync(reader, destDir);
        }

        private async Task UnpackBinderAsync(BinderReader bnd, string destDir)
        {
            foreach (var file in bnd.Files)
            {
                string outName = file.Name;
                int rootIndex = outName.IndexOf(':');
                if (rootIndex > -1)
                    outName = outName[rootIndex..];

                outName = outName.Replace('\\', Path.DirectorySeparatorChar);
                outName = outName.Replace('/', Path.DirectorySeparatorChar);
                outName = outName.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
                outName = outName.TrimStart('\\');

                string outPath = Path.Combine(destDir, outName);
                string? outDir = Path.GetDirectoryName(outPath);
                if (string.IsNullOrEmpty(outDir))
                    throw new Exception($"Could not get folder name of built output path: {outPath}");

                Directory.CreateDirectory(outDir);
                if (file.CompressedSize == 0 &&
                    file.UncompressedSize == 0)
                {
                    File.Create(outPath);
                    continue;
                }
                else
                {
                    await File.WriteAllBytesAsync(outPath, bnd.ReadFile(file));
                }
            }

            bnd.Dispose();
        }

        #endregion

        #region BinderKeys

        private string GetBinderKeysName()
        {
            string game;
            switch (Game)
            {
                case GameType.ArmoredCoreV:
                    game = "ArmoredCore5";
                    break;
                case GameType.ArmoredCoreVerdictDay:
                    game = "ArmoredCoreVerdictDay";
                    break;
                default:
                    throw new NotSupportedException($"{nameof(GameType)} {Game} is currently not supported in method: {nameof(GetBinderKeysName)}");
            }

            string platform;
            switch (Platform)
            {
                case PlatformType.PlayStation3:
                    platform = "PS3";
                    break;
                case PlatformType.Xbox360:
                    platform = "X360";
                    break;
                default:
                    throw new NotSupportedException($"{nameof(PlatformType)} {Platform} is currently not supported in method: {nameof(GetBinderKeysName)}");
            }

            return $"{game}_{platform}";
        }

        private string GetBinderKeysAssetDirectory()
            => Path.Combine(AppRoot, "Assets", "BinderKeys", GetBinderKeysName());

        #endregion

        #region Ebl

        private BHD5.Game GetEblVersion()
        {
            switch (Game)
            {
                case GameType.ArmoredCoreV:
                case GameType.ArmoredCoreVerdictDay:
                    return BHD5.Game.DarkSouls1;
                default:
                    throw new NotSupportedException($"{nameof(GameType)} {Game} is currently not supported in method: {nameof(GetEblVersion)}");
            }
        }

        private bool EblUses64BitHashes(BHD5.Game version)
            => version >= BHD5.Game.EldenRing;

        private ModulusBucketIndexStrategy GetEblIndexingStrategy(BHD5 bhd5)
            => new ModulusBucketIndexStrategy(bhd5.Buckets.Count);

        private EblReader OpenEbl(string headerPath, string dataPath)
        {
            string headerName = Path.GetFileNameWithoutExtension(headerPath);
            string binderKeysDir = GetBinderKeysAssetDirectory();
            string hashDir = Path.Combine(binderKeysDir, "Hash");
            string keyDir = Path.Combine(binderKeysDir, "Key");
            string hashPath = Path.Combine(hashDir, $"{headerName}.txt");
            string keyPath = Path.Combine(keyDir, $"{headerName}.pem");

            var version = GetEblVersion();
            var nameDictionary = new BinderHashDictionary(EblUses64BitHashes(version));
            if (File.Exists(hashPath))
                nameDictionary.AddRange(File.ReadAllLines(hashPath));

            string? key;
            if (File.Exists(keyPath))
                key = File.ReadAllText(keyPath);
            else
                key = null;

            BHD5 header;
            if (key != null)
                header = BHD5.Read(Rsa.Decrypt(headerPath, key), version);
            else
                header = BHD5.Read(headerPath, version);

            var indexingStrategy = GetEblIndexingStrategy(header);
            var config = new EblReaderConfig
            {
                NameDictionary = nameDictionary,
                IndexingStrategy = indexingStrategy,
                LeaveDataOpen = false
            };

            var dfs = File.OpenRead(dataPath);
            return new EblReader(config, header, dfs);
        }

        private async Task UnpackEblAsync(string headerPath, string dataPath)
        {
            Log.WriteLine($"Unpacking binder \"{Path.GetFileNameWithoutExtension(headerPath)}\"...");
            using var ebl = OpenEbl(headerPath, dataPath);
            foreach (var file in ebl.EnumerateFiles())
            {
                if (AppConfig.Instance.SkipUnknownFiles && file.PathUnknown)
                    continue;

                string outName = CleanComponentPath(file.Path);
                string outPath = CreatePath(Root, outName);
                await file.WriteToAsync(outPath);
            }
        }

        #endregion

        #region Detect Platform

        public void DetectPlatform()
        {
            if (Platform != PlatformType.None &&
                Platform != PlatformType.Unknown)
            {
                // Manually overridden already
                return;
            }

            if (FindPlatformByFile(Root, out PlatformType platform))
            {
                // Check the possible files
                string? root = Path.GetDirectoryName(Root);
                if (string.IsNullOrEmpty(root))
                    throw new UserErrorException($"Error: Could not get root game folder from path: {Root}");

                Root = root;
                Platform = platform;
                return;
            }
            else if (Directory.Exists(Root))
            {
                // Check the possible directories for the possible files
                if (FindPlatformByFolder(ref Root, Path.Combine(Root, "PS3_GAME", "USRDIR"), out platform))
                {
                    Platform = platform;
                }
                else if (FindPlatformByFolder(ref Root, Path.Combine(Root, "USRDIR"), out platform))
                {
                    Platform = platform;
                }
                else if (FindPlatformByFolder(ref Root, Root, out platform))
                {
                    Platform = platform;
                }

                return;
            }

            throw new UserErrorException($"Error: Cannot determine {nameof(PlatformType)} from path: {Root}");
        }

        static bool FindPlatformByFile(string file, out PlatformType platform)
        {
            if (File.Exists(file))
            {
                string name = Path.GetFileName(file);
                if (name.Equals("EBOOT.BIN", StringComparison.InvariantCultureIgnoreCase))
                {
                    platform = PlatformType.PlayStation3;
                    return true;
                }
                else if (name.EndsWith(".xex", StringComparison.InvariantCultureIgnoreCase))
                {
                    platform = PlatformType.Xbox360;
                    return true;
                }
                // Less likely
                else if (name.EndsWith(".elf", StringComparison.InvariantCultureIgnoreCase))
                {
                    platform = PlatformType.PlayStation3;
                    return true;
                }
            }

            platform = default;
            return false;
        }

        static bool FindPlatformByFolder(ref string root, string folder, out PlatformType platform)
        {
            if (FindPlatformByFile(Path.Combine(folder, "EBOOT.BIN"), out platform))
            {
                root = folder;
                return true;
            }
            else if (FindPlatformByFile(Path.Combine(folder, "EBOOT.elf"), out platform))
            {
                root = folder;
                return true;
            }
            else if (FindPlatformByFile(Path.Combine(root, "default.xex"), out platform))
            {
                // Just checking in root here...
                return true;
            }

            return false;
        }

        #endregion

        #region Detect Root

        public void DetectRoot()
        {
            // Check the possible files
            if (CheckPlatformFileExists(Root, Platform))
            {
                string? root = Path.GetDirectoryName(Root);
                if (string.IsNullOrEmpty(root))
                    throw new UserErrorException($"Error: Could not get root game folder from path: {Root}");

                return;
            }
            else if (Directory.Exists(Root))
            {
                // Check the possible directories for the possible files
                if (CheckPlatformFolderExists(ref Root, Path.Combine(Root, "PS3_GAME", "USRDIR"), Platform))
                {
                    return;
                }
                else if (CheckPlatformFolderExists(ref Root, Path.Combine(Root, "USRDIR"), Platform))
                {
                    return;
                }
                else if (CheckPlatformFolderExists(ref Root, Root, Platform))
                {
                    return;
                }
            }

            throw new UserErrorException($"Cannot determine root path from {nameof(PlatformType)} {Platform} and path: {Root}");
        }

        static bool CheckPlatformFileExists(string file, PlatformType platform)
        {
            if (File.Exists(file))
            {
                string name = Path.GetFileName(file);
                if (platform == PlatformType.PlayStation3)
                {
                    if (name.Equals("EBOOT.BIN", StringComparison.InvariantCultureIgnoreCase))
                    {
                        return true;
                    }
                    else if (name.EndsWith(".elf", StringComparison.InvariantCultureIgnoreCase))
                    {
                        return true;
                    }
                }
                else if (platform == PlatformType.Xbox360)
                {
                    if (name.EndsWith(".xex", StringComparison.InvariantCultureIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        static bool CheckPlatformFolderExists(ref string root, string folder, PlatformType platform)
        {
            if (platform == PlatformType.PlayStation3)
            {
                if (CheckPlatformFileExists(Path.Combine(folder, "EBOOT.BIN"), platform))
                {
                    root = folder;
                    return true;
                }
                else if (CheckPlatformFileExists(Path.Combine(folder, "EBOOT.elf"), platform))
                {
                    root = folder;
                    return true;
                }
            }
            else if (platform == PlatformType.Xbox360)
            {
                if (CheckPlatformFileExists(Path.Combine(root, "default.xex"), platform))
                {
                    // Just checking in root here...
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Detect Game

        public void DetectGame()
        {
            if (Game != GameType.None &&
                Game != GameType.Unknown)
            {
                // Manually overridden already
                return;
            }

            Log.WriteLine("Attempting to determine game by checking platform...");
            Game = FindGameByPlatform();
            if (Game != GameType.Unknown && Game != GameType.None)
                return;

            // Determine which game we are loose loading for by files
            Log.WriteLine("Attempting to determine game by checking files...");
            Game = FindGameByFile();
            if (Game != GameType.Unknown && Game != GameType.None)
                return;

            throw new UserErrorException($"Error: Game could not be determined from {nameof(PlatformType)} {Platform} and path: {Root}");
        }

        GameType FindGameByFile()
        {
            string sysDir = Path.Combine(Root, "system");
            string ac45Path = Path.Combine(sysDir, "ac45.ini");
            if (File.Exists(ac45Path))
            {
                return GameType.ArmoredCoreForAnswer;
            }

            string bindDir = Path.Combine(Root, "bind");
            string acvPath = Path.Combine(bindDir, "dvdbnd.bdt");
            if (File.Exists(acvPath))
            {
                return GameType.ArmoredCoreV;
            }

            string acvdPath = Path.Combine(bindDir, "dvdbnd_layer0.bdt");
            if (File.Exists(acvdPath))
            {
                return GameType.ArmoredCoreVerdictDay;
            }

            return GameType.Unknown;
        }

        GameType FindGameByPlatform()
        {
            if (Platform == PlatformType.PlayStation3)
            {
                Log.WriteLine("Attempting to determine game by PARAM.SFO...");
                if (TryReadParamSfo(out PARAMSFO? sfo))
                {
                    return FindGameBySFO(sfo);
                }
                else
                {
                    Log.WriteLine("Warning: PARAM.SFO could not be found or was invalid.");
                }
            }

            return GameType.Unknown;
        }

        GameType FindGameBySFO(PARAMSFO sfo)
        {
            // Try to find the title name
            if (sfo.Parameters.TryGetValue("TITLE", out PARAMSFO.Parameter? parameter))
            {
                switch (parameter.Data)
                {
                    case "ARMORED CORE for Answer":
                        return GameType.ArmoredCoreForAnswer;
                    case "ARMORED CORE V":
                        return GameType.ArmoredCoreV;
                    case "Armored Core Verdict Day":
                    case "Armored Core™: Verdict Day™":
                        return GameType.ArmoredCoreVerdictDay;
                }
            }

            // Try to find the title ID
            if (sfo.Parameters.TryGetValue("TITLE_ID", out parameter))
            {
                switch (parameter.Data)
                {
                    case "BLJM55005":
                    case "BLJM60066":
                    case "BLUS30187":
                    case "BLES00370":
                        return GameType.ArmoredCoreForAnswer;
                    case "BLKS20356":
                    case "BLAS50448":
                    case "BLJM60378":
                    case "BLUS30516":
                    case "BLES01440":
                        return GameType.ArmoredCoreV;
                    case "BLKS20441":
                    case "BLAS50618":
                    case "BLJM61014":
                    case "BLJM61020":
                    case "BLUS31194":
                    case "BLES01898":
                    case "NPUB31245":
                    case "NPEB01428":
                        return GameType.ArmoredCoreVerdictDay;
                }
            }

            return GameType.Unknown;
        }

        bool TryReadParamSfo([NotNullWhen(true)] out PARAMSFO? sfo)
        {
            // Get the USRDIR folder
            if (Root.EndsWith("USRDIR"))
            {
                // Get the PS3_GAME folder (disc) or root game folder (digital).
                string? parentDir = Path.GetDirectoryName(Root);
                if (!string.IsNullOrEmpty(parentDir))
                {
                    // Determine which game we are loose loading for by PARAM.SFO
                    string sfoPath = Path.Combine(parentDir, "PARAM.SFO");
                    if (File.Exists(sfoPath)
                        && PARAMSFO.IsRead(sfoPath, out sfo))
                    {
                        return true;
                    }
                }
            }

            sfo = null;
            return false;
        }

        #endregion
    }
}
