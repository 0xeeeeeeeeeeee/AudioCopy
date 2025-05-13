/*
*	 File: MainPage.xaml.cs
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
using Windows.Foundation;
using Windows.System.Profile;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AudioCopyUI_ReceiverOnly
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {

            this.InitializeComponent();

            var scaleFactor = Program.globalScale;
            if (!Program.isPC)
            {
                NavView1.Visibility = Visibility.Collapsed;
                var bounds = Window.Current.Bounds;
                mainPanel.Height = bounds.Height * scaleFactor;
                mainPanel.Width = bounds.Width * scaleFactor;
            }
            else
            {
                mainView.Visibility = Visibility.Collapsed;
            }

            (Program.isPC ? NavView1 : NavView).SelectedItem = NavView.MenuItems[0];
            Navigate(typeof(ReceivePage));
        }


        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                Navigate(typeof(SettingPage));
                return;
            }

            if (args.SelectedItem is NavigationViewItem selectedItem)
            {

                string pageTag = selectedItem.Tag.ToString();
                switch (pageTag)
                {
                    case "ReceivePage":
                        Navigate(typeof(ReceivePage));
                        break;
                    case "PairPage":
                        Navigate(typeof(PairingPage));
                        break;
                }
            }
        }

        private void Navigate(Type type)
        {
            if (Program.isPC) PageFrame1.Navigate(type);
            else PageFrame.Navigate(type);
        }
    }
}
