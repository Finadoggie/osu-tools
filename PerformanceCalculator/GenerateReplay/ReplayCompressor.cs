// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using System.IO;
using System.Text;
using SharpCompress.Compressors.LZMA;

namespace PerformanceCalculator.GenerateReplay
{
    public static class ReplayCompressor
    {
        public struct ReplayFrame
        {
            public long deltaTime;
            public float x;
            public float y;
            public int clicks;
        }

        public static byte[] CompressReplayFrames(ReplayFrame[] frames)
        {
            // 1. Build the comma-separated string payload
            StringBuilder sb = new StringBuilder();

            foreach (var frame in frames)
            {
                // Format: w|x|y|z,
                sb.AppendFormat(CultureInfo.InvariantCulture, "{0}|{1}|{2}|{3},",
                    frame.deltaTime, frame.x, frame.y, frame.clicks);
            }

            byte[] content = Encoding.UTF8.GetBytes(sb.ToString());

            using (var outStream = new MemoryStream())
            {
                using (var lzma = new LzmaStream(new LzmaEncoderProperties(false, 1 << 21, 255), false, outStream))
                {
                    outStream.Write(lzma.Properties);

                    long fileSize = content.Length;
                    for (int i = 0; i < 8; i++)
                        outStream.WriteByte((byte)(fileSize >> (8 * i)));

                    lzma.Write(content);
                }

                return outStream.ToArray();
            }
        }
    }
}
