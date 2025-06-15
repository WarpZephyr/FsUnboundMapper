using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.OpenSsl;
using System;
using System.IO;

namespace FsUnboundMapper.Cryptography
{
    /// <summary>
    /// A class containing decryption methods for decrypting an RSA encrypted stream.
    /// </summary>
    // The main decryption method is modified from UXM, which copied from BinderTool. Thanks to Atvaarks and Nordgaren.
    public static class Rsa
    {
        /// <summary>
        /// Decrypts a stream with the provided decryption key.
        /// </summary>
        /// <param name="encryptedStream">A stream of the encrypted data.</param>
        /// <param name="publicPemKey">An RSA key in public PEM format.</param>
        /// <returns>A <see cref="MemoryStream"/> of the decrypted data.</returns>
        public static MemoryStream Decrypt(Stream encryptedStream, string publicPemKey)
        {
            ArgumentNullException.ThrowIfNull(encryptedStream, nameof(encryptedStream));
            ArgumentException.ThrowIfNullOrWhiteSpace(publicPemKey, nameof(publicPemKey));

            AsymmetricKeyParameter keyParameter = (AsymmetricKeyParameter)new PemReader(new StringReader(publicPemKey)).ReadObject();
            var engine = new RsaEngine();
            engine.Init(false, keyParameter);

            var decryptedStream = new MemoryStream();
            int inputBlockSize = engine.GetInputBlockSize();
            int outputBlockSize = engine.GetOutputBlockSize();
            byte[] inputBlock = new byte[inputBlockSize];
            while (encryptedStream.Read(inputBlock, 0, inputBlock.Length) > 0)
            {
                byte[] outputBlock = engine.ProcessBlock(inputBlock, 0, inputBlockSize);

                int requiredPadding = outputBlockSize - outputBlock.Length;
                if (requiredPadding > 0)
                {
                    byte[] paddedOutputBlock = new byte[outputBlockSize];
                    outputBlock.CopyTo(paddedOutputBlock, requiredPadding);
                    outputBlock = paddedOutputBlock;
                }

                decryptedStream.Write(outputBlock, 0, outputBlock.Length);
            }

            decryptedStream.Seek(0, SeekOrigin.Begin);
            return decryptedStream;
        }

        /// <summary>
        /// Decrypts a stream with the provided decryption key.
        /// </summary>
        /// <param name="filePath">A file path to the encrypted file.</param>
        /// <param name="publicPemKey">An RSA key in public PEM format.</param>
        /// <returns>A <see cref="MemoryStream"/> of the decrypted data.</returns>
        public static MemoryStream Decrypt(string filePath, string publicPemKey)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath, nameof(filePath));
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Decrypt(fs, publicPemKey);
        }

        /// <summary>
        /// Decrypts a stream with the provided decryption key.
        /// </summary>
        /// <param name="encryptedBytes">A byte array containing the encrypted data.</param>
        /// <param name="publicPemKey">An RSA key in public PEM format.</param>
        /// <returns>A <see cref="MemoryStream"/> of the decrypted data.</returns>
        public static MemoryStream Decrypt(byte[] encryptedBytes, string publicPemKey)
        {
            ArgumentNullException.ThrowIfNull(encryptedBytes, nameof(encryptedBytes));
            using var ms = new MemoryStream(encryptedBytes, false);
            return Decrypt(ms, publicPemKey);
        }
    }
}
