using XenonFormats;

namespace FsUnboundMapper
{
    internal static class XexHelper
    {
        public static RegionType GetRegionType(XEX2 xex)
        {
            if (xex.SecurityInfo.RegionCodeFlags == XexRegionCodeFlags.All)
            {
                return RegionType.All;
            }
            else if ((xex.SecurityInfo.RegionCodeFlags & XexRegionCodeFlags.NTSC_U) != 0)
            {
                return RegionType.UnitedStates;
            }
            else if (((xex.SecurityInfo.RegionCodeFlags & XexRegionCodeFlags.NTSC_J) != 0) || ((xex.SecurityInfo.RegionCodeFlags & XexRegionCodeFlags.NTSC_J_Other) != 0))
            {
                return RegionType.Japan;
            }
            else if ((xex.SecurityInfo.RegionCodeFlags & XexRegionCodeFlags.NTSC_J_China) != 0)
            {
                return RegionType.China;
            }
            else if (((xex.SecurityInfo.RegionCodeFlags & XexRegionCodeFlags.PAL) != 0) || ((xex.SecurityInfo.RegionCodeFlags & XexRegionCodeFlags.PAL_AU_NZ) != 0) || ((xex.SecurityInfo.RegionCodeFlags & XexRegionCodeFlags.PAL_Other) != 0))
            {
                return RegionType.Europe;
            }
            else if ((xex.SecurityInfo.RegionCodeFlags & XexRegionCodeFlags.Other) != 0)
            {
                return RegionType.Unknown;
            }

            return RegionType.Unknown;
        }

        public static string GetTitleId(XEX2 xex)
        {
            foreach (var header in xex.OptionalHeaders)
            {
                if (header is XexExecutionIdHeader executionId)
                {
                    return executionId.TitleID.ToString();
                }
            }

            return string.Empty;
        }

        public static string GetOriginalPeName(XEX2 xex)
        {
            foreach (var header in xex.OptionalHeaders)
            {
                if (header is XexOriginalPeNameHeader originalPeName)
                {
                    return originalPeName.OriginalPeName;
                }
            }

            return string.Empty;
        }
    }
}
