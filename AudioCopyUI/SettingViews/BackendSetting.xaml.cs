/*
*	 File: BackendSetting.xaml.cs
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
using System.Net.Http;
using System.Runtime.ConstrainedExecution;
using System.Reflection;
using System.Text.Json;
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AudioCopyUI.SettingViews
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class BackendSetting : Page
    {
        private HttpClient c = new HttpClient();

        public BackendSetting()
        {
            this.InitializeComponent();
            //c.BaseAddress = new Uri(SettingUtility.GetSetting("sourceAddress"));
            //c.Timeout = TimeSpan.FromSeconds(5);
            //var rsp = c.GetAsync($"/RequirePair?udid=AudioCopy&name=none").GetAwaiter().GetResult();
            //if (rsp.IsSuccessStatusCode)
            //{
            //    var rspString = new StreamReader(rsp.Content.ReadAsStream()).ReadToEnd();
            //}
            var backendAsbPath = Path.Combine(LocalStateFolder, @"backend\libAudioCopy-Backend.dll");
            var backendVersion = Assembly.LoadFrom(backendAsbPath).GetName().Version;
            var libAudioCopyAsbPath = Path.Combine(LocalStateFolder, @"backend\libAudioCopy.dll");
            var libAudioCopyVersion = Assembly.LoadFrom(backendAsbPath).GetName().Version;
            BackendVersionBlock.Text += $"{Program.BackendVersionCode} (backend:{backendVersion} libAudioCopy:{libAudioCopyVersion})";

            if (!SettingUtility.Exists("backendOptions")) return;
            var options = JsonSerializer.Deserialize<Dictionary<string, string>>(SettingUtility.GetSetting("backendOptions")) ?? new();
            foreach (var item in options)
            {
                switch (item.Key)
                {
                    case "ASPNETCORE_ENVIRONMENT":
                        if (item.Value == "Development") useDevelopmentMode.IsChecked = true;
                        break;
                    case "ASPNETCORE_URLS":
                        break;
                    case "AudioCopy_AllowLoopbackPair":
                        //if (item.Value == "True") allowLoopbackPair.IsChecked = true;
                        break;
                    case "AudioCopy_AllowInternetPair":
                        if (item.Value == "True") allowNonLocalPair.IsChecked = true;
                        break;
                    default:
                        customEnvironmentVars.Text += $"{item.Key}={item.Value},";
                        break;
                }
            }
            if (!string.IsNullOrWhiteSpace(customEnvironmentVars.Text)) customEnvironmentVars.Text = customEnvironmentVars.Text.Substring(0, customEnvironmentVars.Text.Length - 1);

        }

        private async void OptionsChanged(object sender, object? e)
        {
            if (disableCustomSettings.IsChecked == true)
            {
                SettingUtility.SetSettings("ForceDefaultBackendSettings", "True");
                await ShowDialogue("提示", "重新启动后端来应用更改", "好的", null, this);
                return;
            }
            else
            {
                SettingUtility.SetSettings("ForceDefaultBackendSettings", "False");
            }

            if (useDevelopmentMode.IsChecked == true || !string.IsNullOrWhiteSpace(customEnvironmentVars.Text))
            {
                if (await ShowDialogue("警告", "这些设置非常危险，你确定要设置他们吗？", "取消", "继续", this))
                {
                    useDevelopmentMode.IsChecked = false;
                    customEnvironmentVars.Text = "";
                }
            }

            Dictionary<string, string> config = new Dictionary<string, string>
            {
                { "ASPNETCORE_URLS", $"http://{(string.IsNullOrWhiteSpace(AddressBindBox.Text)? "+" : AddressBindBox.Text )}:{(!string.IsNullOrWhiteSpace(PortBindBox.Text) && int.TryParse(PortBindBox.Text, out var _) ? PortBindBox.Text : "23456") }" },
                { "ASPNETCORE_ENVIRONMENT", (useDevelopmentMode.IsChecked ?? false) ? "Development" : "Production" },
                //{ "AudioCopy_AllowLoopbackPair", (allowLoopbackPair.IsChecked ?? false).ToString() },
                { "AudioCopy_AllowInternetPair", (allowNonLocalPair.IsChecked ?? false).ToString() }
            };
            try
            {
                var pairs = customEnvironmentVars.Text.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var item in pairs)
                {
                    var kvp = item.Split('=');
                    config.Add(kvp[0], kvp[1]);
                }
            }
            catch (Exception)
            {
                await ShowDialogue("提示", "你的自定义选项似乎有问题，请修改。", "好的", null, this);
                return;
            }

            SettingUtility.SetSettings("backendOptions", JsonSerializer.Serialize(config));
            await ShowDialogue("提示", "重新启动后端来应用更改", "好的", null, this);

        }



        private async void RebootBackend_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await Program.BootBackend();
            await ShowDialogue("提示", "完成\r\n如果你仍然遇到问题，请尝试更新或者完全重置后端", "好的", null, this);
        }

        private async void UpgradeBackend_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            SettingUtility.SetSettings("ForceUpgradeBackend", "True");
            await ShowDialogue("提示", "重新启动应用程序来应用更改", "好的", null, this);
        }
    }
}

