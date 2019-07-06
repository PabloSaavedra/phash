﻿using Shipwreck.Phash.Imaging;
using System;

namespace Shipwreck.Phash
{
    public class ImagePhash
    {
        protected ImagePhash()
        { }

        private static float ROUNDING_FACTOR(float x)
            => x >= 0 ? 0.5f : -0.5f;

        private static double ROUNDING_FACTOR(double x)
            => x >= 0 ? 0.5 : -0.5;

        private const double SQRT_TWO = 1.4142135623730950488016887242097;
        protected const double DEFAULT_SIGMA = 3.5;
        protected const double DEFAULT_GAMMA = 1.0;
        protected const int DEFAULT_NUMBER_OF_ANGLES = 180;
        protected const double DEFAULT_THRESHOLD = 0.9;

        #region CompareImages

        /// <summary>
        /// compare 2 images
        /// </summary>
        /// <param name="imA">CImg object of first image</param>
        /// <param name="imB">CImg object of second image</param>
        /// <param name="pcc">double value for peak of cross correlation</param>
        /// <param name="sigma">double value for the deviation of gaussian filter</param>
        /// <param name="gamma">double value for gamma correction of images</param>
        /// <param name="numberOfAngles">int number for the number of angles of radon projections</param>
        /// <param name="threshold">double value for the threshold</param>
        /// <returns>false for different images, 1 true for same image,</returns>
        public static bool CompareImages(IByteImage imA, IByteImage imB, out double pcc, double sigma = DEFAULT_SIGMA, double gamma = DEFAULT_GAMMA, int numberOfAngles = DEFAULT_NUMBER_OF_ANGLES, double threshold = DEFAULT_THRESHOLD)
        {
            var digestA = ComputeDigest(imA, sigma, gamma, numberOfAngles);

            var digestB = ComputeDigest(imB, sigma, gamma, numberOfAngles);

            pcc = GetCrossCorrelation(digestA, digestB);
            return pcc > threshold;
        }

        #endregion CompareImages

        #region ComputeDigest

        /// <summary>
        /// Compute the image digest for an image given the input image
        /// </summary>
        /// <param name="image">CImg object representing an input image</param>
        /// <param name="sigma">double value for the deviation for a gaussian filter function</param>
        /// <param name="gamma">double value for gamma correction on the input image</param>
        /// <param name="numberOfAngles">int value for the number of angles to consider.</param>
        /// <returns></returns>
        public static Digest ComputeDigest(IByteImage image, double sigma = DEFAULT_SIGMA, double gamma = DEFAULT_GAMMA, int numberOfAngles = DEFAULT_NUMBER_OF_ANGLES)
        {
            var blurred = image.Blur(sigma);

            blurred.DivideInplace(blurred.Max());
            blurred.ApplyGamma(gamma);

            var projs = FindRadonProjections(blurred, numberOfAngles);
            var features = ComputeFeatureVector(projs);

            return ComputeDct(features);
        }

        #endregion ComputeDigest

        /// <summary>
        /// return dct matrix, C Return DCT matrix of square size, <paramref name="size" />
        /// </summary>
        /// <param name="size">int denoting the size of the square matrix to create.</param>
        /// <returns>size <paramref name="size" />x<paramref name="size" /> containing the dct matrix</returns>
        internal static FloatImage CreateDctMatrix(int size)
        {
            var ret = new FloatImage(size, size, 1 / (float)Math.Sqrt(size));
            var c1 = (float)Math.Sqrt(2f / size);
            for (var x = 0; x < size; x++)
            {
                for (var y = 1; y < size; y++)
                {
                    ret[x, y] = c1 * (float)Math.Cos((Math.PI / 2 / size) * y * (2 * x + 1));
                }
            }
            return ret;
        }

