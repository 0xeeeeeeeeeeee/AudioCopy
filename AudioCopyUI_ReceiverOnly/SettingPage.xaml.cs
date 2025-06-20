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
using static AudioCopyUI_ReceiverOnly.Logger;
using static AudioCopyUI_ReceiverOnly.Localizer;
using Windows.UI.Xaml.Media.Imaging;
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
        private string viewLogContent = "";

        public SettingPage()
        {
            this.InitializeComponent();

            string logFolderPath = Path.Combine(LocalStateFolder, "logs");
            var logFiles = Directory.GetFiles(logFolderPath, "*.log");
            var files = new List<string>();
            foreach (var item in logFiles.OrderByDescending((f) => new FileInfo(f).CreationTime).Select((origin,index) => index == 0 ? $"{origin}({localize("/Setting/ReceiverOnly_Lastest")})" : origin ))
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
            thanksText = string.Format(localize("/Setting/AboutString").Replace("[line]", Environment.NewLine), Assembly.GetExecutingAssembly().GetName().Version, LocalStateFolder, settings, ___LogPath___);
            thanksBox.Text = thanksText;

            foreach (var item in Localizer.locate)
            {
                var i = new MenuFlyoutItem { Text = item };
                i.Click += LangChanged;
                OptionsFlyout.Items.Add(i);

            }

            if (bool.Parse(SettingUtility.GetOrAddSettings("DisableShowHostSMTCInfo", "False")))
            {
                disableShowHostSMTCInfo.IsChecked = true;
            }
        }

        private async void LangChanged(object sender, RoutedEventArgs e)
        {
            var text = (e.OriginalSource as MenuFlyoutItem).Text;

            var id = Localizer.locateId[Array.IndexOf(Localizer.locate, text)];

            await ShowDialogue("Info", "Reboot to apply these changes.\r\nIn some case, you need to reboot this application for multiple times.", "OK", null, this);

            await Program.ChangeLang(id);


        }



        private async void resetUUID_Click(object sender, RoutedEventArgs e)
        {
            if (!await ShowDialogue(localize("Warn"), localize("/Setting/AdvancedSetting_SureReset"), localize("Cancel"), localize("Accept"), this))
            {
                SettingUtility.SetSettings("udid", AlgorithmServices.MakeRandString(128));
                await ShowDialogue(localize("Info"), localize("/Setting/AdvancedSetting_Reset"), localize("Accept"), null, this);
                Program.ExitApp(true);
            }
        }

        private async void resetAllSettings_Click(object sender, RoutedEventArgs e)
        {
            if (!await ShowDialogue(localize("Warn"), localize("/Setting/AdvancedSetting_Sure"), localize("Cancel"), localize("Accept"), this))
            {
                ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
                localSettings.Values.Clear();
                await ShowDialogue(localize("Info"), localize("/Setting/AdvancedSetting_Reseted"), localize("Accept"), null, this);
                Program.ExitApp(true);
            }
        }

        private async void viewLog_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            viewLogContent = viewLog.Content as string;
            string name = (e.OriginalSource as MenuFlyoutItem).Text;

            thanksBox.Text = File.ReadAllText(Path.Combine(LocalStateFolder, $@"logs\{(name.EndsWith(')') ? name.Split('(')[0] : name)}"));


            viewLog.Content = localize("/Setting/ReceiverOnly_ViewDownside");
            viewLog.Click += (a, b) =>
            {
                thanksBox.Text = thanksText;
                viewLog.Content = viewLogContent;
            };


        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/0xeeeeeeeeeeee/AudioCopy"));
        }

        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (AnalyticsInfo.VersionInfo.DeviceFamily != "Windows.Desktop")
            {
                ContentDialog d = new ContentDialog
                {
                    Title = localize("Info"),
                    Content = localize("/Setting/ReceiverOnly_LaunchLink") + " https://apps.microsoft.com/detail/9P3XT4FS327L",
                    PrimaryButtonText = localize("/Setting/ReceiverOnly_OpenInBrowser"),
                    CloseButtonText = localize("Accept")
                };
                if (await d.ShowAsync() != ContentDialogResult.Primary) return;
            }
            await Windows.System.Launcher.LaunchUriAsync(new Uri("https://apps.microsoft.com/detail/9P3XT4FS327L?cid=receiverOnly"));

        }

        private void disableShowHostSMTCInfo_Click(object sender, RoutedEventArgs e)
        {
            SettingUtility.SetSettings("DisableShowHostSMTCInfo", (disableShowHostSMTCInfo.IsChecked ?? false).ToString());

        }
    }
}
