using System;
using System.Threading;
using System.Windows;

namespace GDUTEasyDrComGUI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly Mutex singleInstanceWatcher;
        private static readonly bool createdNew;

        static App()
        {
            singleInstanceWatcher = new Mutex(false, "GDUTDrComSemaphore", out createdNew);
            if (!createdNew)
            {
                MessageBox.Show("程序已经运行!", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(-1);
            }
        }
    }
}
