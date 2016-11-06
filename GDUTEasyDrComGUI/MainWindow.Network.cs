using DotRas;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace GDUTEasyDrComGUI
{
    public partial class MainWindow
    {
        private struct ConnectInfo
        {
            public bool Connected;
            public RasHandle rasHandle;
            public RasIPInfo ipaddr;
            public Process proc;
        }
        private ConnectInfo info = new ConnectInfo();

        private RasDialer dialer = new RasDialer();
        private readonly string ConnectionName = "GDUT PPPoE Dialer";
        private Encoding defEncoding = Encoding.GetEncoding(1252); // ANSI

        private void CreateConnect()
        {
            RasPhoneBook book = new RasPhoneBook();
            try
            {
                book.Open(RasPhoneBook.GetPhoneBookPath(RasPhoneBookType.User));
                if (book.Entries.Contains(ConnectionName))
                {
                    book.Entries[ConnectionName].PhoneNumber = " ";
                    book.Entries[ConnectionName].Update();
                }
                else
                {
                    RasDevice device = RasDevice.GetDevices().
                        Where(o => o.DeviceType == RasDeviceType.PPPoE).First();
                    RasEntry entry = RasEntry.CreateBroadbandEntry(ConnectionName, device);
                    entry.PhoneNumber = " ";
                    book.Entries.Add(entry);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"创建PPPoE连接失败({ex.Message})");
            }
        }

        private bool Login(string username, string password)
        {
            try
            {
                // Fuck it!
                username = "\r\n" + username;

                dialer.EntryName = ConnectionName;
                dialer.PhoneNumber = " ";
                dialer.AllowUseStoredCredentials = true;
                dialer.PhoneBookPath = RasPhoneBook.GetPhoneBookPath(RasPhoneBookType.User);
                dialer.Credentials = new NetworkCredential(username, password);
                dialer.Timeout = 1000;
                dialer.StateChanged += Dialer_StateChanged;
                dialer.Error += Dialer_Error;
                info.rasHandle = dialer.Dial();
                if (info.rasHandle.IsInvalid)
                    throw new Exception("拨号失败");
                else
                {
                    Logger.Log("拨号成功");
                    RasConnection conn = RasConnection.GetActiveConnections().
                        Where(o => o.Handle == info.rasHandle).First();
                    if (conn == null)
                        throw new Exception("Unable to get active connection by handle");
                    info.ipaddr = (RasIPInfo)conn.GetProjectionInfo(RasProjectionType.IP);
                    Logger.Log("获得IP： " + info.ipaddr.IPAddress.ToString());

                    info.Connected = true;
                    
                    StartHeartBeat();
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "拨号异常");
                return false;
            }
        }

        private void Dialer_Error(object sender, ErrorEventArgs e)
        {
            Logger.Log(e.GetException().Message);
        }

        private void Dialer_StateChanged(object sender, StateChangedEventArgs e)
        {
            Logger.Log(e.State.ToString());
        }

        private void Logout()
        {
            if (!info.Connected)
                return;
            try
            {
                RasConnection.GetActiveConnections().
                    Where(o => o.Handle == info.rasHandle).First()?.HangUp();
                Logger.Log("已注销");
                info.Connected = false;
                if (!info.proc.HasExited)
                    info.proc.Kill();
            }
            catch (Exception ex)
            {
                Logger.Log($"注销异常({ex.Message})");
                System.Windows.MessageBox.Show(ex.Message, "注销异常");
            }
        }

        private void StartHeartBeat()
        {
            info.proc = new Process();
            info.proc.StartInfo.FileName = "gdut-drcom.exe";
            info.proc.StartInfo.Arguments = "";
            info.proc.StartInfo.UseShellExecute = false;
            info.proc.StartInfo.RedirectStandardError = true;
            info.proc.StartInfo.RedirectStandardInput = true;
            info.proc.StartInfo.RedirectStandardOutput = true;
            info.proc.StartInfo.CreateNoWindow = true;
            info.proc.EnableRaisingEvents = true;
            info.proc.Exited += HeartBeat_Exited;
            info.proc.Start();
            //Read(info.proc.StandardOutput);
            Read(info.proc.StandardError);
        }

        private async void Read(StreamReader sr)
        {
            int size = 0;
            char[] buff = new char[4096];
            do
            {
                size = await sr.ReadAsync(buff, 0, buff.Length);
                byte[] abytes = defEncoding.GetBytes(buff, 0, size);
                Logger.Log(defEncoding.GetString(abytes));
            } while (size > 0);
            Logger.Log("Heart Beat process reading error thread exited");
        }

        private void HeartBeat_Exited(object sender, EventArgs e)
        {
            if (info.Connected)
            {
                Logout();
                btn_login.Dispatcher.Invoke(() => btn_login.IsEnabled = true);
                btn_logout.Dispatcher.Invoke(() => btn_logout.IsEnabled = false);
                Logger.Log($"心跳包进程异常结束");
                System.Windows.MessageBox.Show("心跳包进程异常结束", "错误");
            }
        }
    }
}
