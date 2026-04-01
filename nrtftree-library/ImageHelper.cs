/********************************************************************************
 *   This file is part of NRtfTree Library.
 *
 *   NRtfTree Library is free software; you can redistribute it and/or modify
 *   it under the terms of the GNU Lesser General Public License as published by
 *   the Free Software Foundation; either version 3 of the License, or
 *   (at your option) any later version.
 *
 *   NRtfTree Library is distributed in the hope that it will be useful,
 *   but WITHOUT ANY WARRANTY; without even the implied warranty of
 *   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *   GNU Lesser General Public License for more details.
 *
 *   You should have received a copy of the GNU Lesser General Public License
 *   along with this program. If not, see <http://www.gnu.org/licenses/>.
 ********************************************************************************/

using System;
using System.IO;

namespace Net.Sgoliver.NRtfTree.Util
{
    /// <summary>
    /// Cross-platform helper to read image dimensions from raw file headers.
    /// Supports JPEG, PNG and BMP without any external dependencies.
    /// </summary>
    internal static class ImageHelper
    {
        /// <summary>
        /// Reads pixel width and height from a JPEG, PNG or BMP file.
        /// Returns (-1, -1) if the format is not recognized.
        /// </summary>
        public static (int Width, int Height) GetImageDimensions(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            int toRead = (int)Math.Min(32, fs.Length);
            byte[] header = new byte[toRead];
            ReadFully(fs, header, 0, toRead);

            // PNG: 8-byte signature + 4-byte length + "IHDR" + 4-byte width (BE) + 4-byte height (BE)
            if (header.Length >= 24 &&
                header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
            {
                int w = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
                int h = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
                return (w, h);
            }

            // BMP: "BM" magic + ... + width (4-byte LE at offset 18) + height (4-byte LE at offset 22)
            if (header.Length >= 26 && header[0] == 0x42 && header[1] == 0x4D)
            {
                int w = header[18] | (header[19] << 8) | (header[20] << 16) | (header[21] << 24);
                int h = header[22] | (header[23] << 8) | (header[24] << 16) | (header[25] << 24);
                return (w, Math.Abs(h)); // height can be negative (top-down DIB)
            }

            // JPEG: scan for SOF0/SOF1/SOF2 markers (FF Cx) which contain height+width
            if (header.Length >= 2 && header[0] == 0xFF && header[1] == 0xD8)
            {
                fs.Seek(2, SeekOrigin.Begin);
                return ReadJpegDimensions(fs);
            }

            return (-1, -1);
        }

        private static (int Width, int Height) ReadJpegDimensions(Stream s)
        {
            byte[] buf = new byte[4];
            while (s.Position < s.Length - 4)
            {
                if (s.ReadByte() != 0xFF) continue;

                int marker = s.ReadByte();
                if (marker < 0) break;

                // SOF markers: C0-C3, C5-C7, C9-CB, CD-CF
                bool isSof = (marker >= 0xC0 && marker <= 0xC3) ||
                             (marker >= 0xC5 && marker <= 0xC7) ||
                             (marker >= 0xC9 && marker <= 0xCB) ||
                             (marker >= 0xCD && marker <= 0xCF);

                if (isSof)
                {
                    s.Seek(3, SeekOrigin.Current); // skip length + precision
                    ReadFully(s, buf, 0, 4);
                    int h = (buf[0] << 8) | buf[1];
                    int w = (buf[2] << 8) | buf[3];
                    return (w, h);
                }

                // Skip segment: read 2-byte length and jump past it
                ReadFully(s, buf, 0, 2);
                int segLen = (buf[0] << 8) | buf[1];
                if (segLen > 2) s.Seek(segLen - 2, SeekOrigin.Current);
            }
            return (-1, -1);
        }

        private static void ReadFully(Stream s, byte[] buffer, int offset, int count)
        {
            int remaining = count;
            while (remaining > 0)
            {
                int read = s.Read(buffer, offset, remaining);
                if (read == 0) break;
                offset += read;
                remaining -= read;
            }
        }
    }
}
