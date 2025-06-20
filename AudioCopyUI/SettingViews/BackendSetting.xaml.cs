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

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using Windows.Foundation;
using Windows.Foundation.Collections;
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
            
        }

        private async void OptionsChanged(object sender, object? e)
        {
            try
            {
                if (int.TryParse(PortBindBox.Text, out var result) && (result < 0 && result > 65535)) throw new ArgumentOutOfRangeException("Port should around 0 and 65535.");

                if (devMode.IsChecked == true )
                {
                    if (await ShowDialogue(localize("Warn"), localize("/Setting/BackendSetting_Dangerous"), localize("Cancel"), localize("Accept"), this))
                    {
                        devMode.IsChecked = false;
                    }
                }


                SettingUtility.SetSettings("EnableSwagger", (devMode.IsChecked ?? false).ToString());
                SettingUtility.SetSettings("NoDiscover", (noDiscover.IsChecked ?? false).ToString());
                SettingUtility.SetSettings("NoNewPair", (noNewPairing.IsChecked ?? false).ToString());


                SettingUtility.SetSettings("backendPort", !string.IsNullOrWhiteSpace(PortBindBox.Text) && int.TryParse(PortBindBox.Text, out var _) ? PortBindBox.Text : "23456");                

                await ShowDialogue(localize("Info"), localize("RebootRequired"), localize("Accept"), null, this);
            }
            catch (Exception ex)
            {
                await ShowDialogue(localize("Info"), string.Format(localize("/Setting/BackendSettings_OptionsProblem"), ex.Message), localize("Accept"), null, this);
                return;
            }

            
        }



        

        

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (bool.Parse(SettingUtility.GetOrAddSettings("EnableSwagger", "False")))
            {
                devMode.IsChecked = true;
            }
            if (bool.Parse(SettingUtility.GetOrAddSettings("NoDiscover", "False")))
            {
                noDiscover.IsChecked = true;
            }
            if (bool.Parse(SettingUtility.GetOrAddSettings("NoNewPair", "False")))
            {
                noNewPairing.IsChecked = true;
            }
            string cloneInfo = "";

            var backendAsbPath = Path.Combine(LocalStateFolder, @"backend\AudioClone.Server.exe");
            try
            {
                var fileVersionInfo = FileVersionInfo.GetVersionInfo(backendAsbPath);
                var backendVersion = fileVersionInfo.FileVersion ?? "Unknown";
                cloneInfo = $"v{backendVersion}";
            }
            catch
            {
                cloneInfo = "Unavailable";
            }

            BackendVersionBlock.Text = $"{localize("/Setting/BackendSetting_BackendVersion", $"v{Program.BackendAPIVersion}", cloneInfo)}";
            BackendVersionBlock.Text += $" SHA256:{await AlgorithmServices.ComputeFileSHA256Async(backendAsbPath)}";


            var port = SettingUtility.GetOrAddSettings("backendPort", "23456");
            if (port != "23456") PortBindBox.Text = port;

        }
    }
}

