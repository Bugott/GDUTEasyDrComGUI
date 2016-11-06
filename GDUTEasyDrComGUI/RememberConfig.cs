using System.IO;

namespace GDUTEasyDrComGUI
{
    public static class RememberConfig
    {
        private static string fileName = "gdutDrComUserDat.dat";

        public static bool HasConfig()
        {
            return File.Exists(fileName);
        }

        public static void GetConfig(out string usr, out string pw)
        {
            if (HasConfig())
            {
                using (StreamReader sr = new StreamReader(fileName))
                {
                    usr = sr.ReadLine();
                    pw = sr.ReadLine();
                }
            }
            else
            {
                usr = "";
                pw = "";
            }
        }

        public static void SaveConfig(string usr, string pw)
        {
            using (StreamWriter sw = new StreamWriter(fileName))
            {
                sw.WriteLine(usr);
                sw.WriteLine(pw);
            }
        }
    }
}
