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
        }

        private void OptionsChanged(object sender, RoutedEventArgs e)
        {
            SettingUtility.SetSettings("AlwaysAllowMP3", (forceMP3Audio.IsChecked ?? false).ToString());
            SettingUtility.SetSettings("ShowAllAdapter", (showNonLocalAddress.IsChecked ?? false).ToString());
            SettingUtility.SetSettings("KeepBackendRun", (keepBackendRun.IsChecked ?? false).ToString());

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

        private async void resetTokens_Click(object sender, RoutedEventArgs e)
        {
            if (!await ShowDialogue("警告", "所有配对都将失效，你确定要这么做吗？", "取消", "确认", this))
            {
                SettingUtility.SetSettings("deviceMapping", "{}");
                Program.KillBackend();
                File.Delete(Path.Combine(LocalStateFolder, @"wwwroot\tokens.json"));
                await ShowDialogue("提示", "已重置，请重启应用程序", "好的", null, this);
                Program.ExitApp(true);
            }
        }

        private async void resetAllSettings_Click(object sender, RoutedEventArgs e)
        {
            if (!await ShowDialogue("警告", "你确定要这么做吗？", "取消", "确认", this))
            {
                Program.KillBackend();
                File.Delete(Path.Combine(LocalStateFolder, @"wwwroot\tokens.json"));
                ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
                localSettings.Values.Clear();
                await ShowDialogue("提示", "已重置，请重启应用程序", "好的", null, this);
                Program.ExitApp(true);
            }
        }

        private void openDataFolder_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo { FileName = LocalStateFolder, UseShellExecute = true });
        }

        private async void resetBackend_Click(object sender, RoutedEventArgs e)
        {
            if (!await ShowDialogue("警告", "你确定要这么做吗？", "取消", "确认", this))
            {
                Program.KillBackend();
                await Task.Delay(1000);
                Directory.Delete(Path.Combine(LocalStateFolder, @"backend"), true);
                SettingUtility.SetSettings("ForceUpgradeBackend", "True");
                await ShowDialogue("提示", "已重置，请重启应用程序", "好的", null, this);
                Program.ExitApp(true);
            }
        }
    }
}

