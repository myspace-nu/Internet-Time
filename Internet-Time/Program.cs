using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Configuration.Install;
using System.Reflection;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices; // For DllImport & StructLayout
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace Internet_Time
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomainUnhandledException;
            if (!Environment.UserInteractive)
            {
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[]
                {
                new Service1()
                };
                ServiceBase.Run(ServicesToRun);
            }
            else
            {
                string parameter = string.Concat(args);
                switch (parameter)
                {
                    case "--install":
                        ManagedInstallerClass.InstallHelper(new string[] { Assembly.GetExecutingAssembly().Location });
                        break;
                    case "--uninstall":
                        ManagedInstallerClass.InstallHelper(new string[] { "/u", Assembly.GetExecutingAssembly().Location });
                        break;
                    default:
                        // running as console app
                        // Console.WriteLine(System.Reflection.Assembly.GetExecutingAssembly().GetName().Name);
                        // Console.WriteLine(String.Format("Install servicve using {0} --install (or --uninstall).", System.AppDomain.CurrentDomain.FriendlyName));
                        // Console.WriteLine("Press [CTRL][C] to stop...");
                        // Service1 myService = new Service1();
                        // myService.onDebug();
                        // System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);
                        DictionaryWithDefault<string, string> arg = commandArgumentsParser(args);
                        DateTime UTCtime = GetNetworkUTCTime();
                        if (!string.IsNullOrEmpty(arg["add"]))
                        {
                            int addMin = 0;
                            int.TryParse(arg["add"], out addMin);
                            UTCtime = UTCtime.AddMinutes(addMin);
                        }
                        if (!string.IsNullOrEmpty(arg["set"]))
                        {
                            Console.WriteLine("Setting time: " + UTCtime.ToLocalTime().ToString());
                            setSystemTime(UTCtime);
                        }
                        if (!string.IsNullOrEmpty(arg["showoffset"]))
                        {
                            Console.WriteLine("Time difference: " + (DateTime.UtcNow - UTCtime).TotalSeconds + " seconds.");
                        }
                        Console.WriteLine(UTCtime.ToLocalTime().ToString());

                    break;
                }
            }
        }

        static DateTime GetNetworkUTCTime(string ntpServer = "time.windows.com")
        {
            // Minimal NTP payload
            var ntpPayload = new byte[48];
            // Flags                1 byte
            // Stratum              1 byte
            // Polling              1 byte
            // Precision            1 byte
            // Root Delay           4 bytes
            // Root Dispersion      4 bytes
            // Reference Identifier 4 bytes
            // Reference Timestamp  8 bytes
            // Origin Timestamp     8 bytes
            // Receive Timestamp    8 bytes
            // Transmit Timestamp   8 bytes

            // Leap Indicator + Version Number + Mode should be 0x1B;
            ntpPayload[0] |= 0x00; // Leap Indicator (LI) 0 (no warning) : 0b00______
            ntpPayload[0] |= 0x18; // Version Number (VN) 3 (IPv4 only)  : 0b__011___
            ntpPayload[0] |= 0x03; // Mode         (Mode) 3 (Client Mode): 0b_____011


            var addresses = Dns.GetHostEntry(ntpServer).AddressList;
            var ipEndPoint = new IPEndPoint(addresses[0], 123); // NTP uses port 123 (UDP)
            var timer = new Stopwatch();
            int timeout = 3000;
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                socket.Connect(ipEndPoint);
                socket.ReceiveTimeout = timeout;
                timer.Start();
                try
                {
                    socket.Send(ntpPayload);
                    socket.Receive(ntpPayload);
                }
                catch { }
                timer.Stop();
                socket.Close();
            }
            // Get seconds part and convert big-endian to little-endian
            ulong intPart = SwapEndianness(BitConverter.ToUInt32(ntpPayload, 40)); // 40 is offset for the Transmit Timestamp field
            // Get seconds fraction part and convert big-endian to little-endian
            ulong fractPart = SwapEndianness(BitConverter.ToUInt32(ntpPayload, 40 + 4)); // 40 is offset for the Transmit Timestamp field

            var milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);
            milliseconds += (ulong)(timer.Elapsed.Milliseconds/2);

            // networkDateTime as UTC
            var networkDateTime = (new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc)).AddMilliseconds((long)milliseconds);

            return (milliseconds > (ulong)timeout) ? networkDateTime : DateTime.UtcNow;
        }
        static uint SwapEndianness(ulong x)
        {
            return (uint)(((x & 0x000000ff) << 24) +
                           ((x & 0x0000ff00) << 8) +
                           ((x & 0x00ff0000) >> 8) +
                           ((x & 0xff000000) >> 24));
        }
        /// <summary>
        /// Setting system time.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEMTIME
        {
            public short wYear;
            public short wMonth;
            public short wDayOfWeek;
            public short wDay;
            public short wHour;
            public short wMinute;
            public short wSecond;
            public short wMilliseconds;
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetSystemTime(ref SYSTEMTIME st);
        static void setSystemTime(DateTime UTCtime)
        {
            SYSTEMTIME st = new SYSTEMTIME();
            st.wYear = (short)UTCtime.Year;
            st.wMonth = (short)UTCtime.Month;
            st.wDay = (short)UTCtime.Day;
            st.wHour = (short)UTCtime.Hour;
            st.wMinute = (short)UTCtime.Minute;
            st.wSecond = (short)UTCtime.Second;
            st.wMilliseconds = (short)UTCtime.Millisecond;
            SetSystemTime(ref st);
        }
        /// <summary>
        /// Getting named command line parameters
        /// </summary>
        static DictionaryWithDefault<string,string> commandArgumentsParser(string[] args)
        {
            DictionaryWithDefault<string, string> arg = new DictionaryWithDefault<string, string>() { };
            string name = null; int i = 0;
            foreach (string a in args)
            {
                string[] parts = a.Split('=');
                if (Regex.IsMatch(parts[0], @"^(--|\/)"))
                {
                    if (!string.IsNullOrEmpty(name))
                    {
                        arg[name] = "1";
                        arg[i.ToString()] = name;
                        i++;
                        name = null;
                    }
                    if (parts.Length > 1)
                    {
                        arg[Regex.Replace(parts[0], @"^(--|\/)", "")] = parts[1];
                        arg[i.ToString()] = Regex.Replace(a, @"^(--|\/)", "");
                        i++;
                    }
                    else
                    {
                        name = Regex.Replace(parts[0], @"^(--|\/)", "");
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(name))
                    {
                        arg[name] = parts[0];
                        name = null;
                        arg[i.ToString()] = a;
                        i++;

                    }
                    else
                    {
                        arg[i.ToString()] = a;
                        i++;
                    }
                }
            }
            if (!string.IsNullOrEmpty(name))
            {
                arg[name] = "1";
                name = null;
                arg[i.ToString()] = name;
                i++;
            }
            return arg;
        }
        public class DictionaryWithDefault<TKey, TValue> : Dictionary<TKey, TValue>
        {
            TValue _default;
            public TValue DefaultValue
            {
                get { return _default; }
                set { _default = value; }
            }
            public DictionaryWithDefault() : base() { }
            public DictionaryWithDefault(TValue defaultValue) : base()
            {
                _default = defaultValue;
            }
            public new TValue this[TKey key]
            {
                get
                {
                    TValue t;
                    return base.TryGetValue(key, out t) ? t : _default;
                }
                set { base[key] = value; }
            }
        }
        private static void CurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            File.AppendAllText(AppDomain.CurrentDomain.BaseDirectory + "Service installaion error.txt", ((Exception)e.ExceptionObject).Message + ((Exception)e.ExceptionObject).InnerException.Message);
        }
    }
}
