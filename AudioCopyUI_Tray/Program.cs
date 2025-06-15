using System.Reflection;

namespace AudioCopyUI_Tray
{
    public static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            TrayForm trayForm = new TrayForm();
            trayForm.ShowInTaskbar = false;
            trayForm.Visible = false;

            Application.Run();
        }

    }
}