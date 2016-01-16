using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Open.Nat;
using System.Media;

namespace PoiFT
{
    public partial class Form1 : Form
    {
        public const int appPort = 5378;
        public const string applicationName = "Poi file transfer";
        public Thread myThreadRecieve;

        public Form1()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Get byte[] from string
        /// </summary>
        /// <param name="str">String</param>
        /// <returns>Byte array</returns>
        static byte[] GetBytes(string str)
        {
            byte[] bytes = new byte[str.Length * sizeof(char)];
            System.Buffer.BlockCopy(str.ToCharArray(), 0, bytes, 0, bytes.Length);
            return bytes;
        }

        /// <summary>
        /// Get string from byte[]
        /// </summary>
        /// <param name="bytes">Byte array</param>
        /// <returns>String</returns>
        static string GetString(byte[] bytes)
        {
            char[] chars = new char[bytes.Length / sizeof(char)];
            System.Buffer.BlockCopy(bytes, 0, chars, 0, bytes.Length);
            return new string(chars);
        }

        /// <summary>
        /// Send file via TCP
        /// </summary>
        /// <param name="fileName">Full file name</param>
        /// <param name="IP">IP or hostname</param>
        /// <param name="Port">TCP port</param>
        public void SendTCP(string fileName, string IP, Int32 Port)
        {
            byte[] SendingBuffer = null;
            TcpClient client = null;
            lblStatus.Text = "";
            NetworkStream netstream = null;
            try
            {
                client = new TcpClient(IP, Port);
                lblStatus.Text = "Connected to the Server";
                netstream = client.GetStream();
                FileStream Fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);
                int packetNum = Convert.ToInt32(Math.Ceiling(Convert.ToDouble(Fs.Length) / Convert.ToDouble(1024)));
                SendingBuffer = GetBytes(Path.GetFileName(fileName)); //Send filename to server
                Array.Resize(ref SendingBuffer, 1024);
                System.Buffer.BlockCopy(GetBytes(Fs.Length.ToString()), 0, SendingBuffer, 512, GetBytes(Fs.Length.ToString()).Length); //Send file size to server
                netstream.Write(SendingBuffer, 0, (int)SendingBuffer.Length);
                progressBar1.Maximum = 1000;
                int TotalLength = (int)Fs.Length, CurrentPacketLength, counter = 0;
                for (int i = 0; i < packetNum; i++)
                {
                    if (TotalLength > 1024)
                    {
                        CurrentPacketLength = 1024;
                        TotalLength = TotalLength - CurrentPacketLength;
                    }
                    else
                        CurrentPacketLength = TotalLength;

                    SendingBuffer = new byte[CurrentPacketLength];
                    Fs.Read(SendingBuffer, 0, CurrentPacketLength);
                    netstream.Write(SendingBuffer, 0, (int)SendingBuffer.Length);
                    if (progressBar1.Value >= progressBar1.Maximum)
                        progressBar1.Value = progressBar1.Minimum;
                    counter++;
                    if (counter % 500 == 0 || (packetNum < 500 && counter % 50 == 0))
                    {
                        progressBar1.Value = (int)(1000 * (double)counter / (double)packetNum);
                        lblStatus.Text = ((double)counter / (double)packetNum).ToString("P");
                        lblStatus.Update();
                    }
                    Application.DoEvents();
                }
                progressBar1.Value = 0;
                lblStatus.Text = "Sent " + ((double)Fs.Length / (double)(1024 * 1024)).ToString("F") + " MB";
                Fs.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Error!";
            }
            finally
            {
                if (netstream != null) netstream.Close();
                if (client != null) client.Close();
                progressBar1.Value = 0;
            }
        }

