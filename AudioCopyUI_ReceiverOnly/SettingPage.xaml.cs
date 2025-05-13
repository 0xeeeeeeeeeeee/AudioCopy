/*
*	 File: SettingPage.xaml.cs
*	 Website: https://github.com/0xeeeeeeeeeeee/AudioCopy
*	 Copyright 2024-2025 (C) 0xeeeeeeeeeeee (0x12e)
*
*   This file is part of AudioCopy
*	 
*	 AudioCopy is free software: you can redistribute it and/or modify
*	 it under the terms of the GNU General Public License as published by
*	 the Free Software Foundation, either version 2 of the License, or
*	 (at your option) any later version.
*	 
*	 AudioCopy is distributed in the hope that it will be useful,
*	 but WITHOUT ANY WARRANTY; without even the implied warranty of
*	 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
*	 GNU General Public License for more details.
*	 
*	 You should have received a copy of the GNU General Public License
*	 along with AudioCopy. If not, see <http://www.gnu.org/licenses/>.
*/


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
            foreach (var item in logFiles.OrderByDescending((f) => new FileInfo(f).CreationTime).Select((origin,index) => index == 0 ?  origin + "(����)" : origin ))
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
                $"��Ŀ�Ĳ��ִ���������\"Stream What Your Hear\"(https://github.com/StreamWhatYouHear/SWYH)������Ҳ����������\r\n" +
                $"����Ŀʹ��GNU GPLv2���֤������� - ������鿴���֤(https://github.com/0xeeeeeeeeeeee/AudioCopy/blob/master/LICENSE)" +
                $"\r\n\r\n\r\n\r\n\r\n\r\n\r\n\r\n" +
                $"������Ϣ��\r\n" +
                $"����Ŀ¼�� {LocalStateFolder}\r\n\r\n" +
                $"���ã�\r\n" +
                $"{settings}\r\n\r\n";
            thanksBox.Text = thanksText;
            if (AnalyticsInfo.VersionInfo.DeviceFamily != "Windows.Desktop")
            {
                logoIcon.Scale = new System.Numerics.Vector3(1f + (float)Program.globalScale);
            }
        }

        

        private async void resetUUID_Click(object sender, RoutedEventArgs e)
        {
            if (!await ShowDialogue("����", "������Զ���ʧЧ����ȷ��Ҫ��ô����", "ȡ��", "ȷ��", this))
            {
                SettingUtility.SetSettings("udid", AlgorithmServices.MakeRandString(128));
                await ShowDialogue("��ʾ", "�����ã�������Ӧ�ó���", "�õ�", null, this);
                Program.ExitApp(true);
            }
        }

        private async void resetAllSettings_Click(object sender, RoutedEventArgs e)
        {
            if (!await ShowDialogue("����", "��ȷ��Ҫ��ô����", "ȡ��", "ȷ��", this))
            {
                ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
                localSettings.Values.Clear();
                await ShowDialogue("��ʾ", "�����ã�������Ӧ�ó���", "�õ�", null, this);
                Program.ExitApp(true);
            }
        }

        private async void viewLog_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            string name = (e.OriginalSource as MenuFlyoutItem).Text;

            thanksBox.Text = File.ReadAllText(Path.Combine(LocalStateFolder, $@"logs\{(name.EndsWith(')') ? name.Split('(')[0] : name)}"));


            viewLog.Content = "�����·��鿴���������";
            viewLog.Click += (a, b) => 
            {
                thanksBox.Text = thanksText;
                viewLog.Content = "�鿴��־";
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
                    Title = "��ʾ",
                    Content = "�ڵ����ϴ����ӣ�https://apps.microsoft.com/detail/9P3XT4FS327L",
                    PrimaryButtonText = "��������д�",
                    CloseButtonText = "�õ�"
                };
                if (await d.ShowAsync() != ContentDialogResult.Primary) return;
            }
            await Windows.System.Launcher.LaunchUriAsync(new Uri("https://apps.microsoft.com/detail/9P3XT4FS327L"));

        }
    }
}
