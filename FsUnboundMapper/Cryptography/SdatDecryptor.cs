using libps3;
using System.IO;

namespace FsUnboundMapper.Cryptography
{
    internal static class SdatDecryptor
    {
        public static void DecryptIfExists(string path)
        {
            if (!File.Exists(path) ||
                new FileInfo(path).Length < 1) // Try decryption again if file did not decrypt right last time
            {
                string sdatPath = path + ".sdat";
                if (File.Exists(sdatPath))
                {
                    EDATA.DecryptSdata(sdatPath, path);
                }
            }
        }
    }
}