        /// <summary>
        /// Compute the dct of a given vector
        /// </summary>
        /// <param name="featureVector">vector of input series</param>
        /// <returns>the dct of R</returns>
        public static Digest ComputeDct(Features featureVector)
        {
            var N = featureVector.features.Length;

            var digest = new Digest();

            var R = featureVector.features;
            var D = digest.Coefficents;

            var D_temp = new double[Digest.LENGTH];
            double max = 0.0;
            double min = 0.0;
            for (int k = 0; k < Digest.LENGTH; k++)
            {
                double sum = 0.0;
                for (int n = 0; n < N; n++)
                {
                    double temp = R[n] * Math.Cos((Math.PI * (2 * n + 1) * k) / (2 * N));
                    sum += temp;
                }
                if (k == 0)
                {
                    D_temp[k] = sum / Math.Sqrt(N);
                }
                else
                {
                    D_temp[k] = sum * SQRT_TWO / Math.Sqrt((double)N);
                }
                if (D_temp[k] > max)
                {
                    max = D_temp[k];
                }
                if (D_temp[k] < min)
                {
                    min = D_temp[k];
                }
            }

            for (int i = 0; i < Digest.LENGTH; i++)
            {
                D[i] = (byte)(byte.MaxValue * (D_temp[i] - min) / (max - min));
            }

            return digest;
        }

        #region ComputeDctHash

        /// <summary>
        /// compute dct robust image hash
        /// </summary>
        /// <param name="image">An image to compute DCT hash.</param>
        /// <returns>hash of type ulong</returns>
        public static ulong ComputeDctHash(IByteImage image)
        {
            var img = image.Convolve(new FloatImage(7, 7, 1));

            var resized = img.Resize(32, 32);
            var C = CreateDctMatrix(32);
            var Ctransp = C.Transpose();
            var dctImage = C.Multiply(resized).Multiply(Ctransp);

            var sum = 0f;
            for (var y = 0; y < 8; y++)
            {
                for (var x = 0; x < 8; x++)
                {
                    sum += dctImage[x, y];
                }
            }

            var median = sum / 64f;
            var r = 0ul;
            for (var y = 0; y < 8; y++)
            {
                for (var x = 0; x < 8; x++)
                {
                    r |= dctImage[x, y] > median ? (1ul << (x + 8 * y)) : 0;
                }
            }

            return r;
        }

        #endregion ComputeDctHash

        /// <summary>
        /// Find radon projections of N lines running through the image center for lines angled 0 to 180 degrees from horizontal.
        /// </summary>
        /// <param name="img">CImg src image</param>
        /// <param name="numberOfLines">int number of angled lines to consider.</param>
        /// <returns>Projections struct</returns>
        internal static Projections FindRadonProjections(FloatImage img, int numberOfLines)
        {
            var width = img.Width;
            var height = img.Height;
            int D = (width > height) ? width : height;
            var x_center = width / 2f;
            var y_center = height / 2f;
            var x_off = (int)Math.Floor(x_center + ROUNDING_FACTOR(x_center));
            var y_off = (int)Math.Floor(y_center + ROUNDING_FACTOR(y_center));

            var projs = new Projections(numberOfLines, D, numberOfLines);

            var radonMap = projs.Region;
            var ppl = projs.PixelsPerLine;

            for (var k = 0; k < numberOfLines / 4 + 1; k++)
            {
                var theta = k * Math.PI / numberOfLines;
                var alpha = Math.Tan(theta);
                for (var x = 0; x < D; x++)
                {
                    var y = alpha * (x - x_off);
                    var yd = (int)Math.Floor(y + ROUNDING_FACTOR(y));
                    if ((yd + y_off >= 0) && (yd + y_off < height) && (x < width))
                    {
                        radonMap[k, x] = img[x, yd + y_off];
                        ppl[k] += 1;
                    }
                    if ((yd + x_off >= 0) && (yd + x_off < width) && (k != numberOfLines / 4) && (x < height))
                    {
                        radonMap[numberOfLines / 2 - k, x] = img[yd + x_off, x];
                        ppl[numberOfLines / 2 - k] += 1;
                    }
                }
            }
            var j = 0;
            for (var k = 3 * numberOfLines / 4; k < numberOfLines; k++)
            {
                var theta = k * Math.PI / numberOfLines;
                var alpha = Math.Tan(theta);
                for (var x = 0; x < D; x++)
                {
                    var y = alpha * (x - x_off);
                    var yd = (int)Math.Floor(y + ROUNDING_FACTOR(y));
                    if ((yd + y_off >= 0) && (yd + y_off < height) && (x < width))
                    {
                        radonMap[k, x] = img[x, yd + y_off];
                        ppl[k] += 1;
                    }
                    if ((y_off - yd >= 0) && (y_off - yd < width) && (2 * y_off - x >= 0) && (2 * y_off - x < height) && (k != 3 * numberOfLines / 4))
                    {
                        radonMap[k - j, x] = img[-yd + y_off, -(x - y_off) + y_off];
                        ppl[k - j] += 1;
                    }
                }
                j += 2;
            }

            return projs;
        }

