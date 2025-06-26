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
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.System;
using static System.Net.Mime.MediaTypeNames;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Tab;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AudioCopyUI.SettingViews
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class AudioQuality : Page
    {

        int bitrate, samplerate, channels;
        private bool loaded = false;

        public double rawBuffer { get { return double.TryParse(SettingUtility.GetSetting("rawBufferSize"), out var result) ? result : 4096; } set { SettingUtility.SetSettings("rawBufferSize", value.ToString()); _ = Save(); } }

        public AudioQuality()
        {
            this.InitializeComponent();
            //_ = AudioCloneHelper.Boot();
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
            foreach (var item in audioQualities)
            {
                MenuFlyoutItem i = new MenuFlyoutItem
                {
                    Text = item.ToString()
                };
                i.Click += MenuFlyoutItem_Click;
                OptionsFlyout.Items.Add(i);
            }

            try
            {
                rawBufferSize.Value = double.TryParse(SettingUtility.GetSetting("rawBufferSize"), out var result) ? result : 4096;                
                loaded = true;
                rawBufferSize.Value = double.TryParse(SettingUtility.GetSetting("rawBufferSize"), out result) ? result : 4096;

            }
            catch (Exception)
            {
                //defaultAudioQualityBlock.Text = localize("/Setting/AudioQuality_CurrentUnavailable");
            }

            
        }



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
            SettingUtility.SetSettings("resampleType", id);
        }

        private async void MenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            var text = (e.OriginalSource as MenuFlyoutItem).Text;
            audioQualityDropdown.Content = text;
            if (AudioQualityObject.TryParse(text, null, out var body))
            {
                bitrate = body.bitsPerSample;
                samplerate = body.sampleRate;
                channels = body.channels;

            }
            await Save();

        }



        private async void applyButton_Click(object sender, RoutedEventArgs e)
        {
            resampleOption_3.IsChecked = true;
            bitrate = (int)bitRateBox.Value;
            samplerate = (int)sampleRateBox.Value;
            channels = (int)channelBox.Value;
            await Save();
        }

        private async void rawBufferSize_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            SettingUtility.SetSettings("rawBufferSize", ((int)rawBufferSize.Value).ToString());
        }

        private async Task Save()
        {
            if (await ShowDialogue(localize("Info"), localize("/Setting/AudioQuality_RebootRequired"), localize("Accept"), localize("Cancel"), this))
            {
                await AudioCloneHelper.Kill();
                SettingUtility.SetSettings("resampleFormat", $"{samplerate},{bitrate},{channels}");
            }
            else
            {
                this.Frame.Navigate(typeof(SettingViews.AudioQuality));
            }

            //await ShowDialogue("", $"{bitrate}bit {samplerate}hz {channels}channels {rawBufferSize.Value} bytes buf   ", "text", "text", this);

        }


        public class AudioQualityObject : IParsable<AudioQualityObject>
        {
            public int sampleRate { get; set; }
            public int bitsPerSample { get; set; }
            public int channels { get; set; }
            public bool isMp3Ready { get; set; }

            

            public static AudioQualityObject Parse(string s, IFormatProvider? provider) => ParseAudioFormat(s);

            public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out AudioQualityObject result)
            {
                try
                {
                    result = ParseAudioFormat(s);
                    return true;
                }
                catch
                {
                    result = new AudioQualityObject { bitsPerSample = 0, channels = 0, sampleRate = 0 };
                    return false;
                }
            }

            static AudioQualityObject ParseAudioFormat(string input)
            {
                var match = Regex.Match(
                    input,
                    @"(\d+)\s*[^\d]+\s*(\d+)\s*[^\d]+\s*(\d+)",
                    RegexOptions.IgnoreCase
                );

                if (!match.Success || match.Groups.Count < 4)
                {
                    throw new ArgumentException("无效的音频格式字符串");
                }

                return new AudioQualityObject
                {
                    channels = int.Parse(match.Groups[1].Value),
                    bitsPerSample = int.Parse(match.Groups[2].Value),
                    sampleRate = int.Parse(match.Groups[3].Value)
                };
            }

            public override string ToString()
            {
                return string.Format(localize("/Setting/AudioQuality_Foramt"), channels, bitsPerSample, sampleRate);
            }
            
        }
        List<AudioQualityObject> audioQualities = new List<AudioQualityObject>
        {
    new AudioQualityObject { channels = 2, bitsPerSample = 16, sampleRate = 44100 ,isMp3Ready = true},
    new AudioQualityObject { channels = 2, bitsPerSample = 16, sampleRate = 48000 ,isMp3Ready = true},
    new AudioQualityObject { channels = 2, bitsPerSample = 16, sampleRate = 96000 },
    new AudioQualityObject { channels = 2, bitsPerSample = 16, sampleRate = 192000 },
    new AudioQualityObject { channels = 2, bitsPerSample = 24, sampleRate = 44100 },
    new AudioQualityObject { channels = 2, bitsPerSample = 24, sampleRate = 48000 },
    new AudioQualityObject { channels = 2, bitsPerSample = 24, sampleRate = 96000 },
    new AudioQualityObject { channels = 2, bitsPerSample = 24, sampleRate = 192000 },
    new AudioQualityObject { channels = 1, bitsPerSample = 16, sampleRate = 44100 ,isMp3Ready = true},
    new AudioQualityObject { channels = 1, bitsPerSample = 16, sampleRate = 48000 ,isMp3Ready = true},
    new AudioQualityObject { channels = 1, bitsPerSample = 24, sampleRate = 44100 },
    new AudioQualityObject { channels = 1, bitsPerSample = 24, sampleRate = 48000 }
};

    }
}
