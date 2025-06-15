using System.Diagnostics;
using System.Windows.Forms;
using static AudioCopyUI_TrayHelper.TrayHelper;

namespace AudioCopyUI_Tray
{
    public partial class TrayForm : Form
    {
        public TrayForm()
        {
            InitializeComponent();
            notifyIcon1.ContextMenuStrip = contextMenuStrip1;
            this.ShowInTaskbar = false;
            this.Visible = false;
            this.WindowState = FormWindowState.Minimized;

           
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80; // WS_EX_TOOLWINDOW
                return cp;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            BootApp();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (AudioCopyUI_TrayHelper.TrayHelper.CloseApp is not null) AudioCopyUI_TrayHelper.TrayHelper.CloseApp();
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            BootApp();
        }

        private void contextMenuStrip1_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if(AudioCopyUI_TrayHelper.TrayHelper.GUIRunning == false)
            {
                if (IsNotStandalone) return;
            }
            if (!AudioCopyUI_TrayHelper.TrayHelper.IsNotStandalone)
            {
                MediaInfotoolStripMenuItem.Visible = false;
            }

            LaunchToolStripMenuItem.Text = Resource.Launch;
            MediaInfotoolStripMenuItem.Text = "Unknown";
            ListenclientstoolStripMenuItem.Text = Resource.ListeningClients;
            ListenclientstoolStripMenuItem.Visible = false;
            ExitOptionstoolStripMenuItem.Text = Resource.Close + "...";
            ExitToolStripMenuItem.Click += 默认退出选项ToolStripMenuItem_Click;
            ExitToolStripMenuItem.Text = AudioCopyUI_TrayHelper.TrayHelper.KeepBackendAsDefault ? Resource.Close : Resource.Shutdown;
            彻底关闭ToolStripMenuItem.Text = Resource.Shutdown;
            保留后端并退出ToolStripMenuItem.Text = Resource.Close;
            if (AudioCopyUI_TrayHelper.TrayHelper.GetSMTC is not null)
            {
                AudioCopyUI_TrayHelper.TrayHelper.GetSMTC();
                MediaInfotoolStripMenuItem.Text = $"{AudioCopyUI_TrayHelper.TrayHelper.Title} - {AudioCopyUI_TrayHelper.TrayHelper.Artist}";
                ListenclientstoolStripMenuItem.Text = string.Format(ListenclientstoolStripMenuItem.Text ?? "{0} clients listening", AudioCopyUI_TrayHelper.TrayHelper.listeningClient.ToString());
            }
            ExitToolStripMenuItem.Text = AudioCopyUI_TrayHelper.TrayHelper.KeepBackendAsDefault ? Resource.Close : Resource.Shutdown;

        }

        private void toolStripTextBox1_Click(object sender, EventArgs e)
        {
            //todo: goto listening page
        }

        private void ToolStripMenuItem0_Click(object sender, EventArgs e)//launch
        {
            BootApp();
        }

        private void toolStripMenuItem3_Click(object sender, EventArgs e)//exit
        {
            //if (AudioCopyUI_TrayHelper.TrayHelper.CloseApp is not null) AudioCopyUI_TrayHelper.TrayHelper.CloseApp();

        }

        private void 默认退出选项ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (AudioCopyUI_TrayHelper.TrayHelper.KeepBackendAsDefault)
            {
                if (AudioCopyUI_TrayHelper.TrayHelper.CloseApp is not null) AudioCopyUI_TrayHelper.TrayHelper.CloseApp();

            }
            else
            {
                if (AudioCopyUI_TrayHelper.TrayHelper.Shutdown is not null) AudioCopyUI_TrayHelper.TrayHelper.Shutdown();

            }


        }

        private void 彻底关闭ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (AudioCopyUI_TrayHelper.TrayHelper.Shutdown is not null) AudioCopyUI_TrayHelper.TrayHelper.Shutdown();

        }

        void BootApp()
        {
            if (!AudioCopyUI_TrayHelper.TrayHelper.IsNotStandalone)
            {
                Process.Start(new ProcessStartInfo { FileName = "audiocopy:", UseShellExecute = true });
                Environment.Exit(0);
            }
            else
            {
                //if (!AudioCopyUI_TrayHelper.TrayHelper.GUIRunning)
                //{
                //    var result = MessageBox.Show(Resource.DisconnectWarn, Resource.Warn, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                //    if (result != DialogResult.Yes)
                //    {
                //        return;
                //    }
                //}
                if (AudioCopyUI_TrayHelper.TrayHelper.BootApp is not null) AudioCopyUI_TrayHelper.TrayHelper.BootApp();

            }
        }

        private void TrayForm_Load(object sender, EventArgs e)
        {
            this.Hide();
        }

        private void 保留后端并退出ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (AudioCopyUI_TrayHelper.TrayHelper.CloseApp is not null) AudioCopyUI_TrayHelper.TrayHelper.CloseApp();

        }
    }
}
