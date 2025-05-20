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
using System.Threading;
using System.Threading.Tasks;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.UI.Xaml.Controls;
using static AudioCopyUI_ReceiverOnly.GlobalUtility;
using static AudioCopyUI_ReceiverOnly.Logger;
using static AudioCopyUI_ReceiverOnly.Localizer;
using System.Diagnostics;
using Windows.System;
using Windows.UI.Xaml.Media.Imaging;
using Newtonsoft.Json; //System.Text.Json似乎有点问题，反序列化MediaInfo全返回null
using Windows.Foundation.Metadata;


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
        CancellationTokenSource cts = new CancellationTokenSource();
        bool playing = false;
        string deviceName = "";
        string ClientToken = SettingUtility.GetOrAddSettings("udid", AlgorithmServices.MakeRandString(128));


        public ReceivePage()
        {
            this.InitializeComponent();
            if (!string.IsNullOrEmpty(SettingUtility.GetOrAddSettings("sourceAddress", "")))
                c.BaseAddress = new Uri(SettingUtility.GetOrAddSettings("sourceAddress", ""));
            if (bool.Parse(SettingUtility.GetOrAddSettings("DisableShowHostSMTCInfo", "False"))) MedidInfoPanel.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

        }

        async Task<bool> TryConnect()
        {
            try
            {
                if (string.IsNullOrEmpty(SettingUtility.GetOrAddSettings("sourceAddress", "")))
                {
                    if (await ShowDialogue(localize("Info"), localize("NotPaired"), localize("Accept"), localize("Cancel"), this))
                    {
                        this.Frame.Navigate(typeof(PairingPage));
                        return false;
                    }
                    else return false;
                }

                var rsp = await c.GetAsync("/index");
                if (rsp.StatusCode != System.Net.HttpStatusCode.Unauthorized) //no token, should be 401
                {
                    if (await ShowDialogue(localize("Info"), localize("TryReconnect"), localize("Accept"), localize("Cancel"), this))
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
                            deviceName = rspString.Substring(9);
                            playButton.Content = String.Format(localize("PlayString"), deviceName);
                        }
                    }
                }
                catch (Exception) { }
                new Thread(async () =>
                {
                    string perious = "";
                    //if (!ApiInformation.IsMethodPresent("System.Diagnostics.Stopwatch", "StartNew")) throw new SillyWACKVersionLieException();
                    //if (!ApiInformation.IsMethodPresent("Windows.UI.Xaml.Dispatcher", "TryRunAsync")) throw new SillyWACKVersionLieException();
                    //if (!ApiInformation.IsTypePresent("System.UriBuilder")) throw new SillyWACKVersionLieException();

                    Stopwatch timer = Stopwatch.StartNew();
                    Random r = new Random();
                    while (true)
                    {
                        try
                        {
                            rsp = await c.GetAsync($"/api/device/GetSMTCInfo?token={ClientToken}");
                            if (rsp.IsSuccessStatusCode)
                            {
                                var rspStr = await rsp.Content.ReadAsStringAsync();
                                var infoBody = JsonConvert.DeserializeObject<MediaInfo>(rspStr);
                                if (infoBody != null)
                                {
                                    if (infoBody.Artist == "AudioCopy @ 0xeeeeeeeeeeee") break;
                                    await this.Dispatcher.TryRunAsync(
                                            Windows.UI.Core.CoreDispatcherPriority.Normal,
                                            async () =>
                                            {
                                                try
                                                {


                                                    if (perious != infoBody.Title || timer.Elapsed.TotalSeconds > 60)
                                                    {
                                                        if (timer.Elapsed.TotalSeconds > 60) timer.Restart();
                                                        MediaInfo_FromDevice.Text = string.Format(localize("MediaInfo_FromDevice"), deviceName);
                                                        MediaInfo_Title.Text = infoBody.Title;
                                                        MediaInfo_Album.Text = infoBody.AlbumArtist;
                                                        MediaInfo_Artist.Text = infoBody.Artist;
                                                        UriBuilder b = new UriBuilder(c.BaseAddress);
                                                        b.Path = $"/api/device/GetAlbumPhoto";
                                                        b.Query = $"?token={ClientToken}&randomThing={r.Next()}";
                                                        MediaInfo_AlbumArt.Source = null;
                                                        MediaInfo_AlbumArt.Source = new BitmapImage(b.Uri);
                                                        perious = infoBody.Title;

                                                        if (playing)
                                                        {
                                                            var updater = smtc.DisplayUpdater;
                                                            updater.Type = MediaPlaybackType.Music;
                                                            updater.MusicProperties.Title = string.Format(localize("AudioFrom"), deviceName);
                                                            updater.MusicProperties.AlbumArtist = $"{infoBody.Title} - {infoBody.AlbumArtist}";
                                                            updater.Thumbnail = Windows.Storage.Streams.RandomAccessStreamReference.CreateFromUri(b.Uri);
                                                            updater.Update();
                                                        }
                                                    }
                                                }
                                                catch (Exception ex1)
                                                {
                                                    //Log(ex1, "Get SMTC info", this);
                                                }
                                                finally
                                                {
                                                }


                                            });
                                    await Task.Delay(1500);

                                }
                            }
                        }
                        catch (Exception ex1)
                        {
                            Log(ex1, "Get SMTC info", this);
                        }

                    




                    }
                }).Start();
                return true;
            }
            catch (Exception ex)
            {
                Log(ex);
                return false;
            }


        }

        public class MediaInfo
        {
            public string Title { get; set; }
            public string Artist { get; set; }
            public string AlbumArtist { get; set; }
            public string AlbumTitle { get; set; }
            public string PlaybackType { get; set; }
            public string AlbumArtBase64 { get; set; }
        }

        private async void Button_Click(object sender, object e)
        {

            if (playing)
            {
                playing = false;
                mediaPlayer.Pause();
                mediaPlayer.Dispose();
                playButton.Content = String.Format(localize("PlayString"), deviceName);
                return;
            }
            if (!await TryConnect()) return;
            playing = true;
            mediaPlayer = new MediaPlayer();
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
            Log($"Playing at address:{source.ToString()}");
            playButton.Content = localize("StopPlay");



            try
            {
                smtc = mediaPlayer.SystemMediaTransportControls;
                smtc.IsEnabled = true;
                smtc.IsPauseEnabled = false;
                smtc.ButtonPressed += Smtc_ButtonPressed;

                mediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
                PlayerElement.SetMediaPlayer(mediaPlayer);
                mediaPlayer.Source = MediaSource.CreateFromUri(source);
                mediaPlayer.Play();

                var updater = smtc.DisplayUpdater;
                updater.Type = MediaPlaybackType.Music;
                updater.MusicProperties.Title = string.Format(localize("AudioFrom"), deviceName);
                updater.Update();

                smtc.PropertyChanged += (s, ex) =>
                {
                    Smtc_ButtonPressed( new object(), new object());
                };



            }
            catch (Exception ex)
            {
                await LogAndDialogue(ex, "播放流", null, null, this);
            }
        }

        private void MediaPlayer_MediaEnded(MediaPlayer sender, object args)
        {
            sender.Play();
        }


        private void Smtc_ButtonPressed(object sender, object args)
        {
            var updater = smtc.DisplayUpdater;
            updater.Type = MediaPlaybackType.Music;
            updater.MusicProperties.Title = string.Format(localize("AudioFrom"), deviceName);
            updater.Update();
        }

        private void radioButtons_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var id = int.Parse((e.OriginalSource as RadioButton).Name.Split('_')[1]);
            SettingUtility.SetSettings("playingType", id.ToString());
        }

        private async void Page_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
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
                        var body = JsonConvert.DeserializeObject<AudioQualityObject>(await rsp.Content.ReadAsStringAsync());
                        radioButton_1.IsEnabled = body.isMp3Ready;
                        if (!body.isMp3Ready && !bool.Parse(SettingUtility.GetOrAddSettings("AlwaysAllowMP3", "False")))
                        {
                            radioButton_1.Content = localize("MP3UnavailableString");
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
