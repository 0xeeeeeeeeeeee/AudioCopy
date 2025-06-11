namespace AudioCopyUI_Tray
{
    public partial class TrayForm : Form
    {
        public TrayForm()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (AudioCopyUI_TrayHelper.TrayHelper.BootApp is not null) AudioCopyUI_TrayHelper.TrayHelper.BootApp();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (AudioCopyUI_TrayHelper.TrayHelper.CloseApp is not null) AudioCopyUI_TrayHelper.TrayHelper.CloseApp();
        }
    }
}
