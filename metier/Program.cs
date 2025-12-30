using System;
using System.Windows.Forms;

namespace Metier
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            // Windows 10/11 ‚Å•¶Žš‚ÌƒKƒ^‚Â‚«‚ð‚È‚­‚µAŠŠ‚ç‚©‚É•\Ž¦‚·‚é‚½‚ß‚ÌÝ’è
            if (Environment.OSVersion.Version.Major >= 10)
            {
                Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Metier());
        }
    }
}