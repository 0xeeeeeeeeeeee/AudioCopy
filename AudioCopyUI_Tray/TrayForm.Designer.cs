namespace AudioCopyUI_Tray
{
    partial class TrayForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(TrayForm));
            button1 = new Button();
            button2 = new Button();
            notifyIcon1 = new NotifyIcon(components);
            contextMenuStrip1 = new ContextMenuStrip(components);
            LaunchToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator2 = new ToolStripSeparator();
            MediaInfotoolStripMenuItem = new ToolStripMenuItem();
            ListenclientstoolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator1 = new ToolStripSeparator();
            ExitOptionstoolStripMenuItem = new ToolStripMenuItem();
            保留后端并退出ToolStripMenuItem = new ToolStripMenuItem();
            彻底关闭ToolStripMenuItem = new ToolStripMenuItem();
            ExitToolStripMenuItem = new ToolStripMenuItem();
            contextMenuStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // button1
            // 
            button1.Location = new Point(12, 12);
            button1.Name = "button1";
            button1.Size = new Size(94, 29);
            button1.TabIndex = 0;
            button1.Text = "run";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // button2
            // 
            button2.Location = new Point(112, 12);
            button2.Name = "button2";
            button2.Size = new Size(94, 29);
            button2.TabIndex = 1;
            button2.Text = "close";
            button2.UseVisualStyleBackColor = true;
            button2.Click += button2_Click;
            // 
            // notifyIcon1
            // 
            notifyIcon1.BalloonTipIcon = ToolTipIcon.Info;
            notifyIcon1.BalloonTipText = "AudioCopy";
            notifyIcon1.BalloonTipTitle = "AudioCopy";
            notifyIcon1.Icon = (Icon)resources.GetObject("notifyIcon1.Icon");
            notifyIcon1.Text = "AudioCopy";
            notifyIcon1.Visible = true;
            notifyIcon1.MouseDoubleClick += notifyIcon1_MouseDoubleClick;
            // 
            // contextMenuStrip1
            // 
            contextMenuStrip1.ImageScalingSize = new Size(20, 20);
            contextMenuStrip1.Items.AddRange(new ToolStripItem[] { LaunchToolStripMenuItem, toolStripSeparator2, MediaInfotoolStripMenuItem, ListenclientstoolStripMenuItem, toolStripSeparator1, ExitOptionstoolStripMenuItem, ExitToolStripMenuItem });
            contextMenuStrip1.Name = "contextMenuStrip1";
            contextMenuStrip1.ShowImageMargin = false;
            contextMenuStrip1.Size = new Size(203, 164);
            contextMenuStrip1.Opening += contextMenuStrip1_Opening;
            // 
            // LaunchToolStripMenuItem
            // 
            LaunchToolStripMenuItem.Name = "LaunchToolStripMenuItem";
            LaunchToolStripMenuItem.Size = new Size(202, 24);
            LaunchToolStripMenuItem.Text = "打开AudioCopy";
            LaunchToolStripMenuItem.Click += ToolStripMenuItem0_Click;
            // 
            // toolStripSeparator2
            // 
            toolStripSeparator2.Name = "toolStripSeparator2";
            toolStripSeparator2.Size = new Size(199, 6);
            // 
            // MediaInfotoolStripMenuItem
            // 
            MediaInfotoolStripMenuItem.Enabled = false;
            MediaInfotoolStripMenuItem.Name = "MediaInfotoolStripMenuItem";
            MediaInfotoolStripMenuItem.Size = new Size(202, 24);
            MediaInfotoolStripMenuItem.Text = "未知媒体 - 未知艺术家";
            // 
            // ListenclientstoolStripMenuItem
            // 
            ListenclientstoolStripMenuItem.Enabled = false;
            ListenclientstoolStripMenuItem.Name = "ListenclientstoolStripMenuItem";
            ListenclientstoolStripMenuItem.Size = new Size(202, 24);
            ListenclientstoolStripMenuItem.Text = "{0}个客户端监听中";
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new Size(199, 6);
            // 
            // ExitOptionstoolStripMenuItem
            // 
            ExitOptionstoolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { 保留后端并退出ToolStripMenuItem, 彻底关闭ToolStripMenuItem });
            ExitOptionstoolStripMenuItem.Name = "ExitOptionstoolStripMenuItem";
            ExitOptionstoolStripMenuItem.Size = new Size(202, 24);
            ExitOptionstoolStripMenuItem.Text = "退出";
            ExitOptionstoolStripMenuItem.Click += toolStripMenuItem3_Click;
            // 
            // 保留后端并退出ToolStripMenuItem
            // 
            保留后端并退出ToolStripMenuItem.Name = "保留后端并退出ToolStripMenuItem";
            保留后端并退出ToolStripMenuItem.Size = new Size(224, 26);
            保留后端并退出ToolStripMenuItem.Text = "保留后端并退出";
            保留后端并退出ToolStripMenuItem.Click += 保留后端并退出ToolStripMenuItem_Click;
            // 
            // 彻底关闭ToolStripMenuItem
            // 
            彻底关闭ToolStripMenuItem.Name = "彻底关闭ToolStripMenuItem";
            彻底关闭ToolStripMenuItem.Size = new Size(224, 26);
            彻底关闭ToolStripMenuItem.Text = "彻底关闭";
            彻底关闭ToolStripMenuItem.Click += 彻底关闭ToolStripMenuItem_Click;
            // 
            // ExitToolStripMenuItem
            // 
            ExitToolStripMenuItem.Name = "ExitToolStripMenuItem";
            ExitToolStripMenuItem.Size = new Size(202, 24);
            ExitToolStripMenuItem.Text = "默认退出选项";
            ExitToolStripMenuItem.Click += 默认退出选项ToolStripMenuItem_Click;
            // 
            // TrayForm
            // 
            AutoScaleDimensions = new SizeF(9F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(419, 102);
            Controls.Add(button2);
            Controls.Add(button1);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "TrayForm";
            ShowInTaskbar = false;
            Text = "AudioCopy tray module";
            contextMenuStrip1.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private Button button1;
        private Button button2;
        private NotifyIcon notifyIcon1;
        private ContextMenuStrip contextMenuStrip1;
        private ToolStripMenuItem LaunchToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator2;
        private ToolStripMenuItem MediaInfotoolStripMenuItem;
        private ToolStripMenuItem ListenclientstoolStripMenuItem;
        private ToolStripSeparator toolStripSeparator1;
        private ToolStripMenuItem ExitOptionstoolStripMenuItem;
        private ToolStripMenuItem 保留后端并退出ToolStripMenuItem;
        private ToolStripMenuItem 彻底关闭ToolStripMenuItem;
        private ToolStripMenuItem ExitToolStripMenuItem;
    }
}
