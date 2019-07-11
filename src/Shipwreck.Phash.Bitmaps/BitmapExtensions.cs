﻿using Shipwreck.Phash.Imaging;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Shipwreck.Phash.Bitmaps
{
    public static class BitmapExtensions
    {
        internal static Bitmap ToRgb24(this Bitmap bitmap)
        {
            if (bitmap.PixelFormat == PixelFormat.Format24bppRgb)
                return bitmap;
            Bitmap drawingBitmap = null;
            try
            {
                drawingBitmap = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format24bppRgb);
                drawingBitmap.SetResolution(bitmap.HorizontalResolution, bitmap.VerticalResolution);
                using (var graphics = Graphics.FromImage(drawingBitmap))
                {
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.DrawImage(bitmap, 0, 0);
                }
                return drawingBitmap;
            }
            catch (Exception)
            {
                drawingBitmap?.Dispose();
                throw;
            }
        }

        public static ByteImage ToLuminanceImage(this Bitmap bitmap)
        {
            Bitmap bitmap24Rgb = null;
            try
            {
                bitmap24Rgb = bitmap.ToRgb24();

                var data = bitmap24Rgb.ToBytes();

                var r = new ByteImage(bitmap24Rgb.Width, bitmap24Rgb.Height);

                int bytesPerPixel = (Image.GetPixelFormatSize(bitmap24Rgb.PixelFormat) + ((sizeof(byte) * 8) - 1)) / (sizeof(byte) * 8);
                int strideDelta = bitmap24Rgb.GetStride() % (bitmap24Rgb.Width * bytesPerPixel);
                var yc = new Vector3(66, 129, 25);
                var i = 0;
                for (var dy = 0; dy < r.Height; dy++)
                {
                    for (var dx = 0; dx < r.Width; dx++)
                    {
                        Vector3 sv;
                        sv.Z = data[i++]; // B
                        sv.Y = data[i++]; // G
                        sv.X = data[i++]; // R

                        r[dx, dy] = (byte)(((int)(Vector3.Dot(yc, sv) + 128) >> 8) + 16);
                    }

                    i += strideDelta;
                }

                return r;
            }
            finally
            {
                if (bitmap != bitmap24Rgb)
                {
                    bitmap24Rgb?.Dispose();
                }
            }
        }

        public static ByteImage ToRedImage(this Bitmap src)
            => src.ToChannelImage(2);

        public static ByteImage ToGreenImage(this Bitmap src)
            => src.ToChannelImage(1);

        public static ByteImage ToBlueImage(this Bitmap src)
            => src.ToChannelImage(0);

        private static ByteImage ToChannelImage(this Bitmap bitmap, int offset)
        {
            Bitmap bitmap24Rgb = null;
            try
            {
                bitmap24Rgb = bitmap.ToRgb24();

                var data = bitmap24Rgb.ToBytes();

                var r = new ByteImage(bitmap24Rgb.Width, bitmap24Rgb.Height);

                var stride = bitmap24Rgb.GetStride();
                for (var dy = 0; dy < r.Height; dy++)
                {
                    for (var dx = 0; dx < r.Width; dx++)
                    {
                        r[dx, dy] = data[dx * 3 + stride * dy + offset];
                    }
                }

                return r;
            }
            finally
            {
                if (bitmap != bitmap24Rgb)
                {
                    bitmap24Rgb?.Dispose();
                }
            }
        }

        /// <summary>
        /// Copies the bitmap to its raw bytes format with stride bytes.
        /// </summary>
        /// <param name="bitmap">bitmap to convert</param>
        /// <returns>Raw byte array with stride bytes</returns>
        public static byte[] ToBytes(this Bitmap bitmap)
        {
            BitmapData lockedBits = null;
            try
            {
                lockedBits = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, bitmap.PixelFormat);
                int sizeInBytes = lockedBits.Stride * lockedBits.Height;
                byte[] rawPixelByteData = new byte[sizeInBytes];
                Marshal.Copy(lockedBits.Scan0, rawPixelByteData, 0, sizeInBytes);

                return rawPixelByteData;
            }
            finally
            {
                if (lockedBits != null)
                    bitmap.UnlockBits(lockedBits);
            }
        }

        public static RawBitmapData ToRawBitmapData(this Bitmap bitmap)
            => RawBitmapData.FromBitmap(bitmap);

        public static int GetStride(this Bitmap bitmap)
        {
            int bitsPerPixel = ((int)bitmap.PixelFormat & 0xff00) >> 8;
            int stride = 4 * ((bitmap.Width * bitsPerPixel + 31) / 32);
            return stride;
        }

        [DllImport("gdi32")]
        private static extern int DeleteObject(IntPtr o);

        public static Bitmap ToBitmap(this Image image, PixelFormat format = PixelFormat.DontCare)
        {
            var bitmap = new Bitmap(image.Width, image.Height, format == PixelFormat.DontCare ? image.PixelFormat : format);
            try
            {
                bitmap.SetResolution(image.HorizontalResolution, image.VerticalResolution);
                using (var g = Graphics.FromImage(bitmap))
                    g.DrawImage(image, 0, 0);
                return bitmap;
            }
            catch (Exception)
            {
                bitmap?.Dispose();
                throw;
            }
        }
    }
}