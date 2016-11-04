using DotRas;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace GDUTEasyDrComGUI
{
    public partial class MainWindow
    {
        private RasHandle rasHandle;
        private RasIPInfo ipaddr;
        private Process proc;
        private Encoding defEncoding = Encoding.GetEncoding(1252); // ANSI

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

        private void Login()
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
                    Logger.Log("拨号失败");
                    throw new Exception("拨号失败");
                }
                if (!rasHandle.IsInvalid)
                {
                    Logger.Log("拨号成功");
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
                    Logger.Log("获得IP： " + ipaddr.IPAddress.ToString());
                    btn_login.IsEnabled = false;
                    btn_logout.IsEnabled = true;

                    //SendHeartBeat
                    CreateChildProcess();

                    WindowState = System.Windows.WindowState.Minimized;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "拨号异常");
            }
        }

        private void Logout()
        {
            try
            {
                if (proc != null && !proc.HasExited)
                    proc.Kill();
                foreach (var con in RasConnection.GetActiveConnections())
                    if (con.Handle == rasHandle)
                        con.HangUp();
                Thread.Sleep(1000);
                Logger.Log("已注销");
                btn_login.IsEnabled = true;
                btn_logout.IsEnabled = false;
                rasHandle = null;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "注销异常");
            }
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
            //proc.Exited += (s, e) => Close();
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
                Logger.Log(defEncoding.GetString(abytes));
            } while (size > 0);
            Logger.Log("Exited");
        }
    }
}
