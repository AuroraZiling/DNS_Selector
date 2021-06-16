using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Threading;
using System.Windows;

namespace DNS_Selector
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public static string now_path = Environment.CurrentDirectory.Replace("\\", "/") + "/";
        public Dictionary<string, string> Address = new Dictionary<string, string> { };
        public int DNSListCounter = 0;
        public Thread childThread = null;
        public MainWindow()
        {
            InitializeComponent();
            this.Closing += Window_Closing;
            CheckFiles();
            foreach (string i in Address.Keys)
            {
                _ = DNSList.Items.Add(new { Description = i, Address = Address[i], Ping = "尚未测试"});
                ++DNSListCounter;
            }
            ThreadStart childref = new ThreadStart(PingUpdate);
            childThread = new Thread(childref);
            childThread.Start();
        }

        public void PingUpdate()
        {
            while (true)
            {
                ArrayList tempPing = new ArrayList();
                int counter = 0;
                foreach (string i in Address.Keys)
                {
                    Ping pingSender = new Ping();
                    PingReply reply = pingSender.Send(Address[i], 100);
                    if (reply.Status == IPStatus.Success)
                    {
                        tempPing.Add(reply.RoundtripTime+"ms");
                    }
                    else
                    {
                        tempPing.Add("超时");
                    }
                    ++counter;
                }
                this.DNSList.Dispatcher.Invoke(new Action(() => { DNSList.Items.Clear(); }));
                this.DNSList.Dispatcher.Invoke(new Action(() => { DNSList.DataContext = null; }));
                int currentPos = 0;
                foreach (string i in Address.Keys)
                {
                    this.DNSList.Dispatcher.Invoke(new Action(() => { DNSList.Items.Add(new { Description = i, Address = Address[i], Ping = tempPing[currentPos] }); }));
                    ++currentPos;
                }
                Thread.Sleep(1000);
            }
        }

        public void CheckFiles()
        {
            if (File.Exists(now_path + "dns.json"))
            {
                string json = File.OpenText(now_path + "dns.json").ReadToEnd();

                if (JsonSplit.IsJson(json))
                {
                    try
                    {
                        using (StreamReader file = File.OpenText(now_path + "dns.json"))
                        {
                            using (JsonTextReader reader = new JsonTextReader(file))
                            {
                                JObject o = (JObject)JToken.ReadFrom(reader);
                                Address = o["Address"].ToObject<Dictionary<string, string> > ();
                            }
                        }
                    }
                    catch
                    {
                        File.Delete(now_path + "dns.json");
                        File.Create(now_path + "dns.json").Dispose();
                        ExtractResFile("DNS_Selector.dns.json", now_path + "dns.json");
                        using (StreamReader file = File.OpenText(now_path + "dns.json"))
                        {
                            using (JsonTextReader reader = new JsonTextReader(file))
                            {
                                JObject o = (JObject)JToken.ReadFrom(reader);
                                Address = o["Address"].ToObject<Dictionary<string, string>>();
                            }
                        }
                    }
                }
                else
                {
                    File.Create(now_path + "dns.json").Dispose();
                    ExtractResFile("DNS_Selector.dns.json", now_path + "dns.json");
                }
            }
            else
            {
                File.Create(now_path + "dns.json").Dispose();
                ExtractResFile("DNS_Selector.dns.json", now_path + "dns.json");
            }
        }
        internal class JsonSplit //检查Json文件是否合法
        {
            private static bool IsJsonStart(ref string json)
            {
                if (!string.IsNullOrEmpty(json))
                {
                    json = json.Trim('\r', '\n', ' ');
                    if (json.Length > 1)
                    {
                        char s = json[0];
                        char e = json[json.Length - 1];
                        return (s == '{' && e == '}') || (s == '[' && e == ']');
                    }
                }
                return false;
            }
            internal static bool IsJson(string json)
            {
                int errIndex;
                return IsJson(json, out errIndex);
            }
            internal static bool IsJson(string json, out int errIndex)
            {
                errIndex = 0;
                if (IsJsonStart(ref json))
                {
                    CharState cs = new CharState();
                    char c;
                    for (int i = 0; i < json.Length; i++)
                    {
                        c = json[i];
                        if (SetCharState(c, ref cs) && cs.childrenStart)
                        {
                            string item = json.Substring(i);
                            int err;
                            int length = GetValueLength(item, true, out err);
                            cs.childrenStart = false;
                            if (err > 0)
                            {
                                errIndex = i + err;
                                return false;
                            }
                            i = i + length - 1;
                        }
                        if (cs.isError)
                        {
                            errIndex = i;
                            return false;
                        }
                    }

                    return !cs.arrayStart && !cs.jsonStart;
                }
                return false;
            }

            /// <summary>
            /// 获取值的长度（当Json值嵌套以"{"或"["开头时）
            /// </summary>
            private static int GetValueLength(string json, bool breakOnErr, out int errIndex)
            {
                errIndex = 0;
                int len = 0;
                if (!string.IsNullOrEmpty(json))
                {
                    CharState cs = new CharState();
                    char c;
                    for (int i = 0; i < json.Length; i++)
                    {
                        c = json[i];
                        if (!SetCharState(c, ref cs))
                        {
                            if (!cs.jsonStart && !cs.arrayStart)
                            {
                                break;
                            }
                        }
                        else if (cs.childrenStart)
                        {
                            int length = GetValueLength(json.Substring(i), breakOnErr, out errIndex);//递归子值，返回一个长度。。。
                            cs.childrenStart = false;
                            cs.valueStart = 0;
                            //cs.state = 0;
                            i = i + length - 1;
                        }
                        if (breakOnErr && cs.isError)
                        {
                            errIndex = i;
                            return i;
                        }
                        if (!cs.jsonStart && !cs.arrayStart)
                        {
                            len = i + 1;
                            break;
                        }
                    }
                }
                return len;
            }
            /// <summary>
            /// 字符状态
            /// </summary>
            private class CharState
            {
                internal bool jsonStart = false;
                internal bool setDicValue = false;
                internal bool escapeChar = false;
                /// <summary>
                /// 数组开始【仅第一开头才算】，值嵌套的以【childrenStart】来标识。
                /// </summary>
                internal bool arrayStart = false;
                internal bool childrenStart = false;
                /// <summary>
                /// 【0 初始状态，或 遇到“,”逗号】；【1 遇到“：”冒号】
                /// </summary>
                internal int state = 0;

                /// <summary>
                /// 【-1 取值结束】【0 未开始】【1 无引号开始】【2 单引号开始】【3 双引号开始】
                /// </summary>
                internal int keyStart = 0;
                /// <summary>
                /// 【-1 取值结束】【0 未开始】【1 无引号开始】【2 单引号开始】【3 双引号开始】
                /// </summary>
                internal int valueStart = 0;
                internal bool isError = false;

                internal void CheckIsError(char c)
                {
                    if (keyStart > 1 || valueStart > 1)
                    {
                        return;
                    }
                    switch (c)
                    {
                        case '{':
                            isError = jsonStart && state == 0;
                            break;
                        case '}':
                            isError = !jsonStart || (keyStart != 0 && state == 0);
                            break;
                        case '[':
                            isError = arrayStart && state == 0;
                            break;
                        case ']':
                            isError = !arrayStart || jsonStart;
                            break;
                        case '"':
                        case '\'':
                            isError = !(jsonStart || arrayStart);
                            if (!isError)
                            {
                                isError = (state == 0 && keyStart == -1) || (state == 1 && valueStart == -1);
                            }
                            if (!isError && arrayStart && !jsonStart && c == '\'')//['aa',{}]
                            {
                                isError = true;
                            }
                            break;
                        case ':':
                            isError = !jsonStart || state == 1;
                            break;
                        case ',':
                            isError = !(jsonStart || arrayStart);
                            if (!isError)
                            {
                                if (jsonStart)
                                {
                                    isError = state == 0 || (state == 1 && valueStart > 1);
                                }
                                else if (arrayStart)
                                {
                                    isError = keyStart == 0 && !setDicValue;
                                }
                            }
                            break;
                        case ' ':
                        case '\r':
                        case '\n':
                        case '\0':
                        case '\t':
                            break;
                        default:
                            isError = (!jsonStart && !arrayStart) || (state == 0 && keyStart == -1) || (valueStart == -1 && state == 1);//
                            break;
                    }
                }
            }
            /// <summary>
            /// 设置字符状态(返回true则为关键词，返回false则当为普通字符处理）
            /// </summary>
            private static bool SetCharState(char c, ref CharState cs)
            {
                cs.CheckIsError(c);
                switch (c)
                {
                    case '{':
                        #region 大括号
                        if (cs.keyStart <= 0 && cs.valueStart <= 0)
                        {
                            cs.keyStart = 0;
                            cs.valueStart = 0;
                            if (cs.jsonStart && cs.state == 1)
                            {
                                cs.childrenStart = true;
                            }
                            else
                            {
                                cs.state = 0;
                            }
                            cs.jsonStart = true;
                            return true;
                        }
                        #endregion
                        break;
                    case '}':
                        #region 大括号结束
                        if (cs.keyStart <= 0 && cs.valueStart < 2 && cs.jsonStart)
                        {
                            cs.jsonStart = false;
                            cs.state = 0;
                            cs.keyStart = 0;
                            cs.valueStart = 0;
                            cs.setDicValue = true;
                            return true;
                        }
                        #endregion
                        break;
                    case '[':
                        #region 中括号开始
                        if (!cs.jsonStart)
                        {
                            cs.arrayStart = true;
                            return true;
                        }
                        else if (cs.jsonStart && cs.state == 1)
                        {
                            cs.childrenStart = true;
                            return true;
                        }
                        #endregion
                        break;
                    case ']':
                        #region 中括号结束
                        if (cs.arrayStart && !cs.jsonStart && cs.keyStart <= 2 && cs.valueStart <= 0)
                        {
                            cs.keyStart = 0;
                            cs.valueStart = 0;
                            cs.arrayStart = false;
                            return true;
                        }
                        #endregion
                        break;
                    case '"':
                    case '\'':
                        #region 引号
                        if (cs.jsonStart || cs.arrayStart)
                        {
                            if (cs.state == 0)
                            {
                                if (cs.keyStart <= 0)
                                {
                                    cs.keyStart = (c == '"' ? 3 : 2);
                                    return true;
                                }
                                else if ((cs.keyStart == 2 && c == '\'') || (cs.keyStart == 3 && c == '"'))
                                {
                                    if (!cs.escapeChar)
                                    {
                                        cs.keyStart = -1;
                                        return true;
                                    }
                                    else
                                    {
                                        cs.escapeChar = false;
                                    }
                                }
                            }
                            else if (cs.state == 1 && cs.jsonStart)
                            {
                                if (cs.valueStart <= 0)
                                {
                                    cs.valueStart = (c == '"' ? 3 : 2);
                                    return true;
                                }
                                else if ((cs.valueStart == 2 && c == '\'') || (cs.valueStart == 3 && c == '"'))
                                {
                                    if (!cs.escapeChar)
                                    {
                                        cs.valueStart = -1;
                                        return true;
                                    }
                                    else
                                    {
                                        cs.escapeChar = false;
                                    }
                                }

                            }
                        }
                        #endregion
                        break;
                    case ':':
                        #region 冒号
                        if (cs.jsonStart && cs.keyStart < 2 && cs.valueStart < 2 && cs.state == 0)
                        {
                            if (cs.keyStart == 1)
                            {
                                cs.keyStart = -1;
                            }
                            cs.state = 1;
                            return true;
                        }
                        #endregion
                        break;
                    case ',':
                        #region 逗号 //["aa",{aa:12,}]

                        if (cs.jsonStart)
                        {
                            if (cs.keyStart < 2 && cs.valueStart < 2 && cs.state == 1)
                            {
                                cs.state = 0;
                                cs.keyStart = 0;
                                cs.valueStart = 0;
                                cs.setDicValue = true;
                                return true;
                            }
                        }
                        else if (cs.arrayStart && cs.keyStart <= 2)
                        {
                            cs.keyStart = 0;
                            return true;
                        }
                        #endregion
                        break;
                    case ' ':
                    case '\r':
                    case '\n':
                    case '\0':
                    case '\t':
                        if (cs.keyStart <= 0 && cs.valueStart <= 0)
                        {
                            return true;
                        }
                        break;
                    default:
                        if (c == '\\')
                        {
                            if (cs.escapeChar)
                            {
                                cs.escapeChar = false;
                            }
                            else
                            {
                                cs.escapeChar = true;
                                return true;
                            }
                        }
                        else
                        {
                            cs.escapeChar = false;
                        }
                        if (cs.jsonStart || cs.arrayStart)
                        {
                            if (cs.keyStart <= 0 && cs.state == 0)
                            {
                                cs.keyStart = 1;
                            }
                            else if (cs.valueStart <= 0 && cs.state == 1 && cs.jsonStart)
                            {
                                cs.valueStart = 1;
                            }
                        }
                        break;
                }
                return false;
            }
        }
        public static void ExtractResFile(string resFileName, string outputFile) //提取内嵌文件
        {
            BufferedStream inStream = null;
            FileStream outStream = null;
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                inStream = new BufferedStream(asm.GetManifestResourceStream(resFileName));
                outStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write);

                byte[] buffer = new byte[1024];
                int length;

                while ((length = inStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    outStream.Write(buffer, 0, length);
                }
                outStream.Flush();
            }
            finally
            {
                if (outStream != null)
                {
                    outStream.Close();
                }
                if (inStream != null)
                {
                    inStream.Close();
                }
            }
        }

        private void AutoChooseBtn_Click(object sender, RoutedEventArgs e)
        {
            AutoChooseWindow AutoChoose_window = new AutoChooseWindow();
            AutoChoose_window.ShowDialog();
        }

        private void OpenDNSListBtn_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(now_path + "dns.json");
        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            childThread.Abort();
            Environment.Exit(0);
        }
    }
}
