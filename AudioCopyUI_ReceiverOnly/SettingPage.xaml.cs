using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Windows.Storage;
using Windows.System.Profile;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using static AudioCopyUI_ReceiverOnly.GlobalUtility;
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AudioCopyUI_ReceiverOnly
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingPage : Page
    {
        private string thanksText;

        public SettingPage()
        {
            this.InitializeComponent();
            string logFolderPath = Path.Combine(LocalStateFolder, "logs");
            var logFiles = Directory.GetFiles(logFolderPath, "*.log");
            var files = new List<string>();
            foreach (var item in logFiles.OrderByDescending((f) => new FileInfo(f).CreationTime).Select((origin,index) => index == 0 ?  origin + "(最新)" : origin ))
            {
                var m = new MenuFlyoutItem { Text = Path.GetFileName(item) };
                m.Click += viewLog_Click;
                logsMenuFlyout.Items.Add(m);
            }
            var version = Windows.ApplicationModel.Package.Current.Id.Version;
            string appVersion = $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";


            string settings = "";

            foreach (var item in ApplicationData.Current.LocalSettings.Values)
            {
                settings += $"{item.Key} : {item.Value} \r\n";
            }

            thanksText =    
                $"AudioCopy (Receiver Only) {appVersion} Copyright 0xeeeeeeeeeeee (0x12e) 2025.\r\n" +
                $"项目的部分代码来自于\"Stream What Your Hear\"(https://github.com/StreamWhatYouHear/SWYH)，创意也来自于它。\r\n" +
                $"该项目使用GNU GPLv2许可证进行许可 - 详情请查看许可证(https://github.com/0xeeeeeeeeeeee/AudioCopy/blob/master/LICENSE)" +
                $"\r\n\r\n\r\n\r\n\r\n\r\n\r\n\r\n" +
                $"调试信息：\r\n" +
                $"数据目录： {LocalStateFolder}\r\n\r\n" +
                $"设置：\r\n" +
                $"{settings}\r\n\r\n";
            thanksBox.Text = thanksText;
            if (AnalyticsInfo.VersionInfo.DeviceFamily != "Windows.Desktop")
            {
                logoIcon.Scale = new System.Numerics.Vector3(1f + (float)Program.globalScale);
            }
        }

        

        private async void resetUUID_Click(object sender, RoutedEventArgs e)
        {
            if (!await ShowDialogue("警告", "所有配对都将失效，你确定要这么做吗？", "取消", "确认", this))
            {
                SettingUtility.SetSettings("udid", AlgorithmServices.MakeRandString(128));
                await ShowDialogue("提示", "已重置，请重启应用程序", "好的", null, this);
                Program.ExitApp(true);
            }
        }

        private async void resetAllSettings_Click(object sender, RoutedEventArgs e)
        {
            if (!await ShowDialogue("警告", "你确定要这么做吗？", "取消", "确认", this))
            {
                ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
                localSettings.Values.Clear();
                await ShowDialogue("提示", "已重置，请重启应用程序", "好的", null, this);
                Program.ExitApp(true);
            }
        }

        private async void viewLog_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            string name = (e.OriginalSource as MenuFlyoutItem).Text;

            thanksBox.Text = File.ReadAllText(Path.Combine(LocalStateFolder, $@"logs\{(name.EndsWith(')') ? name.Split('(')[0] : name)}"));


            viewLog.Content = "请在下方查看，点击重置";
            viewLog.Click += (a, b) => 
            {
                thanksBox.Text = thanksText;
                viewLog.Content = "查看日志";
            };


        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/0xeeeeeeeeeeee/AudioCopy"));
        }

        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if(AnalyticsInfo.VersionInfo.DeviceFamily != "Windows.Desktop")
            {
                ContentDialog d = new ContentDialog
                {
                    Title = "提示",
                    Content = "在电脑上打开链接：https://apps.microsoft.com/detail/9P3XT4FS327L",
                    PrimaryButtonText = "在浏览器中打开",
                    CloseButtonText = "好的"
                };
                if (await d.ShowAsync() != ContentDialogResult.Primary) return;
            }
            await Windows.System.Launcher.LaunchUriAsync(new Uri("https://apps.microsoft.com/detail/9P3XT4FS327L"));

        }
    }
}
