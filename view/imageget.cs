using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Net.Http;
using System.Threading;

namespace view
{
    public class ImageGet
    {
        public delegate void NewImageEvent(Bitmap image, TimeSpan durration);

        public NewImageEvent NewImage;

        private Thread _thread;

        public ImageGet(string url)
        {
            _thread = new Thread(ThreadFetcher);
            _thread.Start(url);
        }

        private async void ThreadFetcher(object o)
        {
            string url = (string) o;
            HttpClient client = new HttpClient();
            Stopwatch sw = new Stopwatch();

            while (true)
            {
                sw.Restart();
                Stream stream = await client.GetStreamAsync(url);
                Bitmap image = new Bitmap(stream);


                PushImage(image, sw.Elapsed);
            }

        }

        private void PushImage(Bitmap image, TimeSpan durration)
        {
            NewImage?.Invoke(image, durration);
        }
    }
}