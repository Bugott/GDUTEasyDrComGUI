using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Navigation;
using static GDUTEasyDrComGUI.Win32Native;

namespace GDUTEasyDrComGUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MahApps.Metro.Controls.MetroWindow
    {
        private NotifyIcon trayIcon;

        public MainWindow()
        {
            InitializeComponent();
            Icon = Properties.Resources.GDUTDrComIcon.ToImageSource();
            CreateConnect();
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
            string tips = info.rasHandle == null ? "登陆程序在托盘" : $"成功登陆!(IP: {info.ipaddr.IPAddress})";
            trayIcon.ShowBalloonTip(3000, "", tips, ToolTipIcon.Info);
            ShowInTaskbar = false;
        }

        private void MaximizeSetting()
        {
            RemoveTrayIcon();
            ShowInTaskbar = true;
            Win32Native.BringToFront();
        }

        private void AddTrayIcon()
        {
            if (trayIcon != null)
            {
                return;
            }
            trayIcon = new NotifyIcon
            {
                Icon = Properties.Resources.GDUTDrComIcon,
                Text = Title
            };
            trayIcon.DoubleClick += (s, e) => MaximizeSetting();
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

        private void About(object sender, RequestNavigateEventArgs e)
        {
            System.Windows.MessageBox.Show(
                "Author: Wingkou\n" +
                "Source code:\n" +
                "\thttps://github.com/lyhyl/GDUTEasyDrComGUI\n" +
                "Ref:\n" +
                "\thttps://github.com/mchome/PPPoE-Dialer\n" +
                "\thttps://github.com/drcoms/drcom-generic",
                "About");
        }

        private void btn_login_Click(object sender, RoutedEventArgs e)
        {
            if (Login(tb_usr.Text, tb_pw.Password))
            {
                WindowState = System.Windows.WindowState.Minimized;
                btn_login.IsEnabled = false;
                btn_logout.IsEnabled = true;
                RememberConfig.SaveConfig(tb_usr.Text, tb_pw.Password);
            }
        }

        private void btn_logout_Click(object sender, RoutedEventArgs e)
        {
            Logout();
            btn_login.IsEnabled = true;
            btn_logout.IsEnabled = false;
        }

        private void MetroWindow_Closing(object sender, CancelEventArgs e)
        {
            if (info.Connected)
            {
                if (System.Windows.MessageBox.Show("直接退出将自动注销，是否继续退出?", "退出", MessageBoxButton.YesNo)
                  == MessageBoxResult.Yes)
                    btn_logout_Click(btn_logout, new RoutedEventArgs());
                else
                    e.Cancel = true;
            }
        }

        private void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            IntPtr handle = Process.GetCurrentProcess().MainWindowHandle;
            int exStyle = (int)GetWindowLong(handle, (int)GetWindowLongFields.GWL_EXSTYLE);

            exStyle |= (int)ExtendedWindowStyles.WS_EX_TOOLWINDOW;
            SetWindowLong(handle, (int)GetWindowLongFields.GWL_EXSTYLE, (IntPtr)exStyle);

            string usr, pw;
            RememberConfig.GetConfig(out usr, out pw);
            tb_usr.Text = usr;
            tb_pw.Password = pw;
        }
    }
}
