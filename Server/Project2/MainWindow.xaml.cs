using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Threading;
using System.Threading;

namespace Project2
{
    /// <summary>
    /// Shares screens 
    /// </summary>
    /// 

    public partial class MainWindow : Window
    {
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);
        int SwitchedYet = 0;
        int receiverPort = 60001; //thisisthe recieving port
        static int TCPPort = 60002; //this port is used for recieveing on tdp
        int SendPort = 60003; //this is the send port
        IPEndPoint[] ClientIPs;
        int size = 0;
        IPEndPoint Client;
        //instantiate blank client endpoint.
        UdpClient receiver;
        Thread FrameThread; // FrameThread
        Thread UDPReceiverThread; //UdpRecieveThread
        UdpClient server = new UdpClient(60003);
        TcpListener TCPServer = new TcpListener(TCPPort);
        static TcpClient client = new TcpClient();
        // create a udp client. and start recieving on that port
        string GetLocalIPAddress()
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
        void UDPRecieve()//caled by thread to recieve udp
        {
            receiver = new UdpClient(receiverPort);
            receiver.BeginReceive(DataReceived, receiver);
        }
        public MainWindow()
        {
            IPEndPoint ServerTCP;
            IPEndPoint Server;
            IPAddress ipAddress; //used for ipaddress storage
            string Serverip;
            InitializeComponent();
            this.Closed += new EventHandler(onclose);
            FrameThread = new Thread(new ThreadStart(UpdateOneFrame));
            UDPReceiverThread = new Thread(new ThreadStart(UDPRecieve));
            //instantiate screen
            Serverip = GetLocalIPAddress(); //gets the ip address
            ipAddress = Dns.Resolve(Serverip).AddressList[0]; //obsoletebutworks
            Server = new IPEndPoint(ipAddress, receiverPort);
            ServerTCP = new IPEndPoint(ipAddress, TCPPort);
            ClientIPs = new IPEndPoint[] { Server, Server, Server, Server, Server, Server, Server, Server, Server, Server, Server, Server, Server, Server, Server, Server, Server, Server, Server, Server, Server, Server, Server, Server, Server, Server, Server, Server, Server, Server, Server, Server };
            Client = new IPEndPoint(ipAddress, SendPort);//^to store all random clients max 30
            Display("Starting Upd receiving on port: " + receiverPort);
            Display("Server Ip: " + Serverip); // display to under left panel
            UDPReceiverThread.Start();
        } // end ctor
        IPAddress localAddr = IPAddress.Parse("0.0.0.0");//the local address
        void onclose(object sender,EventArgs e)
        {
            for (int x = 0; x < size; x++)
            {
                Client.Address = ClientIPs[x].Address;
                server.Send(Encoding.ASCII.GetBytes("YourDown"), 8, Client);
            }
            UDPReceiverThread.Abort();
            FrameThread.Abort();
        }
        void SetupConnection() //sets up connection with tcp
        {
            TCPServer.Start();
            bool x = true;
            while (x)
            {
                if (TCPServer.Pending()) //wait for a pending connection
                {
                    client = TCPServer.AcceptTcpClient();

                    x = false;
                }
            }
        }
        void CloseConnection()
        {
            // Shutdown and end connection
            Thread.Sleep(100);
            try
            {
                client.Close();
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e);
            }
            finally
            {
                // Stop listening for new clients.
                TCPServer.Stop();
            }
        }
        byte[] RecieveAFrame() //recieves 1 frame in bytearray 
        {
            Byte[] Size = new Byte[4];// ends up containing size
            int SIZE;
            NetworkStream stream = client.GetStream();
            Byte[] bytes;
            int numBytesToRead = 4;
            int numBytesRead = 0;
            do
            {
                // Read may return anything from 0 to 4
                int n = stream.Read(Size, numBytesRead, 4);
                numBytesRead += n;
                numBytesToRead -= n;
            } while (numBytesToRead > 0);
            SIZE = BitConverter.ToInt32(Size, 0);// converts byte[4] to int32
            bytes = new Byte[SIZE];
            numBytesToRead = SIZE;
            numBytesRead = 0;
            do
            {
                //Read may return anything from 0 to size
                int n = stream.Read(bytes, numBytesRead, numBytesToRead);
                numBytesRead += n;
                numBytesToRead -= n;
            } while (numBytesRead < SIZE);

            return bytes;
        }
        Bitmap BytesToBitmap(byte[] bytes, Bitmap bmp)
        {
            using (var ms = new MemoryStream(bytes))
            {
                bmp = new Bitmap(ms);
                return bmp;
            }
        }
        void UpdateFromBitmap(Bitmap bmp)
        {
            // use invoke to cause other thread to grab it 
            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)(() =>
            {
                DisplayPanel.Source = ToBitmapSource(bmp);
                DisplayPanel.Stretch = System.Windows.Media.Stretch.Fill;
                DisplayPanel.Visibility = Visibility.Visible;
                bmp.Dispose();
            }));
        }
        BitmapSource ToBitmapSource(Bitmap source)// convert bitmap to bitmap source
        {
            IntPtr hBitmap = source.GetHbitmap();
            BitmapSource result = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, System.Windows.Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            source.Dispose();
            DeleteObject(hBitmap);
            return result;
        }
        void DataReceived(IAsyncResult ar)// async data recieve ( used for udp)
        {
            UdpClient c = (UdpClient)ar.AsyncState;
            IPEndPoint receivedIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
            Byte[] receivedBytes = c.EndReceive(ar, ref receivedIpEndPoint);
            // Convert data to ASCII and print in console
            string receivedText = ASCIIEncoding.ASCII.GetString(receivedBytes);
            // do this per package
            if (receivedText == "Hi!") //thisisthefirstbroadcast
            {
                Thread.Sleep(100);
                Display(receivedText);
                Display("Recieved First Broadcast from" + receivedIpEndPoint);
                Client.Address = receivedIpEndPoint.Address;
                    server.Send(Encoding.ASCII.GetBytes("GotIt"), 5, Client);

                ClientIPs[size++] = Client;
                Update();
            }
            else
            {
                if (receivedText == "GoodBye")
                {
                    Thread.Sleep(100);
                    FrameThread.Abort();
                    FrameThread = new Thread(new ThreadStart(UpdateOneFrame));
                    CloseConnection();
                    Remove(GetLastSegment(receivedIpEndPoint.Address.ToString()));
                    size--;
                    Display(receivedIpEndPoint.Address.ToString() + "shutdown");
                    Update();
                }
            }
            c.BeginReceive(DataReceived, ar.AsyncState);
        }
        string GetLastSegment(string ip)//gets last 3 digits for removeing people when they leave
        {
            int mlem = 0;
            string TEMP = "";
            for (int w = 0; w < ip.Length; w++)
            {
                if (ip[w] == '.')//10.0.0.17
                {
                    mlem++;
                }
                if (mlem == 3)
                {
                    TEMP += ip[w];
                }
            }
            string TEmp = "";
            for (int x = 1; x < TEMP.Length; x++)
            {
                TEmp += TEMP[x];
            }
            return TEmp;
        }
        void Update()//updates left panel 
        {
            this.Dispatcher.Invoke(() =>
            {
                if (size == 0)
                {
                }
                else
                {
                    Button Entry = new Button();
                    Entry.Name = "name" + GetLastSegment(ClientIPs[size].Address.ToString());//+ClientIPs[size].Address.ToString();
                    Entry.Width = 25;
                    Entry.Height = 10;
                    Entry.FontSize = 3;
                    Entry.Margin = new Thickness(5);
                    Entry.Content = ClientIPs[size - 1].Address.ToString();
                    Entry.HorizontalAlignment = HorizontalAlignment.Left;
                    Entry.HorizontalContentAlignment = HorizontalAlignment.Left;
                    Entry.VerticalAlignment = VerticalAlignment.Top;
                    Entry.Click += (sender, args) =>
                    {
                        Switch(Entry.Content.ToString());
                    };
                    LeftPanel.Children.Add(Entry);
                }
            });
        }
        void Remove(String name) //removes a child from left panel
        {
            this.Dispatcher.Invoke(() =>
            {
                var Tempymctemp = (Button)this.FindName("name" + name);
                LeftPanel.Children.Remove(Tempymctemp);
            });
        }
        void Switch(String IP) //this is called on button click
        {
            if (SwitchedYet == 1)
            {
                for (int x = 0; x < size; x++)
                {
                    Client.Address = ClientIPs[x].Address;
                        server.Send(Encoding.ASCII.GetBytes("YourDown"), 8, Client);
                }
                if (FrameThread.IsAlive)
                {
                    FrameThread.Abort();
                }

                FrameThread = new Thread(new ThreadStart(UpdateOneFrame));
                CloseConnection();
            }
            Client.Address = IPAddress.Parse(IP);
            //Client.Address = Dns.Resolve(IP).AddressList[0];
            server.Send(Encoding.ASCII.GetBytes("YourLive"), 8, Client);
            SetupConnection();
            SwitchedYet = 1;

            FrameThread.Start();
        }
        void Display(string content)//this displays in the textbox under leftpanel
        {
            this.Dispatcher.Invoke(() =>
            {
                TextBox.FontSize = 6;
                TextBox.Text += System.Environment.NewLine;
                TextBox.Text += (content);
            });
        }
        void UpdateOneFrame()//1 frame worth of logic
        {
            while (true)
            {
                using (Bitmap bmp = null)
                {
                    UpdateFromBitmap(BytesToBitmap(RecieveAFrame(), bmp));
                    //Thread.Sleep(25);
                }
            }
        }
    }
}
