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




using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AudioCopyUI
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingPage : Page
    {
        public SettingPage()
        {
            this.InitializeComponent();
            navigationView.IsSettingsVisible = false;
            ContentFrame.Navigate(typeof(SettingViews.AudioQuality));


        }

        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                //ContentFrame.Navigate(typeof(SettingViews.DebugPage));

            }
            else
            {
                var selectedItem = args.SelectedItem as NavigationViewItem;
                if (selectedItem != null && selectedItem.Tag != null)
                {
                    string tag = selectedItem.Tag.ToString();
                    switch (tag)
                    {
                        case "Page1":
                            ContentFrame.Navigate(typeof(SettingViews.AudioQuality));
                            break;
                        case "Page2":
                            ContentFrame.Navigate(typeof(SettingViews.BackendSetting));
                            break;
                        case "Page3":
                            ContentFrame.Navigate(typeof(SettingViews.AdvancedSetting));
                            break;
                        case "PageAbout":
                            ContentFrame.Navigate(typeof(SettingViews.AboutPage));
                            break;
                        
                        default:
                            break;
                    }
                }
            }
        }

        private async void NavigationView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {

            this.Frame.Navigate(typeof(ReceivePage));


        }

    }
}
