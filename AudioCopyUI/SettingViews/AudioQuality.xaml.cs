/*
*	 File: AudioQuality.xaml.cs
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

namespace AudioCopyUI.SettingViews
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class AudioQuality : Page
    {
        public AudioQuality()
        {
            this.InitializeComponent();
            bitrate = 0;
            samplerate = 0;
            channels = 0;
            switch (SettingUtility.GetOrAddSettings("resampleType", "1"))
            {
                case "1":
                    resampleOption_1.IsChecked = true;
                    break;
                case "2":
                    resampleOption_2.IsChecked = true;
                    break;
                case "3":
                    resampleOption_3.IsChecked = true;
                    break;
                default:
                    resampleOption_1.IsChecked = true;
                    break;
            }


        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var token = SettingUtility.GetOrAddSettings("udid", AlgorithmServices.MakeRandString(128));
                HttpClient c = new();
                c.BaseAddress = new($"http://127.0.0.1:{SettingUtility.GetOrAddSettings("defaultPort", "23456")}/");
                var rsp = await c.GetAsync($"api/audio/GetAudioFormat?token={token}");
                AudioQualityObject body = JsonSerializer.Deserialize<AudioQualityObject>(new StreamReader(rsp.Content.ReadAsStream()).ReadToEnd());
                var text = $"{body.channels} 通道，{body.bitsPerSample} 位，{body.sampleRate} Hz ";
                defaultAudioQualityBlock.Text += text;
            }
            catch (Exception)
            {
                defaultAudioQualityBlock.Text += "目前不可用";
            }
        }

        int bitrate, samplerate, channels;

        private void itemSelected(UIElement sender, DropCompletedEventArgs args)
        {
            audioQualityDropdown.Content = (args.OriginalSource as MenuFlyoutItem).Text;
        }

        private void audioQualityDropdown_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private async void resampleOption_Click(object sender, RoutedEventArgs e)
        {
            var id = (e.OriginalSource as RadioButton).Name.Split('_')[1];
            if (id == "1")
            {
                bitrate = -1;
                samplerate = -1;
                channels = -1;
                await Save();

            }
            //await ShowDialogue(id, id, id, id, this);
            SettingUtility.SetSettings("resampleType", id);
        }

        private async void MenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            var text = (e.OriginalSource as MenuFlyoutItem).Text;
            audioQualityDropdown.Content = text;
            if (text.Contains('(')) text = text.Split('(')[0];
            var parts = text.Split('，');
            if (parts.Length == 3)
            {
                // Extract channels
                if (int.TryParse(parts[0].Replace("通道", "").Trim(), out int parsedChannels))
                {
                    channels = parsedChannels;
                }

                // Extract bitrate
                if (int.TryParse(parts[1].Replace("位", "").Trim(), out int parsedBitrate))
                {
                    bitrate = parsedBitrate;
                }

                // Extract samplerate
                if (int.TryParse(parts[2].Replace("Hz", "").Trim(), out int parsedSamplerate))
                {
                    samplerate = parsedSamplerate;
                }
            }
            //await ShowDialogue(text, $"{bitrate}bit {samplerate}hz {channels}channels", text, text, this);
            await Save();

        }



        private async void applyButton_Click(object sender, RoutedEventArgs e)
        {
            resampleOption_3.IsChecked = true;
            bitrate = (int)bitRateBox.Value;
            samplerate = (int)sampleRateBox.Value;
            channels = (int)channelBox.Value;
            //await ShowDialogue("", $"{bitrate}bit {samplerate}hz {channels}channels", "text", "text", this);
            await Save();
        }



        private async Task Save()
        {
            SettingUtility.SetSettings("resampleFormat", $"{samplerate},{bitrate},{channels}");
            await ShowDialogue("提示", "重启应用程序来应用更改", "好的", null, this);
        }


        public class AudioQualityObject
        {
            public int sampleRate { get; set; }
            public int bitsPerSample { get; set; }
            public int channels { get; set; }
            public bool isMp3Ready { get; set; }
        }

    }
}
