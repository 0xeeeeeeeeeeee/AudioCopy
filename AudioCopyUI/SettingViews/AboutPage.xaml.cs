/*
*	 File: AboutPage.xaml.cs
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
using System.Diagnostics;
using System.Reflection;
using Windows.Storage;
using Windows.ApplicationModel.DataTransfer;
using Microsoft.UI.Dispatching;
using System.Threading.Tasks;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AudioCopyUI.SettingViews
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class AboutPage : Page
    {
        public AboutPage()
        {
            this.InitializeComponent();

            string settings = "";

            foreach (var item in ApplicationData.Current.LocalSettings.Values)
            {
                settings += $"{item.Key} : {item.Value} \r\n";
            }

            thanksBox.Text = string.Format(localize("/Setting/AboutString").Replace("[line]", Environment.NewLine), Assembly.GetExecutingAssembly().GetName().Version, LocalStateFolder, settings, ___LogPath___);
         
//$$"""
//AudioCopy {{Assembly.GetExecutingAssembly().GetName().Version}} Copyright 0xeeeeeeeeeeee (0x12e) 2025.
//项目的部分代码来自于"Stream What Your Hear"(https://github.com/StreamWhatYouHear/SWYH)，创意也来自于它。
//该项目使用GNU GPLv2许可证进行许可 - 详情请查看许可证(https://github.com/0xeeeeeeeeeeee/AudioCopy/blob/master/LICENSE)







//调试信息：
//数据目录： {{LocalStateFolder}}

//设置：
//{{settings}}

//最新的日志路径：
//{{___LogPath___}}

//""";

            
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            bool result = await Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/0xeeeeeeeeeeee/AudioCopy"));
        }

        private async void MenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            Program.KillBackend();
            await ShowDialogue("info", "backend is now killed!", "ok", null, this);
        }

        private void MenuFlyoutItem_Click_1(object sender, RoutedEventArgs e)
        {
            Log(thanksBox.Text);
            Process.Start(new ProcessStartInfo { FileName = ___LogPath___ , UseShellExecute = true });
        }

        private async void MenuFlyoutItem_Click_2(object sender, RoutedEventArgs e)
        {
            if(Localizer.current == "zh-CN")
            {
                _ = await Windows.System.Launcher.LaunchUriAsync(new Uri("https://www.bilibili.com/video/BV1GJ411x7h7"));
            }
            else
            {
                _ = await Windows.System.Launcher.LaunchUriAsync(new Uri("https://www.youtube.com/watch?v=dQw4w9WgXcQ"));
            }
        }
    }
}

