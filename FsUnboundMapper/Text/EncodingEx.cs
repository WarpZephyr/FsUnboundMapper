using System.Text;

namespace FsUnboundMapper.Text
{
    /// <summary>
    /// A helper containing encodings that are usually not easily accessed.
    /// </summary>
    internal class EncodingEx
    {
        /// <summary>
        /// UTF-16 or Unicode encoding in little endian.
        /// </summary>
        public static readonly Encoding UTF16LE;

        /// <summary>
        /// UTF-16 or Unicode encoding in big endian.
        /// </summary>
        public static readonly Encoding UTF16BE;

        /// <summary>
        /// Japanese Shift-JIS encoding.
        /// </summary>
        public static readonly Encoding ShiftJIS;

        /// <summary>
        /// Register the encodings.
        /// </summary>
        static EncodingEx()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            UTF16LE = Encoding.Unicode;
            UTF16BE = Encoding.BigEndianUnicode;
            ShiftJIS = Encoding.GetEncoding("shift-jis");
        }
    }
}
