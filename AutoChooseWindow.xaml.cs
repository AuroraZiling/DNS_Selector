using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Diagnostics;

namespace DNS_Selector
{
    /// <summary>
    /// AutoChooseWindow.xaml 的交互逻辑
    /// </summary>
    public partial class AutoChooseWindow : Window
    {
        public static string now_path = Environment.CurrentDirectory.Replace("\\", "/") + "/";
        public Dictionary<string, string> Address = new Dictionary<string, string> { };
        public int DNSListCounter = 0;
        public int FastestMs = 9999;
        public string FastestAddress = "";
        public int SlowestMs = -1;
        public string SlowestAddress = "";
        public AutoChooseWindow()
        {
            InitializeComponent();
            using (StreamReader file = File.OpenText(now_path + "dns.json"))
            {
                using (JsonTextReader reader = new JsonTextReader(file))
                {
                    JObject o = (JObject)JToken.ReadFrom(reader);
                    Address = o["Address"].ToObject<Dictionary<string, string>>();
                }
            }
            foreach (string i in Address.Keys)
            {
                _ = DNSList.Items.Add(new { Description = i, Address = Address[i], Ping = "尚未测试" });
                ++DNSListCounter;
            }
            ArrayList tempPing = new ArrayList();
            int counter = 0;
            foreach (string i in Address.Keys)
            {
                Ping pingSender = new Ping();
                PingReply reply = pingSender.Send(Address[i], 100);
                if (reply.Status == IPStatus.Success)
                {
                    if (FastestMs > reply.RoundtripTime)
                    {
                        FastestMs = int.Parse(reply.RoundtripTime.ToString());
                        FastestAddress = Address[i];
                    }
                    if (SlowestMs < reply.RoundtripTime)
                    {
                        SlowestMs = int.Parse(reply.RoundtripTime.ToString());
                        SlowestAddress = Address[i];
                    }
                    tempPing.Add(reply.RoundtripTime + "ms");
                }
                else
                {
                    tempPing.Add("超时");
                }
                ++counter;
            }
            DNSList.Items.Clear();
            DNSList.DataContext = null;
            int currentPos = 0;
            foreach (string i in Address.Keys)
            {
                _ = DNSList.Items.Add(new { Description = i, Address = Address[i], Ping = tempPing[currentPos] });
                ++currentPos;
            }
            FastestLabel.Content = "最快: DNS:" + FastestAddress + " 延迟:" + FastestMs.ToString();
            SlowestLabel.Content = "最慢: DNS:" + SlowestAddress + " 延迟:" + SlowestMs.ToString();
        }

        private void TestAgainBtn_Click(object sender, RoutedEventArgs e)
        {
            ArrayList tempPing = new ArrayList();
            int counter = 0;
            foreach (string i in Address.Keys)
            {
                Ping pingSender = new Ping();
                PingReply reply = pingSender.Send(Address[i], 100);
                if (reply.Status == IPStatus.Success)
                {
                    if (FastestMs > reply.RoundtripTime)
                    {
                        FastestMs = int.Parse(reply.RoundtripTime.ToString());
                        FastestAddress = Address[i];
                    }
                    if (SlowestMs < reply.RoundtripTime)
                    {
                        SlowestMs = int.Parse(reply.RoundtripTime.ToString());
                        SlowestAddress = Address[i];
                    }
                    tempPing.Add(reply.RoundtripTime + "ms");
                }
                else
                {
                    tempPing.Add("超时");
                }
                ++counter;
            }
            DNSList.Items.Clear();
            DNSList.DataContext = null;
            int currentPos = 0;
            foreach (string i in Address.Keys)
            {
                _ = DNSList.Items.Add(new { Description = i, Address = Address[i], Ping = tempPing[currentPos] });
                ++currentPos;
            }
            FastestLabel.Content = "最快DNS:" + FastestAddress + " 延迟:" + FastestMs.ToString();
            SlowestLabel.Content = "最慢DNS:" + SlowestAddress + " 延迟:" + SlowestMs.ToString();
        }

        private void MaxPingTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Regex re = new Regex("[^0-9.-]+");
            e.Handled = re.IsMatch(e.Text);
        }

        private void ChooseFastestBtn_Click(object sender, RoutedEventArgs e)
        {
            string AllNetworkAdapterStr = "设置完毕，结果如下:\n";
            Dictionary<string, string> AllNetworkAdapter = new Dictionary<string, string> { };
            ManagementScope oMs = new ManagementScope();
            ObjectQuery oQuery =
                new ObjectQuery("Select * From Win32_NetworkAdapter");
            ManagementObjectSearcher oSearcher = new ManagementObjectSearcher(oMs, oQuery);
            ManagementObjectCollection oReturnCollection = oSearcher.Get();
            foreach (ManagementObject oReturn in oReturnCollection)
            {
                if (oReturn.Properties["NetConnectionID"].Value != null)
                {
                    Console.WriteLine(oReturn.Properties["NetConnectionID"].Value);
                    string NetworkAdapter = oReturn.Properties["NetConnectionID"].Value.ToString();

                    Process process = new Process();
                    process.StartInfo.FileName = "cmd.exe";
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.RedirectStandardInput = true;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.UseShellExecute = false;
                    process.Start();
                    process.StandardInput.WriteLine("netsh interface ipv4 set dns "+NetworkAdapter+" static "+FastestAddress);
                    process.StandardInput.Flush();
                    process.StandardInput.Close();
                    process.WaitForExit();
                    if (process.StandardOutput.ReadToEnd().IndexOf("此命令提供的语法不正确") >= 0)
                    {
                        AllNetworkAdapter.Add(NetworkAdapter, " 设置失败");
                    }
                    else
                    {
                        AllNetworkAdapter.Add(NetworkAdapter, " 设置成功");
                    }
                }
            }
            foreach (string i in AllNetworkAdapter.Keys)
            {
                AllNetworkAdapterStr += "网卡:" + i + " 结果:" + AllNetworkAdapter[i] + '\n';
            }
            MessageBox.Show(AllNetworkAdapterStr);
        }
    }
}
