using System.Diagnostics;
using System.Windows.Forms;
using static AudioCopyUI_MiddleWare.TrayHelper;

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
            if (AudioCopyUI_MiddleWare.TrayHelper.CloseApp is not null) AudioCopyUI_MiddleWare.TrayHelper.CloseApp();
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            BootApp();
        }

        private void contextMenuStrip1_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (AudioCopyUI_MiddleWare.TrayHelper.GUIRunning == false)
            {
                if (IsNotStandalone) return;
            }
            if (!AudioCopyUI_MiddleWare.TrayHelper.IsNotStandalone)
            {
                //MediaInfotoolStripMenuItem.Visible = false;
            }

            LaunchToolStripMenuItem.Text = Resource.Launch;
            //MediaInfotoolStripMenuItem.Text = "Unknown";
            //ListenclientstoolStripMenuItem.Text = Resource.ListeningClients;
            //ListenclientstoolStripMenuItem.Visible = false;
            ExitOptionstoolStripMenuItem.Text = Resource.Exit;
            ExitToolStripMenuItem.Click += 默认退出选项ToolStripMenuItem_Click;
            ExitToolStripMenuItem.Text = AudioCopyUI_MiddleWare.TrayHelper.KeepBackendAsDefault ? Resource.Close : Resource.Shutdown;
            彻底关闭ToolStripMenuItem.Text = Resource.Shutdown;
            保留后端并退出ToolStripMenuItem.Text = Resource.Close;
            RebootToolStripMenuItem1.Text = Resource.Reboot;
            //if (AudioCopyUI_MiddleWare.TrayHelper.GetSMTC is not null)
            //{
            //    AudioCopyUI_MiddleWare.TrayHelper.GetSMTC();
            //    MediaInfotoolStripMenuItem.Text = $"{AudioCopyUI_MiddleWare.TrayHelper.Title} - {AudioCopyUI_MiddleWare.TrayHelper.Artist}";
            //    ListenclientstoolStripMenuItem.Text = string.Format(ListenclientstoolStripMenuItem.Text ?? "{0} clients listening", AudioCopyUI_MiddleWare.TrayHelper.listeningClient.ToString());
            //}
            ExitToolStripMenuItem.Text = AudioCopyUI_MiddleWare.TrayHelper.KeepBackendAsDefault ? Resource.Close : Resource.Shutdown;

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
            //if (AudioCopyUI_MiddleWare.TrayHelper.CloseApp is not null) AudioCopyUI_MiddleWare.TrayHelper.CloseApp();

        }

        private void 默认退出选项ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (AudioCopyUI_MiddleWare.TrayHelper.KeepBackendAsDefault)
            {
                if (AudioCopyUI_MiddleWare.TrayHelper.CloseApp is not null) AudioCopyUI_MiddleWare.TrayHelper.CloseApp();

            }
            else
            {
                if (AudioCopyUI_MiddleWare.TrayHelper.NoKeepClone)
                {
                    var result = MessageBox.Show(Resource.DisconnectWarn, Resource.Warn, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (result != DialogResult.Yes)
                    {
                        return;
                    }
                }
                if (AudioCopyUI_MiddleWare.TrayHelper.Shutdown is not null) AudioCopyUI_MiddleWare.TrayHelper.Shutdown();

            }


        }

        private void 彻底关闭ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (AudioCopyUI_MiddleWare.TrayHelper.NoKeepClone)
            {
                var result = MessageBox.Show(Resource.DisconnectWarn, Resource.Warn, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result != DialogResult.Yes)
                {
                    return;
                }
            }
            if (AudioCopyUI_MiddleWare.TrayHelper.Shutdown is not null) AudioCopyUI_MiddleWare.TrayHelper.Shutdown();

        }

        void BootApp()
        {
            if (!AudioCopyUI_MiddleWare.TrayHelper.IsNotStandalone)
            {
                Process.Start(new ProcessStartInfo { FileName = "audiocopy:", UseShellExecute = true });
                Environment.Exit(0);
            }
            else
            {

                if (AudioCopyUI_MiddleWare.TrayHelper.BootApp is not null) AudioCopyUI_MiddleWare.TrayHelper.BootApp();

            }
        }

        private void TrayForm_Load(object sender, EventArgs e)
        {
            this.Hide();
        }

        private void 保留后端并退出ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (AudioCopyUI_MiddleWare.TrayHelper.CloseApp is not null) AudioCopyUI_MiddleWare.TrayHelper.CloseApp();

        }

        private void RebootToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (AudioCopyUI_MiddleWare.TrayHelper.RebootApp is not null) AudioCopyUI_MiddleWare.TrayHelper.RebootApp();

        }
    }
}
