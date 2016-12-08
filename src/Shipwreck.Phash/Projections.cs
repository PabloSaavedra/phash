using Shipwreck.Phash.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Shipwreck.Phash
{
    /// <summary>
    /// Radon Projection info
    /// </summary>
    public class Projections
    {
        public Projections(int regionWidth, int regionHeight, int lineCount)
        {
            Region = new Imaging.FloatImage(regionWidth, regionHeight);
            PixelsPerLine = new int[lineCount];
        }

        /// <summary>
        /// contains projections of image of angled lines through center
        /// </summary>
        public FloatImage Region { get; }

        [Obsolete]
        public FloatImage R => Region;

        /// <summary>
        /// int array denoting the number of pixels of each line
        /// </summary>
        public int[] PixelsPerLine { get; }

        [Obsolete]
        public int[] nb_pix_perline => PixelsPerLine;
    }
}