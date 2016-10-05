using System.Diagnostics;
using System.Windows.Forms;

namespace view
{
    public class Program
    {
        public static void Main(string[] args)
        {
            ImageGet fetcher = new ImageGet("http://192.168.0.185:8080/shot.jpg");
            Application.Run(new ImageDisplay(fetcher));
        }
    }
}