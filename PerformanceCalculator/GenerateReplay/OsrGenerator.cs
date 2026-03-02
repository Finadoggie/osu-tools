// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Text;

namespace PerformanceCalculator.GenerateReplay
{
    public static class OsrGenerator
    {
        public struct ReplayData
        {
            public byte GameMode;
            public int GameVersion;
            public string BeatmapHash;
            public string PlayerName;
            public string ReplayHash;
            public short Count300;
            public short Count100;
            public short Count50;
            public short CountGeki;
            public short CountKatu;
            public short Misses;
            public int TotalScore;
            public short MaxCombo;
            public bool IsPerfect;
            public int Mods;
            public string LifeBarGraph;
            public long Timestamp; // Windows Ticks
            public int ReplayFrameBytes;
            public byte[] ReplayFrameData;
            public long OnlineID;
        }

        public static void WriteReplayHeader(string filePath, ReplayData data)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Create))
            using (BinaryWriter writer = new BinaryWriter(fs))
            {
                writer.Write(data.GameMode);
                writer.Write(data.GameVersion);

                // osu! custom string format
                writeOsuString(writer, data.BeatmapHash);
                writeOsuString(writer, data.PlayerName);
                writeOsuString(writer, data.ReplayHash);

                writer.Write(data.Count300);
                writer.Write(data.Count100);
                writer.Write(data.Count50);
                writer.Write(data.CountGeki);
                writer.Write(data.CountKatu);
                writer.Write(data.Misses);
                writer.Write(data.TotalScore);
                writer.Write(data.MaxCombo);
                writer.Write((byte)(data.IsPerfect ? 1 : 0));
                writer.Write(data.Mods);

                writeOsuString(writer, data.LifeBarGraph);
                writer.Write(data.Timestamp);
                writer.Write(data.ReplayFrameBytes);
                writer.Write(data.ReplayFrameData);
                writer.Write(data.OnlineID);
            }
        }

        private static void writeOsuString(BinaryWriter writer, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                writer.Write((byte)0x00);
            }
            else
            {
                writer.Write((byte)0x0b);
                byte[] utf8Bytes = Encoding.UTF8.GetBytes(value);
                writeUleb128(writer, (uint)utf8Bytes.Length);
                writer.Write(utf8Bytes);
            }
        }

        private static void writeUleb128(BinaryWriter writer, uint value)
        {
            do
            {
                byte b = (byte)(value & 0x7F);
                value >>= 7;
                if (value != 0) b |= 0x80;
                writer.Write(b);
            } while (value != 0);
        }
    }
}
