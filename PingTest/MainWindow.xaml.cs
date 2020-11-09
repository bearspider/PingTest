using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PingTest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ObservableCollection<IPObject> ipcollection = new ObservableCollection<IPObject>();
        private readonly SynchronizationContext synccontext;
        private object _iplock = new object();
        private int ipcounter = 0;        

        // Create a buffer of 32 bytes of data to be transmitted.
        private string bufferdata = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        
        // Wait 1 seconds for a reply.
        int timeout = 1000;
        public MainWindow()
        {
            InitializeComponent();
            BindingOperations.EnableCollectionSynchronization(ipcollection, _iplock);
            synccontext = SynchronizationContext.Current;
            dgOutput.ItemsSource = ipcollection;
            statusbarIPs.Content = ipcounter;
            buttonExport.IsEnabled = false;
            
    }
        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (IPAddress.Parse(textStart.Text).Compare(IPAddress.Parse(textEnd.Text)) == -1)
                {
                    buttonStart.IsEnabled = false;
                    buttonExport.IsEnabled = false;
                    statusbarComplete.Visibility = Visibility.Hidden;
                    ipcollection.Clear();
                    ResetCount();
                    BackgroundWorker bw = new BackgroundWorker();
                    bw.DoWork += new DoWorkEventHandler(ProcessIP);
                    bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(ProcessCompleted);
                    ArrayList arguments = new ArrayList();
                    arguments.Add(textStart.Text);
                    arguments.Add(textEnd.Text);
                    arguments.Add(checkResolve.IsChecked);
                    bw.RunWorkerAsync(argument: arguments);
                }
                else
                {
                    MessageBoxResult result = Xceed.Wpf.Toolkit.MessageBox.Show("Invalid IP Range!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (FormatException)
            {
                MessageBoxResult result = Xceed.Wpf.Toolkit.MessageBox.Show("Invalid IP Address", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        public void ProcessIP(object sender, DoWorkEventArgs e)
        {
            bool loop = true;
            bool finalrun = false;
            
            ArrayList values = (ArrayList)e.Argument;
            IPAddress startip = IPAddress.Parse(values[0].ToString());
            IPAddress currentip = startip;
            IPAddress endip = IPAddress.Parse(values[1].ToString());
            bool dnscheck = (bool)values[2];

            // Set options for transmission:
            // The data can go through 64 gateways or routers
            // before it is destroyed, and the data packet
            // cannot be fragmented.
            byte[] buffer = Encoding.ASCII.GetBytes(bufferdata);
            PingOptions options = new PingOptions(64, true);


            while (loop)
            {
                IPObject newobject = new IPObject();
                newobject.IP = currentip;
                ipcollection.Add(newobject);
                currentip = Increment(currentip);
                if (finalrun)
                {
                    loop = false;
                }
                if(currentip.ToString() == endip.ToString())
                {
                    finalrun = true;
                }
            }
            Parallel.ForEach(ipcollection, nodeip =>
            {
                // Send the request.
                Ping pingSender = new Ping();
                PingReply reply = pingSender.Send(nodeip.IP, timeout, buffer, options);

                if (reply.Status == IPStatus.Success)
                {
                    nodeip.Active = true;
                }
                if (dnscheck == true)
                {
                    try
                    {
                        nodeip.DNSName = (Dns.GetHostEntry(nodeip.IP.ToString())).HostName;
                    }
                    catch
                    {
                        nodeip.DNSName = "NONE";
                    }
                }
                UpdateCount(1);
            });

        }
        private void ResetCount()
        {
            int value = 0;
            synccontext.Post(new SendOrPostCallback(o =>
            {
                ipcounter = (int)o;
                statusbarIPs.Content = ipcounter;
            }), value);
        }
        private void UpdateCount(int value)
        {
            synccontext.Post(new SendOrPostCallback(o =>
            {
                ipcounter += (int)o;
                statusbarIPs.Content = ipcounter;
            }), value);
        }
        public void ProcessCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            buttonExport.IsEnabled = true;
            buttonStart.IsEnabled = true;
            statusbarComplete.Visibility = Visibility.Visible;
            Console.WriteLine("Process Finished");
        }
        public static IPAddress Increment(IPAddress address)
        {
            IPAddress result;

            byte[] bytes = address.GetAddressBytes();

            for (int k = bytes.Length - 1; k >= 0; k--)
            {
                if (bytes[k] == byte.MaxValue)
                {
                    bytes[k] = 0;
                    continue;
                }

                bytes[k]++;

                result = new IPAddress(bytes);
                return result;
            }

            // Un-incrementable, return the original address.
            return address;
        }
        private String SelectFolder()
        {
            String rval = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            System.Windows.Forms.FolderBrowserDialog folderDialog = new System.Windows.Forms.FolderBrowserDialog();
            folderDialog.SelectedPath = rval;
            System.Windows.Forms.DialogResult result = folderDialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                rval = folderDialog.SelectedPath;
            }
            else
            {
                rval = "false";
            }
            return rval;
        }
        private void ButtonExit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ButtonExport_Click(object sender, RoutedEventArgs e)
        {
            String savefolder = SelectFolder();
            if (savefolder != "false" && ipcollection.Count > 0)
            {
                var lines = new List<string>();
                string columnNames = "IP Address,Active,DNS Name";

                lines.Add(columnNames);
                foreach(IPObject node in ipcollection)
                {
                    string line = $"{node.IP},{node.Active},{node.DNSName}";
                    lines.Add(line);
                }

                File.WriteAllLines(savefolder + @"\IPScanExport.csv", lines);
                Xceed.Wpf.Toolkit.MessageBox.Show("Export Complete.", "Report Export", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
    public static class IPExtensions
    {
        public static int ToInteger(this IPAddress IP)
        {
            int result = 0;

            byte[] bytes = IP.GetAddressBytes();
            result = (int)(bytes[0] << 24 | bytes[1] << 16 | bytes[2] << 8 | bytes[3]);

            return result;
        }

        //returns 0 if equal
        //returns 1 if ip1 > ip2
        //returns -1 if ip1 < ip2
        public static int Compare(this IPAddress IP1, IPAddress IP2)
        {
            int ip1 = IP1.ToInteger();
            int ip2 = IP2.ToInteger();
            return (((ip1 - ip2) >> 0x1F) | (int)((uint)(-(ip1 - ip2)) >> 0x1F));
        }
    }
    public static class IPAddressExtensions
    {
        public static IPAddress GetBroadcastAddress(this IPAddress address, IPAddress subnetMask)
        {
            byte[] ipAdressBytes = address.GetAddressBytes();
            byte[] subnetMaskBytes = subnetMask.GetAddressBytes();

            if (ipAdressBytes.Length != subnetMaskBytes.Length)
                throw new ArgumentException("Lengths of IP address and subnet mask do not match.");

            byte[] broadcastAddress = new byte[ipAdressBytes.Length];
            for (int i = 0; i < broadcastAddress.Length; i++)
            {
                broadcastAddress[i] = (byte)(ipAdressBytes[i] | (subnetMaskBytes[i] ^ 255));
            }
            return new IPAddress(broadcastAddress);
        }

        public static IPAddress GetNetworkAddress(this IPAddress address, IPAddress subnetMask)
        {
            byte[] ipAdressBytes = address.GetAddressBytes();
            byte[] subnetMaskBytes = subnetMask.GetAddressBytes();

            if (ipAdressBytes.Length != subnetMaskBytes.Length)
                throw new ArgumentException("Lengths of IP address and subnet mask do not match.");

            byte[] broadcastAddress = new byte[ipAdressBytes.Length];
            for (int i = 0; i < broadcastAddress.Length; i++)
            {
                broadcastAddress[i] = (byte)(ipAdressBytes[i] & (subnetMaskBytes[i]));
            }
            return new IPAddress(broadcastAddress);
        }

        public static bool IsInSameSubnet(this IPAddress address2, IPAddress address, IPAddress subnetMask)
        {
            IPAddress network1 = address.GetNetworkAddress(subnetMask);
            IPAddress network2 = address2.GetNetworkAddress(subnetMask);

            return network1.Equals(network2);
        }
        public static IPAddress CidrToNetmask(this int cidr)
        {
            String longmask = "";
            int zeroes = 32 - cidr;
            String dottednotation = "";

            //Add 1's
            for (int i = 0; i < cidr; i++)
            {
                longmask += "1";
            }

            //Add 0's
            for (int j = 0; j < zeroes; j++)
            {
                longmask += "0";
            }

            //Convert to netmask            
            for (int k = 0; k < 4; k++)
            {
                String section = "";
                int lastrange = (k * 8);
                section = longmask.Substring(lastrange, 8);
                dottednotation += Convert.ToInt32(section, 2);
                if (k < 3)
                {
                    dottednotation += ".";
                }
            }

            //Convert string to ip address
            IPAddress netmask = IPAddress.Parse(dottednotation);
            return netmask;
        }
    }
    public class IPObject : INotifyPropertyChanged
    {
        private IPAddress _ip;
        private Boolean _active;
        private String _dnsname;

        public IPAddress IP { get { return _ip; } set { _ip = value; NotifyPropertyChanged("IP"); } }
        public Boolean Active { get { return _active; } set { _active = value; NotifyPropertyChanged("Active"); } }
        public String DNSName { get { return _dnsname; } set { _dnsname = value; NotifyPropertyChanged("DNSName"); } }
        public IPObject()
        {
            IP = IPAddress.Parse("0.0.0.0");
            Active = false;
            DNSName = "";
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
