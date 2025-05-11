/*
*	 File: ReceivePage.xaml.cs
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
using AudioCopyUI_ReceiverOnly;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.UI.Xaml.Controls;
using static AudioCopyUI_ReceiverOnly.GlobalUtility;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AudioCopyUI_ReceiverOnly
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ReceivePage : Page
    {
        private SystemMediaTransportControls smtc;
        HttpClient c = new HttpClient();
        MediaPlayer mediaPlayer = new MediaPlayer();
        string deviceName = "未知设备";

        public ReceivePage()
        {
            this.InitializeComponent();
            if (!string.IsNullOrEmpty(SettingUtility.GetOrAddSettings("sourceAddress", "")))
                c.BaseAddress = new Uri(SettingUtility.GetOrAddSettings("sourceAddress", ""));

            var mediaPlayer = PlayerElement.MediaPlayer;



        }

       

        async Task<bool> TryConnect()
        {
            try
            {
                if (string.IsNullOrEmpty(SettingUtility.GetOrAddSettings("sourceAddress", "")))
                {
                    if (await ShowDialogue("提示", "你尚未配对，要去配对吗？", "好的", "取消", this))
                    {
                        this.Frame.Navigate(typeof(PairingPage));
                        return false;
                    }
                    else return false;
                }

                var rsp = await c.GetAsync("/index");
                if (rsp.StatusCode != System.Net.HttpStatusCode.Unauthorized) //no token, should be 401
                {
                    if (await ShowDialogue("提示", "连接失败，要尝试重新配对吗？", "好的", "取消", this))
                    {
                        this.Frame.Navigate(typeof(PairingPage));
                        return false;
                    }
                    else return false;
                }

                try
                {
                    rsp = await c.GetAsync($"/RequirePair?udid=AudioCopy&name={Environment.MachineName}");
                    if (rsp.IsSuccessStatusCode)
                    {
                        var rspString = await rsp.Content.ReadAsStringAsync();    
                        if (rspString.StartsWith("AudioCopy"))
                        {
                            var name = rspString.Substring(9);
                            playButton.Content = $"播放来自{name}的音频";
                            deviceName = name;
                        }
                    }
                }
                catch (Exception) { }

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }


        }

        private async void Button_Click(object sender, object e)
        {

            if (!await TryConnect()) return;
            mediaPlayer.Pause();
            mediaPlayer.Source = null;
            string format;
            switch (SettingUtility.GetOrAddSettings("playingType", "2"))
            {
                case "1":
                    format = "mp3";
                        break;
                case "2":
                    format = "wav";
                    break;
                case "3":
                    format = "flac";
                    break;
                default:
                    format = "wav";
                    break;
            }



            var token = SettingUtility.GetOrAddSettings("udid", AlgorithmServices.MakeRandString(128));
            Uri source = new Uri(c.BaseAddress, $"/api/audio/{format}?token={token}&clientName={Environment.MachineName}");
            Logger.Log($"Playing at address:{source.ToString()}");


            try
            {
                mediaPlayer = new MediaPlayer();
                mediaPlayer.MediaEnded += MediaPlayer_MediaEnded;

                PlayerElement.SetMediaPlayer(mediaPlayer);

                mediaPlayer.Source = MediaSource.CreateFromUri(source);
                mediaPlayer.Play();

                await Task.Delay(1500); // 等待更新

                if (smtc == null)
                {
                    smtc = mediaPlayer.SystemMediaTransportControls;
                    smtc.IsEnabled = true;
                    smtc.IsPauseEnabled = false;
                    smtc.ButtonPressed += Smtc_ButtonPressed;

                    var updater = smtc.DisplayUpdater;
                    updater.Type = MediaPlaybackType.Music;
                    updater.MusicProperties.Title = $"Audio from {deviceName}";
                    updater.Update();
                }
            }
            catch (Exception ex)
            {
                 await ShowDialogue("错误", $"播放流时发生了错误：{ex.Message}", "好的", null,this);

            }
        }

        private void MediaPlayer_MediaEnded(MediaPlayer sender, object args)
        {
            sender.Play();
        }


        private void Smtc_ButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
        {
            var updater = smtc.DisplayUpdater;
            updater.Type = MediaPlaybackType.Music;
            updater.MusicProperties.Title = $"Audio from {deviceName}";
            updater.Update();
        }

        private void radioButtons_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var id = int.Parse((e.OriginalSource as RadioButton).Name.Split('_')[1]);
            SettingUtility.SetSettings("playingType", id.ToString());
        }

        private async void Page_Loaded(object sender, object e)
        {
            switch (SettingUtility.GetOrAddSettings("playingType", "2"))
            {
                case "1":
                    radioButton_1.IsChecked = true;
                    break;
                case "2":
                    radioButton_2.IsChecked = true;
                    break;
                case "3":
                    radioButton_3.IsChecked = true;
                    break;


            }
            if (await TryConnect())
            {
                try
                {
                    var token = SettingUtility.GetOrAddSettings("udid", AlgorithmServices.MakeRandString(128));
                    var rsp = await c.GetAsync($"api/audio/GetAudioFormat?token={token}");
                    if (rsp.IsSuccessStatusCode)
                    {
                        var body = JsonSerializer.Deserialize<AudioQualityObject>(await (rsp).Content.ReadAsStringAsync());
                        radioButton_1.IsEnabled = body.isMp3Ready;
                        if (!body.isMp3Ready && !bool.Parse(SettingUtility.GetOrAddSettings("AlwaysAllowMP3", "False")))
                        {
                            radioButton_1.Content += "(根据你的配置不可用)";
                            if (radioButton_1.IsChecked ?? true) radioButton_2.IsChecked = true;
                        }
                    }
                }
                catch (Exception) { }


            }
        }

        public class AudioQualityObject
        {
            public int sampleRate { get; set; }
            public int bitsPerSample { get; set; }
            public int channels { get; set; }
            public bool isMp3Ready { get; set; }
        }

        private void Button_Click_1(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {

        }
    }
}
