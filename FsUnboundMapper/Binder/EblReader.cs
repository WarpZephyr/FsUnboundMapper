using Edoke.IO;
using FsUnboundMapper.Binder.Strategy;
using FsUnboundMapper.Cryptography;
using FsUnboundMapper.IO;
using FsUnboundMapper.IO.Buffers;
using SoulsFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace FsUnboundMapper.Binder
{
    public class EblReader : IDisposable, IAsyncDisposable
    {
        private readonly EblReaderConfig Config;
        private readonly BHD5 Header;
        private readonly BinaryStreamReader DataReader;
        private bool disposedValue;

        public EblReader(EblReaderConfig config, BHD5 header, Stream data)
        {
            Config = config;
            Header = header;
            DataReader = new BinaryStreamReader(data, false, config.LeaveDataOpen);
        }

        #region Factory

        private static BHD5.Game GetEblVersion(GameType game)
        {
            switch (game)
            {
                case GameType.ArmoredCoreV:
                case GameType.ArmoredCoreVerdictDay:
                    return BHD5.Game.DarkSouls1;
                default:
                    throw new NotSupportedException($"{nameof(GameType)} {game} is currently not supported in method: {nameof(GetEblVersion)}");
            }
        }

        private static bool EblUses64BitHashes(BHD5.Game version)
            => version >= BHD5.Game.EldenRing;

        private static ModulusBucketIndexStrategy GetEblIndexingStrategy(BHD5 bhd5)
            => new ModulusBucketIndexStrategy(bhd5.Buckets.Count);

        public static EblReader Open(string headerPath, string dataPath, GameType game, PlatformType platform)
        {
            string headerName = Path.GetFileNameWithoutExtension(headerPath);
            string binderKeysDir = BinderKeys.GetAssetDirectory(game, platform);
            string hashDir = Path.Combine(binderKeysDir, "Hash");
            string keyDir = Path.Combine(binderKeysDir, "Key");
            string hashPath = Path.Combine(hashDir, $"{headerName}.txt");
            string keyPath = Path.Combine(keyDir, $"{headerName}.pem");

            var version = GetEblVersion(game);
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

        #endregion

        #region File Exists

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool FileExists(string name)
            => FileHeaderExists(name);

        #endregion

        #region Enumerate Files

        public IEnumerable<EblFile> EnumerateFiles()
        {
            foreach (var bucket in Header.Buckets)
            {
                foreach (var file in bucket)
                {
                    yield return OpenFile(file);
                }
            }
        }

        #endregion

        #region Open File

        public EblFile OpenFile(string name)
        {
            if (!TryGetFileHeader(name, out BHD5.FileHeader? header))
            {
                throw new FileNotFoundException($"Could not locate the specified file: {name}", name);
            }

            return OpenFile(header);
        }

        public bool TryOpenFile(string name, [NotNullWhen(true)] out EblFile? file)
        {
            if (!TryGetFileHeader(name, out BHD5.FileHeader? header))
            {
                file = null;
                return false;
            }

            file = OpenFile(header);
            return true;
        }

        private EblFile OpenFile(BHD5.FileHeader file)
        {
            bool pathUnknown;
            if (!Config.NameDictionary.TryGetValue(file.FileNameHash, out string? name))
            {
                name = $"/_unknown/{file.FileNameHash}";
                pathUnknown = true;
            }
            else
            {
                pathUnknown = false;
            }

            return new EblFile(DataReader, name, pathUnknown, file, GetFileLength(file, Header.Format));
        }

        #endregion

        #region Helpers

        private bool TryGetFileHeader(string name, [NotNullWhen(true)] out BHD5.FileHeader? header)
        {
            ulong hash = Config.NameDictionary.ComputeHash(name);
            int bucketIndex = Config.IndexingStrategy.ComputeBucketIndex(hash);
            if (bucketIndex >= Header.Buckets.Count || bucketIndex < 0)
            {
                header = null;
                return false;
            }

            var bucket = Header.Buckets[bucketIndex];
            foreach (var file in bucket)
            {
                if (file.FileNameHash == hash)
                {
                    header = file;
                    return true;
                }
            }

            header = null;
            return false;
        }

        private bool FileHeaderExists(string name)
        {
            ulong hash = Config.NameDictionary.ComputeHash(name);
            int bucketIndex = Config.IndexingStrategy.ComputeBucketIndex(hash);
            if (bucketIndex >= Header.Buckets.Count || bucketIndex < 0)
            {
                return false;
            }

            var bucket = Header.Buckets[bucketIndex];
            foreach (var file in bucket)
            {
                if (file.FileNameHash == hash)
                {
                    return true;
                }
            }

            return false;
        }

        private static long GetFileLength(BHD5.FileHeader fileHeader, BHD5.Game version)
        {
            if (version >= BHD5.Game.DarkSouls3)
            {
                return fileHeader.UnpaddedFileSize;
            }

            return fileHeader.PaddedFileSize;
        }

        #endregion

        #region IDisposable

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    DataReader.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public ValueTask DisposeAsync()
        {
            // Perform async cleanup.
            var value = DataReader.DisposeAsync();
            GC.SuppressFinalize(this);
            return value;
        }

        #endregion

        #region EblFile

        public class EblFile
        {
            const int DefaultCopyBufferSize = 81920;

            private static readonly Aes AES;

            private readonly BinaryStreamReader DataReader;
            public readonly string Path;
            public readonly bool PathUnknown;
            private readonly BHD5.FileHeader Header;
            public readonly long Length;

            static EblFile()
            {
                AES = Aes.Create();
                AES.Mode = CipherMode.ECB;
                AES.Padding = PaddingMode.None;
                AES.KeySize = 128;
            }

            internal EblFile(BinaryStreamReader dataReader, string path, bool pathUnknown, BHD5.FileHeader fileHeader, long length)
            {
                DataReader = dataReader;
                Path = path;
                PathUnknown = pathUnknown;
                Header = fileHeader;
                Length = length;
            }

            #region Read

            public Stream Read()
            {
                var ms = new MemoryStream();
                if (Length == 0)
                    return ms;

                if (Header.AESKey != null)
                {
                    foreach (var chunk in EnumerateEncryptedChunks())
                    {
                        ms.Write(chunk.Buffer, 0, chunk.Length);
                    }
                }
                else
                {
                    DataReader.BaseStream.Seek(Header.FileOffset, SeekOrigin.Begin);
                    using var cs = new CopyStream(DataReader.BaseStream, Length);
                    cs.CopyTo(ms);
                }

                return ms;
            }

            public async Task<Stream> ReadAsync()
            {
                var ms = new MemoryStream();
                if (Length == 0)
                    return ms;

                if (Header.AESKey != null)
                {
                    foreach (var chunk in EnumerateEncryptedChunks())
                    {
                        await ms.WriteAsync(chunk.Buffer.AsMemory(0, chunk.Length));
                    }
                }
                else
                {
                    DataReader.BaseStream.Seek(Header.FileOffset, SeekOrigin.Begin);
                    using var cs = new CopyStream(DataReader.BaseStream, Length);
                    await cs.CopyToAsync(ms);
                }

                return ms;
            }

            #endregion

            #region Write

            public void WriteTo(string path)
            {
                if (Length == 0)
                {
                    File.Create(path);
                    return;
                }

                using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, Math.Min(DefaultCopyBufferSize, (int)Length), FileOptions.SequentialScan);
                if (Header.AESKey != null)
                {
                    foreach (var chunk in EnumerateEncryptedChunks())
                    {
                        fs.Write(chunk.Buffer, 0, chunk.Length);
                    }
                }
                else
                {
                    DataReader.BaseStream.Seek(Header.FileOffset, SeekOrigin.Begin);
                    using var cs = new CopyStream(DataReader.BaseStream, Length);
                    cs.CopyTo(fs);
                }
            }

            public Task WriteToAsync(string path)
            {
                if (Length == 0)
                {
                    File.Create(path);
                    return Task.CompletedTask;
                }

                var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, Math.Min(DefaultCopyBufferSize, (int)Length), FileOptions.SequentialScan);
                return Core(fs);
                async Task Core(FileStream fs)
                {
                    if (Header.AESKey != null)
                    {
                        foreach (var chunk in EnumerateEncryptedChunks())
                        {
                            await fs.WriteAsync(chunk.Buffer.AsMemory(0, chunk.Length));
                        }
                    }
                    else
                    {
                        DataReader.BaseStream.Seek(Header.FileOffset, SeekOrigin.Begin);
                        using var cs = new CopyStream(DataReader.BaseStream, Length);
                        await cs.CopyToAsync(fs);
                    }

                    await fs.DisposeAsync();
                }
            }

            #endregion

            #region CopyTo

            public void CopyTo(Stream stream)
            {
                if (Length == 0)
                    return;

                if (Header.AESKey != null)
                {
                    foreach (var chunk in EnumerateEncryptedChunks())
                    {
                        stream.Write(chunk.Buffer, 0, chunk.Length);
                    }
                }
                else
                {
                    DataReader.BaseStream.Seek(Header.FileOffset, SeekOrigin.Begin);
                    using var cs = new CopyStream(DataReader.BaseStream, Length);
                    cs.CopyTo(stream);
                }
            }

            public Task CopyToAsync(Stream stream)
            {
                if (Length == 0)
                    return Task.CompletedTask;

                return Core(stream);
                async Task Core(Stream stream)
                {
                    if (Header.AESKey != null)
                    {
                        foreach (var chunk in EnumerateEncryptedChunks())
                        {
                            await stream.WriteAsync(chunk.Buffer.AsMemory(0, chunk.Length));
                        }
                    }
                    else
                    {
                        DataReader.BaseStream.Seek(Header.FileOffset, SeekOrigin.Begin);
                        using var cs = new CopyStream(DataReader.BaseStream, Length);
                        await cs.CopyToAsync(stream);
                    }
                }
            }

            #endregion

            #region Helpers

            private IEnumerable<RentBuffer<byte>> EnumerateEncryptedChunks()
            {
                using var decryptor = AES.CreateDecryptor(Header.AESKey.Key, new byte[16]);
                foreach (var range in Header.AESKey.Ranges)
                {
                    var encData = RentRangeBuffer(range);
                    if (encData.Length < 1)
                    {
                        yield return encData;
                    }
                    else
                    {
                        decryptor.TransformBlock(encData.Buffer, 0, encData.Length, encData.Buffer, 0);
                    }

                    encData.Return();
                }
            }

            private RentBuffer<byte> RentRangeBuffer(BHD5.Range range)
            {
                if (range.StartOffset < 0 ||
                    range.EndOffset < 0 ||
                    range.StartOffset > range.EndOffset ||
                    range.StartOffset == range.EndOffset)
                {
                    return new RentBuffer<byte>(0);
                }

                int length = (int)(range.EndOffset - range.StartOffset);
                var buffer = new RentBuffer<byte>(length);
                DataReader.GetBytes(range.StartOffset, buffer.Buffer, length);
                return buffer;
            }

            #endregion
        }

        #endregion
    }
}
