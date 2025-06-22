using libps3;
using System.IO;

namespace FsUnboundMapper.Cryptography
{
    internal static class SdatDecryptor
    {
        public static void DecryptIfExists(string path)
        {
            if (!File.Exists(path))
            {
                string sdatPath = path + ".sdat";
                if (File.Exists(sdatPath))
                {
                    EDAT.DecryptSdatFile(sdatPath, path);
                }
            }
        }
    }
}
