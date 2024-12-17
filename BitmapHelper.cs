using Avalonia.Media.Imaging;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IPStreamApp
{
    public static class BitmapHelper
    {
        public static Bitmap ToAvaloniaBitmap(Mat mat)
        {
            using var ms = new MemoryStream();
            mat.ToMemoryStream(".bmp").CopyTo(ms);
            ms.Position = 0;

            return new Bitmap(ms);
        }
    }
}
