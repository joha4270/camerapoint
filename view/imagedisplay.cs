using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using camerapoint;

namespace view
{
    public class ImageDisplay : Form
    {
        #region Controls

        private CheckBox _renderBlobs;
        private NumericUpDown _maxDelta;
        #endregion

        private Font FpsFont;
        private const int SIDEBAR_SIZE = 130;
        private readonly ImageGet _imageSource;
        private ImageAnalyser _analyser = new ImageAnalyser();
        private Graphics _graphics;
        public ImageDisplay(ImageGet imageSource)
        {
            this.Size = new Size(130 + (1920 / 2), 1080 / 2);

            _renderBlobs = new CheckBox
            {
                Text = "Render blobs",
                Checked = false,
                Location = new Point(3, 3)
            };
            Controls.Add(_renderBlobs);

            _maxDelta = new NumericUpDown
            {

                Location = new Point(3, 30),
                Minimum = 0,
                Maximum = 260*3
            };
            _maxDelta.Value = _analyser.Difference;
            _maxDelta.ValueChanged += MaxDeltaOnValueChanged;
            Controls.Add(_maxDelta);

            this.Resize += OnResize;


            FpsFont = new Font(DefaultFont.FontFamily, 20);
            _imageSource = imageSource;
            _graphics = this.CreateGraphics();
            _imageSource.NewImage += NewImage;


        }

        private void MaxDeltaOnValueChanged(object sender, EventArgs eventArgs)
        {
            _analyser.Difference = (int) _maxDelta.Value;
        }

        private void OnResize(object sender, EventArgs eventArgs)
        {
            _graphics?.Dispose();
            _graphics = this.CreateGraphics();
        }

        private void NewImage(Bitmap image, TimeSpan durration)
        {
            if (InvokeRequired)
            {
                Invoke(new ImageGet.NewImageEvent(NewImage), image, durration);
                return;
            }

            //_graphics.FillRegion(new SolidBrush(Color.DarkGreen), new Region(GetDrawBounds()));

            if (_renderBlobs.Checked)
            {
                BlobsData blobs = _analyser.GetBlobs(image);
                Image blobimg = _analyser.DrawBlobs(image, blobs, VisibilityMode.Solid);
                blobimg.Save("test.jpg");
                _graphics.DrawImage(blobimg, GetDrawBounds());
            }
            else
            {
                _graphics.DrawImage(image, GetDrawBounds());
            }


            _graphics.DrawString(
                $"{1 / durration.TotalSeconds:F1} FPS",
                FpsFont,
                new SolidBrush(Color.Green),
                new PointF(SIDEBAR_SIZE + 3, 3)
            );
        }

        private RectangleF GetDrawBounds()
        {
            int wfromw = ClientRectangle.Width - SIDEBAR_SIZE;
            int wfromh = (ClientRectangle.Height * 16) / 9;
            int hfromh = ClientRectangle.Height;
            int hfromw = ((ClientRectangle.Width - SIDEBAR_SIZE) / 16) * 9;

            return new Rectangle(SIDEBAR_SIZE, 0, Math.Min(wfromw, wfromh), Math.Min(hfromh, hfromw));
        }
    }


}