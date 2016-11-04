using System;
using System.IO;

namespace GDUTEasyDrComGUI
{
    public static class Logger
    {
        public static void Log(string log)
        {
            using (StreamWriter sw = new StreamWriter("log.txt"))
            {
                sw.WriteLine(DateTime.Now.ToLongTimeString() + " : " + log);
            }
        }
    }
}
