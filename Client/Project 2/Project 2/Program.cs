using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Timers;
using System.Drawing.Drawing2D;

namespace Project_2
{
    class Program
    {
        static System.Timers.Timer LoopTimer;
        public static IPAddress ipaddress = Dns.Resolve("0.0.0.0").AddressList[0];
        public static IPEndPoint server = new IPEndPoint(ipaddress, 60001);
        public static IPEndPoint sender = new IPEndPoint(IPAddress.Parse(GetLocalIPAddress()), 60002);
        public static UdpClient udpSender = new UdpClient(sender);
        public static bool connected;
        public static bool disconnect = false;
        public static TcpClient Tcpclient = new TcpClient();

        public static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    if (ip.ToString()[0] == '6' && ip.ToString()[1] == '4')
                    {
                        return ip.ToString();
                    }
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }

        static void Broadcast(string Message)
        {
            // string to byte array 
            byte[] message = Encoding.ASCII.GetBytes(Message);

            // broadcast to all computers on port 60001
            udpSender.Send(message, message.Length, new IPEndPoint(IPAddress.Broadcast, 60001));
        }

        static void DataReceived(IAsyncResult ar)
        {
            UdpClient c = (UdpClient)ar.AsyncState;
            IPEndPoint receivedIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
            Byte[] receivedBytes = c.EndReceive(ar, ref receivedIpEndPoint);

            // Convert data to ASCII and print in console
            string receivedText = ASCIIEncoding.ASCII.GetString(receivedBytes);
            // do this per package
            if (receivedText == "GotIt") //thisisthefirstbroadcast
            {
                server.Address = receivedIpEndPoint.Address;
                Console.WriteLine("RECIEVED");
            }
            else if (receivedText == "YourLive")
            {
                connected = true;
            }
            else if (receivedText == "YourDown")
            {
                disconnect = true;
            }
            c.BeginReceive(DataReceived, ar.AsyncState);
        }

        static void Disconnect(string messagetoSend)
        {
            // string to byte array 
            byte[] message = Encoding.ASCII.GetBytes(messagetoSend);

            // broadcast to all computers on port 60001
            udpSender.Send(message, message.Length, server);
            connected = false;
        }
        public static Bitmap ResizeImage(Bitmap image, int width, int height)
        {
            var destRect = new Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }
            return destImage;
        }
        static Bitmap screenShot()
        {
            int width = Screen.PrimaryScreen.Bounds.Width;
            int height = Screen.PrimaryScreen.Bounds.Height;
            Bitmap bmp = new Bitmap(width, height);
            Graphics memoryGraphics = Graphics.FromImage(bmp);
            memoryGraphics.CopyFromScreen(0, 0, 0, 0, new Size(width, height));
            return ResizeImage(bmp, bmp.Width, bmp.Height);//USE THIS IF YOU NEED TO CHANGE SCALING RESOLUTION/RESTRICT NUMBER OF BYTES PASSING.
        }

        static byte[] ImageToByte(System.Drawing.Image iImage)
        {
            MemoryStream mMemoryStream = new MemoryStream();
            iImage.Save(mMemoryStream, System.Drawing.Imaging.ImageFormat.Jpeg);
            return mMemoryStream.ToArray();
        }

        static byte[] bstreamChange(byte[] bStream, byte[] sizeStream)
        {
            byte[] temp = new byte[bStream.Length + 4];

            for (int i = 0; i < bStream.Length + 4; i++)
            {
                if (i < 4)
                {
                    temp[i] = sizeStream[i];
                }
                else
                {
                    temp[i] = bStream[i - 4];
                }
            }
            return temp;
        }

        private static void LoopTimeEvent(object source, ElapsedEventArgs e)
        {

        }

        static void Main(string[] args)
        {
            connected = false;
            Broadcast("Hi!");

            UdpClient receiver = new UdpClient(60003);
            receiver.BeginReceive(DataReceived, receiver);

            while (!connected)
            {

            }

            Tcpclient.Connect(server.Address, 60002);
            while (connected)
            {
                while (!Tcpclient.Connected)
                {
                    //Stalls untill server wants to connect
                }
                if (Tcpclient.Connected)
                {
                    //Takes the screenshot and puts it into bytes
                    byte[] bStream = ImageToByte(screenShot());
                    //Gets the size of the screenshot and puts in into bytes
                    byte[] intStream = BitConverter.GetBytes(bStream.Length);
                    //sets up the stream
                    NetworkStream nstream = Tcpclient.GetStream();

                    byte[] combinedStream = bstreamChange(bStream, intStream);

                    //Sends it out
                    nstream.Write(combinedStream, 0, combinedStream.Length);
                }
                System.Threading.Thread.Sleep(25);
            }

            Disconnect("Goodbye");

            while (!disconnect)
            {

            }
        }
    }
}