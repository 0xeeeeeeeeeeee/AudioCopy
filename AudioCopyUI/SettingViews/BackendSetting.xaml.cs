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
            
        }

        private async void OptionsChanged(object sender, object? e)
        {
            try
            {
                if (int.TryParse(PortBindBox.Text, out var result) && (result < 0 && result > 65535)) throw new ArgumentOutOfRangeException("Port should around 0 and 65535.");

                if (useDevelopmentMode.IsChecked == true || devMode.IsChecked == true || !string.IsNullOrWhiteSpace(customEnvironmentVars.Text))
                {
                    if (await ShowDialogue(localize("Warn"), localize("/Setting/BackendSetting_Dangerous"), localize("Cancel"), "¼ÌÐø", this))
                    {
                        useDevelopmentMode.IsChecked = false;
                        customEnvironmentVars.Text = "";
                    }
                }

                if(oldBackend.IsChecked == false)
                {
                    await ShowDialogue(localize("Info"), localize("NewBackendPrompt"), localize("Accept"), null, this);
                }

                Dictionary<string, string> config = new Dictionary<string, string>
                {
                    { "ASPNETCORE_URLS", $"http://+:{(!string.IsNullOrWhiteSpace(PortBindBox.Text) && uint.TryParse(PortBindBox.Text, out var i) && i <= 65535 ? PortBindBox.Text : "23456") }" },
                    { "ASPNETCORE_ENVIRONMENT", (useDevelopmentMode.IsChecked ?? false) ? "Development" : "Production" },
                    //{ "AudioCopy_AllowLoopbackPair", (allowLoopbackPair.IsChecked ?? false).ToString() },
                    { "AudioCopy_AllowInternetPair", (allowNonLocalPair.IsChecked ?? false).ToString() }
                };

                var pairs = customEnvironmentVars.Text.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var item in pairs)
                {
                    var kvp = item.Split('=');
                    config.Add(kvp[0], kvp[1]);
                }

                SettingUtility.SetSettings("EnableSwagger", (devMode.IsChecked ?? false).ToString());
                SettingUtility.SetSettings("backendOptions", JsonSerializer.Serialize(config));
                SettingUtility.SetSettings("backendPort", !string.IsNullOrWhiteSpace(PortBindBox.Text) && int.TryParse(PortBindBox.Text, out var _) ? PortBindBox.Text : "23456");

                if (disableCustomSettings.IsChecked == true || oldBackend.IsChecked == true)
                {
                    SettingUtility.SetSettings("ForceDefaultBackendSettings", (disableCustomSettings.IsChecked ?? false).ToString());
                    SettingUtility.SetSettings("OldBackend", (oldBackend.IsChecked ?? false).ToString());
                }
                else
                {
                    SettingUtility.SetSettings("ForceDefaultBackendSettings", "False");
                    SettingUtility.SetSettings("OldBackend", "False");
                }

                await ShowDialogue(localize("Info"), localize("/Setting/BackendSetting_RebootRequired"), localize("Accept"), null, this);
            }
            catch (Exception ex)
            {
                await ShowDialogue(localize("Info"), string.Format(localize("/Setting/BackendSettings_OptionsProblem"), ex.Message), localize("Accept"), null, this);
                return;
            }

            
        }



        private async void RebootBackend_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var c = rebootBackend.Content;
            rebootBackend.Content = new ProgressRing { IsActive = true };
            await Program.KillBackend();
            await Program.BootBackend();
            rebootBackend.Content = c;
            await ShowDialogue(localize("Info"), localize("/Setting/BackendSetting_Rebooted"), localize("Accept"), null, this);
        }

        

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (bool.Parse(SettingUtility.GetOrAddSettings("OldBackend", "False")))
            {
                oldBackend.IsChecked = true;
            }
            if (bool.Parse(SettingUtility.GetOrAddSettings("EnableSwagger", "False")))
            {
                devMode.IsChecked = true;
            }
            var cloneAsbPath = Path.Combine(LocalStateFolder, @"backend\AudioClone.Server.dll");
            var cloneAsb = Assembly.LoadFrom(cloneAsbPath);
            var cloneHash = await AlgorithmServices.ComputeFileSHA256Async(cloneAsbPath);
            BackendVersionBlock.Text = $"{localize("/Setting/BackendSetting_BackendVersion")}{Program.BackendVersionCode} \r\n ({cloneAsb.FullName} SHA256:{cloneHash})";

            var port = SettingUtility.GetOrAddSettings("backendPort", "23456");
            if (port != "23456") PortBindBox.Text = port;
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
    }
}

