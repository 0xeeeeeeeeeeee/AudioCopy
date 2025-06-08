/*
*	 File: AdvancedSetting.xaml.cs
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
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System.Xml.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using System.Diagnostics;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AudioCopyUI.SettingViews
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class AdvancedSetting : Page
    {


        public AdvancedSetting()
        {
            this.InitializeComponent();

            if(bool.Parse(SettingUtility.GetOrAddSettings("AlwaysAllowMP3", "False")))
            {
                forceMP3Audio.IsChecked = true;
            }

            if (bool.Parse(SettingUtility.GetOrAddSettings("ShowAllAdapter", "False")))
            {
                showNonLocalAddress.IsChecked = true;
            }

            if (bool.Parse(SettingUtility.GetOrAddSettings("KeepBackendRun", "False")))
            {
                keepBackendRun.IsChecked = true;
            }

            if(bool.Parse(SettingUtility.GetOrAddSettings("SkipSplash", "False")))
            {
                skipSplashScreen.IsChecked = true;
            }

            if (bool.Parse(SettingUtility.GetOrAddSettings("DisableShowHostSMTCInfo", "False")))
            {
                disableShowHostSMTCInfo.IsChecked = true;
            }

            

            foreach (var item in Localizer.locate)
            {
                var i = new MenuFlyoutItem { Text = item };
                i.Click += LangChanged;
                OptionsFlyout.Items.Add(i);

            }
        }

        private async void LangChanged(object sender, RoutedEventArgs e)
        {
            var text = (e.OriginalSource as MenuFlyoutItem).Text;

            var id = Localizer.locateId[Array.IndexOf(Localizer.locate, text)];

            await Program.ChangeLang(id);

            
        }

        private void OptionsChanged(object sender, RoutedEventArgs e)
        {
            SettingUtility.SetSettings("AlwaysAllowMP3", (forceMP3Audio.IsChecked ?? false).ToString());
            SettingUtility.SetSettings("ShowAllAdapter", (showNonLocalAddress.IsChecked ?? false).ToString());
            SettingUtility.SetSettings("KeepBackendRun", (keepBackendRun.IsChecked ?? false).ToString());
            SettingUtility.SetSettings("SkipSplash", (skipSplashScreen.IsChecked ?? false).ToString());
            SettingUtility.SetSettings("DisableShowHostSMTCInfo", (disableShowHostSMTCInfo.IsChecked ?? false).ToString());

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

        private async void resetTokens_Click(object sender, RoutedEventArgs e)
        {
            if (!await ShowDialogue(localize("Warn"), localize("/Setting/AdvancedSetting_SureReset"), localize("Cancel"), localize("Accept"), this))
            {
                SettingUtility.SetSettings("deviceMapping", "{}");
                Program.KillBackend();
                File.Delete(Path.Combine(LocalStateFolder, @"wwwroot\tokens.json"));
                await ShowDialogue(localize("Info"), localize("/Setting/AdvancedSetting_Reseted"), localize("Accept"), null, this);
                Program.ExitApp(true);
            }
        }

        private async void resetAllSettings_Click(object sender, RoutedEventArgs e)
        {
            if (!await ShowDialogue(localize("Warn"), localize("/Setting/AdvancedSetting_Sure"), localize("Cancel"), localize("Accept"), this))
            {
                (e.OriginalSource as Button).Content = new ProgressRing { IsActive = true };
                await Program.KillBackend();
                //ClearDirectory(LocalStateFolder);
                ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
                localSettings.Values.Clear();
                SettingUtility.SetSettings("ResetEverything", "True");
                await ShowDialogue(localize("Info"), localize("/Setting/AdvancedSetting_Reseted"), localize("Accept"), null, this);
                Program.ExitApp(true);
            }
        }

        private void openDataFolder_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo { FileName = LocalStateFolder, UseShellExecute = true });
        }

        private async void resetBackend_Click(object sender, RoutedEventArgs e)
        {
            if (!await ShowDialogue(localize("Warn"), localize("/Setting/AdvancedSetting_Sure"), localize("Cancel"), localize("Accept"), this))
            {
                await Program.KillBackend();
                await Task.Delay(1000);
                Directory.Delete(Path.Combine(LocalStateFolder, @"backend"), true);
                SettingUtility.SetSettings("ForceUpgradeBackend", "True");
                await ShowDialogue(localize("Info"), localize("/Setting/AdvancedSetting_Reseted"), localize("Accept"), null, this);
                Program.ExitApp(true);
            }
        }

        private async void UpgradeBackend_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            SettingUtility.SetSettings("ForceUpgradeBackend", "True");
            await ShowDialogue(localize("Info"), localize("/Setting/BackendSetting_RebootRequired"), localize("Accept"), null, this);
            Program.ExitApp(true);
        }

        public static void ClearDirectory(string path)
        {
            if (!Directory.Exists(path))
                return;

            foreach (var file in Directory.GetFiles(path))
            {
                File.Delete(file);
            }

            foreach (var dir in Directory.GetDirectories(path))
            {
                Directory.Delete(dir, true);
            }
        }
    }
}

