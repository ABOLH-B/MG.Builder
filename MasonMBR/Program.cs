using System;
using System.Windows.Forms;

namespace MasonMBR
{
    internal static class StartForm
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MasonForm());
        }
    }
}
