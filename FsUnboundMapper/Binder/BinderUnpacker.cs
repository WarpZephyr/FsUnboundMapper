using FsUnboundMapper.IO;
using FsUnboundMapper.Logging;
using Org.BouncyCastle.Utilities;
using SoulsFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace FsUnboundMapper.Binder
{
    internal static class BinderUnpacker
    {
        public static void UnpackBinder3s(string dir, string destDir, string wildcard, SearchOption searchOption)
        {
            foreach (var file in Directory.EnumerateFiles(dir, wildcard, searchOption))
            {
                UnpackBinder3(file, destDir);
            }
        }

        public static void UnpackBinder3sAsFolders(string dir, string destDir, string wildcard, SearchOption searchOption)
        {
            foreach (var file in Directory.EnumerateFiles(dir, wildcard, searchOption))
            {
                UnpackBinder3(file, Path.Combine(destDir, Path.GetFileNameWithoutExtension(file)));
            }
        }

        public static void UnpackBinder3(string path, string destDir)
        {
            using BinderReader reader = new BND3Reader(path);
            UnpackBinder(reader, destDir);
        }

        public static void UnpackBinder3(byte[] bytes, string destDir)
        {
            using BinderReader reader = new BND3Reader(bytes);
            UnpackBinder(reader, destDir);
        }

        public static void UnpackSplitBinder3(byte[] bhdBytes, byte[] bdtBytes, string destDir)
        {
            using BinderReader reader = new BXF3Reader(bhdBytes, bdtBytes);
            UnpackBinder(reader, destDir);
        }

        public static void UnpackBinder(BinderReader bnd, string destDir)
        {
            foreach (var file in bnd.Files)
            {
                string outName = PathCleaner.CleanComponentPath(file.Name);
                string outPath = PathCleaner.CreatePath(destDir, outName);
                UnpackBinderFile(bnd, file, outPath);
            }
        }

        public static void UnpackBinderFile(BinderReader bnd, BinderFileHeader binderFile, string outPath)
        {
            if (binderFile.CompressedSize == 0 &&
                binderFile.UncompressedSize == 0)
            {
                File.WriteAllBytes(outPath, []);
            }
            else
            {
                File.WriteAllBytes(outPath, bnd.ReadFile(binderFile));
            }
        }

        public static void UnpackEbl(string headerPath, string dataPath, string destDir, GameType game, PlatformType platform)
        {
            using var ebl = EblReader.Open(headerPath, dataPath, game, platform);
            foreach (var file in ebl.EnumerateFiles())
            {
                if (AppConfig.Instance.SkipUnknownFiles && file.PathUnknown)
                    continue;

                string outName = PathCleaner.CleanComponentPath(file.Path);
                string outPath = PathCleaner.CreatePath(destDir, outName);
                file.WriteTo(outPath);
            }
        }

        public static async Task UnpackEblAsync(string headerPath, string dataPath, string destDir, GameType game, PlatformType platform)
        {
            using var ebl = EblReader.Open(headerPath, dataPath, game, platform);
            foreach (var file in ebl.EnumerateFiles())
            {
                if (AppConfig.Instance.SkipUnknownFiles && file.PathUnknown)
                    continue;

                string outName = PathCleaner.CleanComponentPath(file.Path);
                string outPath = PathCleaner.CreatePath(destDir, outName);
                await file.WriteToAsync(outPath);
            }
        }

        public static BND3 PackBinder3(string dir, Span<string> extensions)
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

        public static BND3 PackBinder3(string dir, string extension, string excludeExtension)
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

        public static async Task<BND3> PackBinder3Async(string dir, string[] extensions)
        {
            var tasks = new List<Task>();
            var binder = new BND3();
            int nameIndex = dir.Length + 1;
            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.TopDirectoryOnly))
            {
                tasks.Add(ReadFileAsync());
                async Task ReadFileAsync()
                {
                    foreach (string extension in extensions)
                    {
                        if (file.EndsWith(extension))
                        {
                            var bfile = new BinderFile
                            {
                                Name = file[nameIndex..],
                                Bytes = await File.ReadAllBytesAsync(file)
                            };
                            binder.Files.Add(bfile);
                            break;
                        }
                    }
                }
            }

            await Task.WhenAll(tasks);
            return binder;
        }

        public static async Task<BND3> PackBinder3Async(string dir, string extension, string excludeExtension)
        {
            var tasks = new List<Task>();
            var binder = new BND3();
            int nameIndex = dir.Length + 1;
            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.TopDirectoryOnly))
            {
                tasks.Add(ReadFileAsync());
                async Task ReadFileAsync()
                {
                    if (file.EndsWith(extension) &&
                    !file.EndsWith(excludeExtension))
                    {
                        var bfile = new BinderFile
                        {
                            Name = file[nameIndex..],
                            Bytes = await File.ReadAllBytesAsync(file)
                        };

                        binder.Files.Add(bfile);
                    }
                }
            }

            await Task.WhenAll(tasks);
            return binder;
        }
    }
}
