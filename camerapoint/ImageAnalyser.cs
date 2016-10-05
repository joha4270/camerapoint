using System;
using System.Configuration;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using System.Security.Policy;

namespace camerapoint
{
    public class ImageAnalyser
    {
        public int MaxBlobCount { get; set; } = 1024 * 16;
        public int Difference { get; set; } = 256*3;
        public BlobsData GetBlobs(Bitmap image)
        {
            if (image.PixelFormat != PixelFormat.Format24bppRgb)
            {
                throw new ArgumentException("Only supports Format24bppRgb");
            }

            BitmapData data = image.LockBits(new Rectangle(Point.Empty, image.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb); //fuck reading anything but whole ints atm


            int processedCount = 0;
            int usedBlobCount = 0;
            Blob[] blobs = new Blob[MaxBlobCount];
            for (int i = 0; i < blobs.Length; i++) { blobs[i].Init(); }
            int[] blopIndex = new int[data.Height * data.Width];

            //Data is in 32 bit per pixel, with RGB 8 bits each + first byte 0xff
            unsafe
            {
                int* pixelPointer = (int*)data.Scan0.ToPointer();
                int pixelData;
                /*
                 Image is processed in parts divided like

                ABBB...BBB
                CDDD...DDD
                CDDD...DDD
                ..........
                ..........
                CDDD...DDD
                CDDD...DDD

                A cannot do any processing. It has no processing as no other pixels has been checked
                B can compare with the left neighbour
                C can compare with upper but not left neighbour
                D can compare with upper and left neighbour
                */

                //Process A
                pixelData = GetPixelData(pixelPointer, 0, 0, data.Stride);
                blobs[usedBlobCount].CheckAdd(pixelData, Difference);
                blopIndex[0] = usedBlobCount;
                processedCount++;

                //Process B (Start at 1, A processed)
                for (int x = 1; x < data.Width; x++)
                {
                    pixelData = GetPixelData(pixelPointer, x, 0, data.Stride);
                    if (blobs[blopIndex[x - 1]].CheckAdd(pixelData, Difference))
                    {
                        blopIndex[x] = blopIndex[x - 1];
                    }
                    else
                    {
                        usedBlobCount++;
                        if(usedBlobCount == blobs.Length) throw new Exception(
                            $"B ran out of blobs at {x}({processedCount})/{blopIndex.Length}");
                        blobs[usedBlobCount].CheckAdd(pixelData, Difference);
                        blopIndex[x] = usedBlobCount;
                    }
                    processedCount++;
                }

                //First line processed (Where nothing above exists)

                for (int y = 1; y < data.Height; y++)
                {
                    //Process C
                    pixelData = GetPixelData(pixelPointer, 0, y, data.Stride);
                    if (blobs[blopIndex[(y - 1) * data.Width]].CheckAdd(pixelData, Difference))
                    {
                        blopIndex[y * data.Width] = blopIndex[(y - 1) * data.Width];
                    }
                    else
                    {
                        usedBlobCount++;
                        if(usedBlobCount == blobs.Length) throw new Exception($"C ran out of blobs at {y * data.Width}{processedCount})/{blopIndex.Length}");
                        blobs[usedBlobCount].CheckAdd(pixelData, Difference);
                        blopIndex[y * data.Width] = usedBlobCount;
                    }

                    processedCount++;

                    for (int x = 1; x < data.Width; x++)
                    {
                        //Process D
                        pixelData = GetPixelData(pixelPointer, x, y, data.Stride);
                        if (blobs[blopIndex[y * data.Width + x - 1]].CheckAdd(pixelData, Difference))
                        {
                            blopIndex[y * data.Width + x] = blopIndex[y * data.Width + x - 1];
                        }
                        else if (blobs[blopIndex[(y - 1) * data.Width + x]].CheckAdd(pixelData, Difference))
                        {
                            blopIndex[y * data.Width + x] = blopIndex[(y - 1) * data.Width + x];
                        }
                        else
                        {
                            usedBlobCount++;
                            if(usedBlobCount == blobs.Length) throw new Exception(
                                $"D ran out of blobs at {y * data.Width+x}({processedCount})/{blopIndex.Length}");
                            blobs[usedBlobCount].CheckAdd(pixelData, Difference);
                            blopIndex[x] = usedBlobCount;
                        }

                        processedCount++;
                    }
                }
            }

            image.UnlockBits(data);
            return new BlobsData(data.Width, data.Height, usedBlobCount, blobs, blopIndex);
        }

        private unsafe int GetPixelData(void* image, int x, int y, int stride)
        {
            int raw = *((int*) image + (x) + (y * stride / 4));

            return raw & 0x00FFFFFF;
        }

        public Bitmap DrawBlobs(Bitmap underlayer, BlobsData blobs, VisibilityMode visibilityMode)
        {

            if(visibilityMode != VisibilityMode.Solid && underlayer == null) throw new ArgumentException("visibility can only be solid if no bitmap is provided");

            if(visibilityMode != VisibilityMode.Solid) throw new NotImplementedException("Only visibilityMode.Solid is supported");

            Bitmap bitmap = new Bitmap(underlayer);
            BitmapData data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.ReadWrite,
                PixelFormat.Format32bppRgb);

            unsafe
            {
                for (int i = 0; i < blobs.BlobIndex.Length; i++)
                {
                    int* prt = (int*)data.Scan0.ToPointer();
                    *(prt + i) = (0-16777216)  | blobs.Blons[blobs.BlobIndex[i]].Color();
                }
            }

            bitmap.UnlockBits(data);
            return bitmap;
        }
    }