        /// <summary>
        /// compute the feature vector from a radon projection map.
        /// </summary>
        /// <param name="projections">Projections struct</param>
        /// <returns>Features struct</returns>
        internal static Features ComputeFeatureVector(Projections projections)
        {
            var map = projections.Region;
            var ppl = projections.PixelsPerLine;
            var N = ppl.Length;
            var D = map.Height;

            var fv = new Features(N);

            var feat_v = fv.Items;
            var sum = 0.0;
            var sum_sqd = 0.0;
            for (int k = 0; k < N; k++)
            {
                var line_sum = 0.0;
                var line_sum_sqd = 0.0;
                var nb_pixels = ppl[k];
                for (var i = 0; i < D; i++)
                {
                    line_sum += map[k, i];
                    line_sum_sqd += map[k, i] * map[k, i];
                }
                feat_v[k] = nb_pixels > 0 ? (line_sum_sqd / nb_pixels) - (line_sum * line_sum) / (nb_pixels * nb_pixels) : 0;
                sum += feat_v[k];
                sum_sqd += feat_v[k] * feat_v[k];
            }
            var mean = sum / N;
            var var = Math.Sqrt((sum_sqd / N) - (sum * sum) / (N * N));

            for (var i = 0; i < N; i++)
            {
                feat_v[i] = (feat_v[i] - mean) / var;
            }

            return fv;
        }

        /// <summary>
        /// cross correlation for 2 series. Compute the cross correlation of two series vectors
        /// </summary>
        /// <param name="x">Digest struct</param>
        /// <param name="y">Digest struct</param>
        /// <returns>double value the peak of cross correlation</returns>
        public static double GetCrossCorrelation(Digest x, Digest y)
            => GetCrossCorrelation(x.Coefficents, y.Coefficents);

        public static double GetCrossCorrelation(byte[] coefficients1, byte[] coefficients2)
            => CrossCorrelation.GetCrossCorrelationCore(coefficients1, coefficients2, Math.Min(coefficients1.Length, coefficients2.Length));

        public unsafe static double GetCrossCorrelation(byte* coefficients1, byte* coefficients2)
            => CrossCorrelation.GetCrossCorrelationCore(coefficients1, coefficients2, 40);

        public static double GetCrossCorrelation(Span<byte> coefficients1, Span<byte> coefficients2)
            => CrossCorrelation.GetCrossCorrelationCore(coefficients1, coefficients2, Math.Min(coefficients1.Length, coefficients2.Length));

        internal static FloatImage CreateMHKernel(float alpha, float level)
        {
            var sigma = (int)(4 * Math.Pow((float)alpha, (float)level));

            var kernel = new FloatImage(2 * sigma + 1, 2 * sigma + 1);

            for (var y = 0; y < kernel.Width; y++)
            {
                for (var x = 0; x < kernel.Height; x++)
                {
                    var xpos = Math.Pow(alpha, -level) * (x - sigma);
                    var ypos = Math.Pow(alpha, -level) * (y - sigma);
                    var A = xpos * xpos + ypos * ypos;
                    kernel[x, y] = (float)((2 - A) * Math.Exp(-A / 2));
                }
            }
            return kernel;
        }

        #region GetHammingDistance

        public static int GetHammingDistance(long x, long y)
            => GetHammingDistance(x ^ y);

        public static int GetHammingDistance(ulong x, ulong y)
            => GetHammingDistance(x ^ y);

        public static int GetHammingDistance(long v)
            => GetHammingDistance(unchecked((ulong)v));

        public static int GetHammingDistance(ulong v)
        {
            // TODO: (netcoreapp3.0) if (Popcnt.IsSupported) return Popcnt.PopCount(v);

            unchecked
            {
                v = v - ((v >> 1) & 0x5555555555555555UL);
                v = (v & 0x3333333333333333UL) + ((v >> 2) & 0x3333333333333333UL);
                return (int)((((v + (v >> 4)) & 0xF0F0F0F0F0F0F0FUL) * 0x101010101010101UL) >> 56);
            }
        }

        #endregion GetHammingDistance
    }
}