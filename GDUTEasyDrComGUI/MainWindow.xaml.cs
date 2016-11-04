using DotRas;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Navigation;

namespace GDUTEasyDrComGUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MahApps.Metro.Controls.MetroWindow
    {
        [DllImport("User32.dll")]
        private static extern bool ShowWindowAsync(IntPtr hWnd, int cmdShow);
        [DllImport("User32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        private const int SW_SHOWNOMAL = 1;
        private static void BringToFront()
        {
            Process instance = Process.GetCurrentProcess();
            ShowWindowAsync(instance.MainWindowHandle, SW_SHOWNOMAL);
            SetForegroundWindow(instance.MainWindowHandle);
        }

        private RasHandle rasHandle;
        private RasIPInfo ipaddr;
        private NotifyIcon trayIcon;
        private Process proc;
        private Encoding defEncoding = Encoding.GetEncoding(1252); // ANSI

        public MainWindow()
        {
            InitializeComponent();
            TitleCharacterCasing = System.Windows.Controls.CharacterCasing.Normal;
            CreateConnect("PPPoEDialer");
            btn_logout.IsEnabled = false;
        }

        public void CreateConnect(string ConnectName)
        {
            RasDialer dialer = new RasDialer();
            RasPhoneBook book = new RasPhoneBook();
            try
            {
                book.Open(RasPhoneBook.GetPhoneBookPath(RasPhoneBookType.User));
                if (book.Entries.Contains(ConnectName))
                {
                    book.Entries[ConnectName].PhoneNumber = " ";
                    book.Entries[ConnectName].Update();
                }
                else
                {
                    System.Collections.ObjectModel.ReadOnlyCollection<RasDevice> readOnlyCollection = RasDevice.GetDevices();
                    RasDevice device = RasDevice.GetDevices().Where(o => o.DeviceType == RasDeviceType.PPPoE).First();
                    RasEntry entry = RasEntry.CreateBroadbandEntry(ConnectName, device);
                    entry.PhoneNumber = " ";
                    book.Entries.Add(entry);
                }
            }
            catch (Exception)
            {
                //lb_status.Content = "创建PPPoE连接失败";
            }
        }

        private void btn_login_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string username = "\r\n" + tb_usr.Text;
                string password = tb_pw.Password.ToString();
                RasDialer dialer = new RasDialer();
                dialer.EntryName = "PPPoEDialer";
                dialer.PhoneNumber = " ";
                dialer.AllowUseStoredCredentials = true;
                dialer.PhoneBookPath = RasPhoneBook.GetPhoneBookPath(RasPhoneBookType.User);
                dialer.Credentials = new System.Net.NetworkCredential(username, password);
                dialer.Timeout = 500;
                rasHandle = dialer.Dial();
                while (rasHandle.IsInvalid)
                {
                    //lb_status.Content = "拨号失败";
                }
                if (!rasHandle.IsInvalid)
                {
                    //lb_status.Content = "拨号成功! ";
                    RasConnection conn = null;
                    foreach (var con in RasConnection.GetActiveConnections())
                        if (con.Handle == rasHandle)
                        {
                            conn = con;
                            break;
                        }
                    if (conn == null)
                    {
                        throw new Exception("Unable to get active connection by handle");
                    }
                    ipaddr = (RasIPInfo)conn.GetProjectionInfo(RasProjectionType.IP);
                    //lb_message.Content = "获得IP： " + ipaddr.IPAddress.ToString();
                    btn_login.IsEnabled = false;
                    btn_logout.IsEnabled = true;

                    SendHeartBeat();

                    MinimizeSettings();
                }
            }
            catch (Exception ex)
            {
                //lb_status.Content = "拨号出现异常";
            }
        }

        private void SendHeartBeat()
        {
            CreateChildProcess();
        }

        private void ConsoleWrapper_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = System.Windows.MessageBox.Show(
                "Really want to exit?",
                "Exit?") == MessageBoxResult.No;
        }

        private void CreateChildProcess()
        {
            proc = new Process();
            proc.StartInfo.FileName = "gdut-drcom.exe";
            proc.StartInfo.Arguments = "";
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardError = true;
            proc.StartInfo.RedirectStandardInput = true;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.CreateNoWindow = true;
            proc.EnableRaisingEvents = true;
            proc.Exited += (s, e) => Close();
            proc.Start();
            Read(proc.StandardOutput);
            Read(proc.StandardError);
        }

        private async void Read(StreamReader sr)
        {
            int size = 0;
            char[] buff = new char[4096];
            do
            {
                size = await sr.ReadAsync(buff, 0, buff.Length);
                byte[] abytes = defEncoding.GetBytes(buff, 0, size);
                Display("out", defEncoding.GetString(abytes));
            } while (size > 0);
            Display("ser", $"Read Exited {sr}");
        }

        private void Display(string type, string str)
        {
        }

        private void btn_logout_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                proc.Kill();
                foreach (var con in RasConnection.GetActiveConnections())
                    if (con.Handle == rasHandle)
                        con.HangUp();
                Thread.Sleep(1000);
                //lb_status.Content = "注销成功";
                //lb_message.Content = "已注销";
                btn_login.IsEnabled = true;
                btn_logout.IsEnabled = false;
                rasHandle = null;
            }
            catch (Exception ex)
            {
                //lb_status.Content = "注销出现异常";
            }
        }

        private void MetroWindow_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
                MinimizeSettings();
            else
                MaximizeSetting();
        }

        private void MinimizeSettings()
        {
            AddTrayIcon();
            string tips = rasHandle == null ? "登陆程序在托盘" : $"成功登陆!(IP{ipaddr.IPAddress})";
            trayIcon.ShowBalloonTip(3000, "", tips, ToolTipIcon.Info);
            ShowInTaskbar = false;
        }

        private void MaximizeSetting()
        {
            RemoveTrayIcon();
            ShowInTaskbar = true;
            BringToFront();
        }

        private void AddTrayIcon()
        {
            if (trayIcon != null)
            {
                return;
            }
            trayIcon = new NotifyIcon
            {
                Icon = new System.Drawing.Icon("GDUTDrCom.ico"),
                Text = Title
            };
            trayIcon.DoubleClick += TrayIcon_DoubleClick;
            trayIcon.Visible = true;
        }

        private void RemoveTrayIcon()
        {
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
                trayIcon = null;
            }
        }

        private void TrayIcon_DoubleClick(object sender, EventArgs e)
        {
            MaximizeSetting();
        }

        private void About(object sender, RequestNavigateEventArgs e)
        {
            System.Windows.MessageBox.Show(
                "Author: Wingkou\n" +
                "Ref:\n" +
                "\thttps://github.com/mchome/PPPoE-Dialer\n" +
                "\thttps://github.com/chenhaowen01/gdut-drcom",
                "About");
        }
    }
}
