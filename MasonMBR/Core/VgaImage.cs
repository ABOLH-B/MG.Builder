using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace MasonMBR.Core
{
    static class VgaImage
    {
        const int MAX_COLORS = 8000;

        static readonly Color[] Pal = BuildPalette();

        const int MAX_FRAMES = 100;

        internal static bool TryConvertGif(string path, out byte[][] frames, out int[] delays, out string err)
        {
            frames = null;
            delays = null;
            err    = null;

            Image img;
            try { img = Image.FromFile(path); }
            catch (Exception ex) { err = ex.Message; return false; }

            using (img)
            {
                if (img.FrameDimensionsList == null || img.FrameDimensionsList.Length == 0)
                    return false;

                var dim   = new FrameDimension(img.FrameDimensionsList[0]);
                int count = img.GetFrameCount(dim);

                if (count <= 1) return false;

                if (count > MAX_FRAMES) count = MAX_FRAMES;

                int[] rawDelay = new int[count];
                try
                {
                    var pi = img.GetPropertyItem(0x5100);
                    for (int i = 0; i < count; i++)
                    {
                        int off = i * 4;
                        rawDelay[i] = (off + 4 <= pi.Value.Length)
                            ? BitConverter.ToInt32(pi.Value, off)
                            : 10;
                    }
                }
                catch { for (int i = 0; i < count; i++) rawDelay[i] = 10; }

                var outFrames = new byte[count][];
                var outDelays = new int[count];

                for (int i = 0; i < count; i++)
                {
                    img.SelectActiveFrame(dim, i);

                    Bitmap frameSnap = new Bitmap(img.Width, img.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    Bitmap bmp = null;
                    try
                    {
                        using (var gf = System.Drawing.Graphics.FromImage(frameSnap))
                            gf.DrawImage(img, 0, 0, img.Width, img.Height);
                        bmp = Scale(frameSnap);
                    }
                    finally
                    {
                        frameSnap.Dispose();
                    }
                    int stride;
                    byte[] raw = ReadRaw(bmp, out stride);
                    bmp.Dispose();

                    int uniq;
                    if (!SimpleEnough(raw, stride, out uniq))
                    {
                        err = "Frame " + (i + 1) + " is too complex (" + uniq + "+ unique colors).\n\n"
                            + "Please choose a simpler GIF:\n"
                            + "  - Logos, icons, or simple animations\n"
                            + "  - Flat color artwork";
                        return false;
                    }

                    outFrames[i] = Quantize(raw, stride);
                    outDelays[i] = rawDelay[i];
                }

                frames = outFrames;
                delays = outDelays;
                return true;
            }
        }

        internal static bool TryConvert(string path, out byte[] pixels, out string err)
        {
            pixels = null;
            err = null;

            Image src;
            try { src = Image.FromFile(path); }
            catch (Exception ex) { err = ex.Message; return false; }

            using (src)
            {
                var bmp = Scale(src);

                int stride;
                byte[] raw = ReadRaw(bmp, out stride);
                bmp.Dispose();

                int uniq;
                if (!SimpleEnough(raw, stride, out uniq))
                {
                    err = "Image is too complex (" + uniq + "+ unique colors).\n\n"
                        + "Please choose a simpler image:\n"
                        + "  - Logos, icons, or simple illustrations\n"
                        + "  - Flat color artwork\n"
                        + "  - Reduce image detail first";
                    return false;
                }

                pixels = Quantize(raw, stride);

                if (AllBlack(pixels))
                {
                    err = "Image appears blank or completely black";
                    pixels = null;
                    return false;
                }

                return true;
            }
        }

        internal static Bitmap Preview(byte[] pixels)
        {
            var bmp = new Bitmap(320, 200, PixelFormat.Format32bppArgb);
            var bd = bmp.LockBits(new Rectangle(0, 0, 320, 200),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            byte[] buf = new byte[bd.Stride * 200];
            for (int y = 0; y < 200; y++)
            {
                for (int x = 0; x < 320; x++)
                {
                    var c = Pal[pixels[y * 320 + x]];
                    int off = y * bd.Stride + x * 4;
                    buf[off]     = c.B;
                    buf[off + 1] = c.G;
                    buf[off + 2] = c.R;
                    buf[off + 3] = 255;
                }
            }

            Marshal.Copy(buf, 0, bd.Scan0, buf.Length);
            bmp.UnlockBits(bd);
            return bmp;
        }

        static Bitmap Scale(Image src)
        {
            var dst = new Bitmap(320, 200, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(dst))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode   = PixelOffsetMode.HighQuality;
                g.Clear(Color.Black);

                float s = Math.Min(320f / src.Width, 200f / src.Height);
                int w = (int)(src.Width * s);
                int h = (int)(src.Height * s);
                g.DrawImage(src, (320 - w) / 2, (200 - h) / 2, w, h);
            }
            return dst;
        }

        static byte[] ReadRaw(Bitmap bmp, out int stride)
        {
            var bd = bmp.LockBits(new Rectangle(0, 0, 320, 200),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            stride = bd.Stride;
            byte[] raw = new byte[stride * 200];
            Marshal.Copy(bd.Scan0, raw, 0, raw.Length);
            bmp.UnlockBits(bd);
            return raw;
        }

        static bool SimpleEnough(byte[] raw, int stride, out int uniqueCount)
        {
            var seen = new HashSet<int>(MAX_COLORS + 1);
            bool over = false;

            for (int y = 0; y < 200 && !over; y++)
            {
                for (int x = 0; x < 320; x++)
                {
                    int off = y * stride + x * 4;
                    seen.Add(raw[off + 2] << 16 | raw[off + 1] << 8 | raw[off]);
                    if (seen.Count > MAX_COLORS) { over = true; break; }
                }
            }

            uniqueCount = seen.Count;
            return !over;
        }

        static byte[] Quantize(byte[] raw, int stride)
        {
            var result = new byte[64000];
            for (int y = 0; y < 200; y++)
            {
                for (int x = 0; x < 320; x++)
                {
                    int off = y * stride + x * 4;
                    result[y * 320 + x] = Nearest(raw[off + 2], raw[off + 1], raw[off]);
                }
            }
            return result;
        }

        static byte Nearest(byte r, byte g, byte b)
        {
            byte best = 0;
            int min = int.MaxValue;

            for (int i = 0; i < Pal.Length; i++)
            {
                int dr = r - Pal[i].R;
                int dg = g - Pal[i].G;
                int db = b - Pal[i].B;

                int d = (dr * dr) + (dg * dg) + (db * db);

                if (d < min)
                {
                    min = d;
                    best = (byte)i;
                }
                if (d == 0) break;
            }

            return best;
        }

        static bool AllBlack(byte[] px)
        {
            foreach (byte b in px)
                if (b != 0) return false;
            return true;
        }

        static Color[] BuildPalette()
        {
            var p = new Color[256];

            byte[,] vga16 = {
                {0,0,0},     {0,0,168},   {0,168,0},   {0,168,168},
                {168,0,0},   {168,0,168}, {168,84,0},  {168,168,168},
                {84,84,84},  {84,84,252}, {84,252,84}, {84,252,252},
                {252,84,84}, {252,84,252},{252,252,84},{252,252,252}
            };

            for (int i = 0; i < 16; i++)
                p[i] = Color.FromArgb(vga16[i, 0], vga16[i, 1], vga16[i, 2]);

            for (int i = 0; i < 16; i++)
            {
                int v = i * 16;
                p[16 + i] = Color.FromArgb(v, v, v);
            }

            int idx = 32;
            int[] lv = { 0, 42, 84, 126, 168, 210 };
            for (int r = 0; r < 6; r++)
                for (int g = 0; g < 6; g++)
                    for (int b = 0; b < 6; b++)
                        p[idx++] = Color.FromArgb(lv[r], lv[g], lv[b]);

            int[] grays = { 8, 24, 40, 56, 72, 88, 104, 120 };
            for (int i = 0; i < 8; i++)
                p[248 + i] = Color.FromArgb(grays[i], grays[i], grays[i]);

            return p;
        }
    }
}