        [STAThread]
        public void ReceiveTCP()
        {
            TcpListener Listener = null;
            try
            {
                Listener = new TcpListener(IPAddress.Any, appPort);
                Listener.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
            }

            byte[] RecData = new byte[1024];
            int RecBytes;
            TcpClient client = null;
            NetworkStream netstream = null;

            while (true)
            {
                Thread.Sleep(5000);
                try
                {
                    if (Listener.Pending())
                    {
                        using (var audioStream = Properties.Resources.poi)
                        {
                            using (var player = new SoundPlayer(audioStream))
                            {
                                player.Play();
                            }
                        }
                        DialogResult result = MessageBox.Show(new Form() { TopMost = true }, "Accept the Incoming File", "Incoming Connection", MessageBoxButtons.YesNo);
                        if (result == System.Windows.Forms.DialogResult.Yes)
                        {
                            client = Listener.AcceptTcpClient();
                            netstream = client.GetStream();
                            string Filename = "";
                            long sizeFile = 1;
                            if ((RecBytes = netstream.Read(RecData, 0, RecData.Length)) > 0)
                            {
                                Filename = GetString(RecData.Take(512).ToArray()).Replace("\0", "");
                                sizeFile = long.Parse(GetString(RecData.Skip(512).Take(512).ToArray()).Replace("\0", ""));
                            }
                            else
                            {
                                MessageBox.Show("Recieve Error", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                this.lblStatus.BeginInvoke((MethodInvoker)delegate() { this.lblStatus.Text = "Recieve error!"; });
                            }
                            string SaveFileName = string.Empty;
                            int counter = 0;
                            SaveFileDialog DialogSave = new SaveFileDialog();
                            DialogSave.Filter = "All files (*.*)|*.*";
                            DialogSave.RestoreDirectory = true;
                            DialogSave.FileName = Filename;
                            DialogSave.Title = "Where do you want to save the file?";
                            DialogSave.InitialDirectory = @"C:/";
                            if (DialogSave.ShowDialog() == DialogResult.OK)
                                SaveFileName = DialogSave.FileName;
                            if (SaveFileName != string.Empty)
                            {
                                long totalrecbytes = 0;
                                FileStream Fs = new FileStream(SaveFileName, FileMode.OpenOrCreate, FileAccess.Write);
                                while ((RecBytes = netstream.Read(RecData, 0, RecData.Length)) > 0)
                                {
                                    Fs.Write(RecData, 0, RecBytes);
                                    totalrecbytes += RecBytes;
                                    counter++;
                                    if (counter % 500 == 0 || ((sizeFile / 1024) < 500 && counter % 50 == 0))
                                    {
                                        this.lblStatus.BeginInvoke((MethodInvoker)delegate() { this.lblStatus.Text = ((double)totalrecbytes / (double)sizeFile).ToString("P"); });
                                        this.progressBar1.BeginInvoke((MethodInvoker)delegate() { this.progressBar1.Value = ((int)(1000 * (double)totalrecbytes / (double)sizeFile)); });
                                        this.progressBar1.BeginInvoke((MethodInvoker)delegate() { this.progressBar1.Update(); });
                                    }
                                }
                                this.lblStatus.BeginInvoke((MethodInvoker)delegate() { this.lblStatus.Text = "Received: " + Filename.Replace("\0", "") + Environment.NewLine + "(" + ((double)totalrecbytes / (double)(1024 * 1024)).ToString("0.00") + " MB)"; });
                                this.progressBar1.BeginInvoke((MethodInvoker)delegate() { this.progressBar1.Value = 0; });
                                Fs.Close();
                            }
                            netstream.Close();
                            client.Close();
                            netstream = null;
                            client = null;
                        }
                        else
                        {
                            client = Listener.AcceptTcpClient();
                            client.Close();
                            client = null;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    this.lblStatus.BeginInvoke((MethodInvoker)delegate() { this.lblStatus.Text = "Recieve error!"; });
                    if (netstream!=null) netstream.Close();
                    if (client!= null) client.Close();
                    client = null;
                    netstream = null;
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                SendTCP(openFileDialog1.FileName, textBox1.Text, 5378);
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            NatF(appPort, applicationName);
            this.textBox1.Text = Properties.Settings.Default.LastNameHost;
            myThreadRecieve = new Thread(ReceiveTCP);
            myThreadRecieve.SetApartmentState(ApartmentState.STA);
            myThreadRecieve.Start();
        }

        /// <summary>
        /// Open requested port via UPnP
        /// </summary>
        /// <param name="port">Port number</param>
        /// <param name="appName">Application name</param>
        private async void NatF(int port, string appName)
        {
            var discoverer = new NatDiscoverer();
            var device = await discoverer.DiscoverDeviceAsync();
            await device.CreatePortMapAsync(new Mapping(Protocol.Tcp, port, port, appName));
        }


        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            myThreadRecieve.Abort();
            Application.Exit();
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.LastNameHost = textBox1.Text;
            Properties.Settings.Default.Save();
        }
    }
}