    public enum VisibilityMode
    {
        Solid,
        Transparent,
        Outline
    }

    public class BlobsData
    {
        public BlobsData(int width, int height, int blobCount, Blob[] blons, int[] blobIndex)
        {
            Width = width;
            Height = height;
            BlobCount = blobCount;
            Blons = blons;
            BlobIndex = blobIndex;
        }

        public int Width { get; }
        public int Height { get; }

        public int BlobCount { get; }
        public Blob[] Blons { get; }
        public int[] BlobIndex { get; }

    }

    public struct Blob
    {
        internal void Init()
        {
            lor = log= lob = 255;
            hir = hig = hib = 0;
        }

        private byte lor, log, lob;
        private byte hir, hig, hib;

        internal bool CheckAdd(int rawColor, int maxDelta)
        {
            //Way this method works (supposedly) is it keeps track of max and min seen rgb values.
            //If a value falls inside this range, it is Allowed (true)
            //If it falls outside, it is checked if it can be added, without moving max and min from eachother with more than maxDelta
            //if possible max/min is updated and it is Allowed
            //otherwise it is disallowed

            byte inr, ing, inb;
            ToBytes(rawColor, out inr, out ing, out inb);
            if (lor == 255)
            {
                lor = hir = inr;
                log = hig = ing;
                lob = hib = inb;
                return true;
            }


            if (hir > inr && inr > lor && hig > ing && ing > log && hib > inb && inb > lob) return true;


            int olddif = (hir - lor) + (hig - log) + (hib - lob);

            int fatrh = Math.Max(0, inr - hir); int fatrl = Math.Max(0, lor - inr);
            int fatgh = Math.Max(0, ing - hig); int fatgl = Math.Max(0, log - ing);
            int fatbh = Math.Max(0, inb - hib); int fatbl = Math.Max(0, lob - inb);

            int fat = fatrh + fatrl + fatgh + fatgl + fatbh + fatbl;
            if (fat + olddif > maxDelta)
                return false;

            hir = Math.Max(hir, inr);
            hig = Math.Max(hig, ing);
            hib = Math.Max(hib, inb);

            lor = Math.Min(lob, inr);
            log = Math.Min(log, ing);
            lob = Math.Min(lob, inb);

            return true;
        }

        private void ToBytes(int input, out byte red, out byte green, out byte blue)
        {
            blue = (byte)(input & 0x0000ff);
            green = (byte)((input & 0x00ff00) >> 8);
            red = (byte)((input & 0xff0000) >> 16);
        }

        public int Color()
        {
            int t = (hir + lor) / 2;
            int ret = t << 16;

            t = (hig + log) / 2;
            ret |= t << 8;

            t = (hib + lob) / 2;
            ret |= t;

            return t;
        }
    }
}


