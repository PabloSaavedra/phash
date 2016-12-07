using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Shipwreck.Phash
{
    public sealed class ByteImage
    {
        private readonly int _Width;
        private readonly int _Height;
        private readonly byte[] _Data;

        public ByteImage(int width, int height)
        {
            _Width = width;
            _Height = height;
            _Data = new byte[width * height];
        }

        public ByteImage(int width, int height, byte value)
        {
            _Width = width;
            _Height = height;
            _Data = new byte[width * height];
            for (var i = 0; i < _Data.Length; i++)
            {
                _Data[i] = value;
            }
        }

        public ByteImage(int width, int height, byte[] data)
        {
            _Width = width;
            _Height = height;
            _Data = data;
        }

        public int Width => _Width;
        public int Height => _Height;

        public byte this[int x, int y]
        {
            get
            {
                var i = x + y * _Width;
                return _Data[i];
            }
            set
            {
                var i = x + y * _Width;
                _Data[i] = value;
            }
        }

        public FloatImage Convolve(FloatImage k)
        {
            var kw = k.Width;
            var kh = k.Height;
            var kxs = kw >> 1;
            var kys = kh >> 1;

            var r = new FloatImage(_Width, _Height);

            for (var dy = 0; dy < _Height; dy++)
            {
                for (var dx = 0; dx < _Width; dx++)
                {
                    var v = 0f;
                    var sum = 0f;
                    for (var ky = 0; ky < kh; ky++)
                    {
                        var sy = dy + ky - kys;
                        if (sy < 0 || _Height <= sy)
                        {
                            continue;
                        }

                        for (var kx = 0; kx < kh; kx++)
                        {
                            var sx = dx + kx - kxs;
                            if (sx < 0 || _Width <= sx)
                            {
                                continue;
                            }
                            var sv = this[sx, sy];
                            var kv = k[kx, ky];
                            v += sv * kv;
                            sum += kv;
                        }
                    }

                    r[dx, dy] = v / sum;
                }
            }

            return r;
        }
    }
}