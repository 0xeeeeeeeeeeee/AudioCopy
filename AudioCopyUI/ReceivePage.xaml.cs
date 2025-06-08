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
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using NAudio.Wave;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media;
using Windows.Media.Control;
using Windows.Media.Core;
using Windows.Media.Playback;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AudioCopyUI
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ReceivePage : Page
    {
        private SystemMediaTransportControls smtc;
        HttpClient c = new();
        MediaPlayer mediaPlayer = new MediaPlayer();
        CancellationTokenSource cts = new();
        bool rawPlaying = false;
        string deviceName = "";
        private bool playing;
        private bool updated;
        string ClientToken = SettingUtility.GetOrAddSettings("udid", AlgorithmServices.MakeRandString(128));
        private bool SMTCRunning;

        public ReceivePage()
        {
            this.InitializeComponent();
            if (!string.IsNullOrEmpty(SettingUtility.GetOrAddSettings("sourceAddress", "")))
                c.BaseAddress = new(SettingUtility.GetOrAddSettings("sourceAddress",""));

            if (bool.Parse(SettingUtility.GetOrAddSettings("DisableShowHostSMTCInfo", "False"))) MedidInfoPanel.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;

            c.Timeout = TimeSpan.FromSeconds(45);
            if(bool.Parse(SettingUtility.GetOrAddSettings("NoShowNewBackend", "False")))
            {
                NewBackendInfoBar.Visibility = Visibility.Collapsed;
            }
            else
            {
                NewBackendBar.Text = localize("NewBackendBar");
            }
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
                HttpResponseMessage rsp;

                if (SettingUtility.OldBackend)
                {
                    rsp = await c.GetAsync("/Index");

                    if (rsp.StatusCode != HttpStatusCode.Unauthorized)
                    {
                        if (await ShowDialogue(localize("Info"), localize("TryReconnect"), localize("Accept"), localize("Cancel"), this))
                        {
                            this.Frame.Navigate(typeof(PairingPage));
                            return false;
                        }
                        else return false;
                    }
                }
                else
                {
                    rsp = await c.GetAsync("/Detect");

                    if (!rsp.IsSuccessStatusCode)
                    {
                        if (await ShowDialogue(localize("Info"), localize("TryReconnect"), localize("Accept"), localize("Cancel"), this))
                        {
                            this.Frame.Navigate(typeof(PairingPage));
                            return false;
                        }
                        else return false;
                    }
                }
                try
                {
                    rsp = await c.GetAsync($"/RequirePair?udid=AudioCopy&name={Environment.MachineName}");
                    if (rsp.IsSuccessStatusCode)
                    {
                        var rspString = new StreamReader(rsp.Content.ReadAsStream()).ReadToEnd();
                        if (rspString.StartsWith("AudioCopy"))
                        {
                            deviceName = rspString.Substring(9);
                            playButton.Content = String.Format(localize("PlayString"), deviceName);
                        }
                    }
                }catch (Exception) { }
                new Thread(async () =>
                {
                    if (SMTCRunning) return;
                    SMTCRunning = true;
                    string perious = "";
                    Stopwatch timer = Stopwatch.StartNew();
                    while (true)
                    {
                        if (bool.Parse(SettingUtility.GetOrAddSettings("DisableShowHostSMTCInfo", "False")))
                        {
                            if (SettingUtility.OldBackend) goto upload;
                            else return;
                        }
                        try
                        {
                            rsp = await c.GetAsync($"/api/device/GetSMTCInfo?token={ClientToken}");
                            if (rsp.IsSuccessStatusCode)
                            {
                                var infoBody = await rsp.Content.ReadFromJsonAsync<MediaInfo>();
                                if (infoBody is not null)
                                {
                                    if (infoBody.Artist == "AudioCopy @ 0xeeeeeeeeeeee") break;
                                    this.DispatcherQueue.TryEnqueue(
                                            DispatcherQueuePriority.Normal,
                                            () =>
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
                                                        b.Query = $"?token={ClientToken}&randomThing={Random.Shared.Next()}"; //确保图片被刷新
                                                        MediaInfo_AlbumArt.Source = null;
                                                        MediaInfo_AlbumArt.Source = new BitmapImage(b.Uri);
                                                        perious = infoBody.Title;

                                                        if (playing && !rawPlaying)
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
                                                    Log(ex1, "Get SMTC info", this);
                                                }

                                            });
                                }
                            }
                        }
                        catch (Exception ex1)
                        {
                            Log(ex1, "Get SMTC info", this);
                        }
                        if (!SettingUtility.OldBackend) continue;
                        await Task.Delay(1500);


                    upload:
                        try
                        {
                            var body = await Backend.DeviceController.GetCurrentMediaInfoAsync();
                            var bodyJson = JsonSerializer.Serialize(body);
                            rsp = await c.PostAsync($"/api/device/UploadSMTCInfo?hostToken={SettingUtility.HostToken}", new StringContent(bodyJson, System.Text.Encoding.UTF8, "application/json"));
                            if (!rsp.IsSuccessStatusCode) Log($"failed to update SMTC:{await rsp.Content.ReadAsStringAsync()}", "warn");
                        }
                        catch (Exception ex2)
                        {
                            Log(ex2, "Upload SMTC info", this);
                        }
                        finally
                        {
                            
                        }




                    }
                }).Start();
                return true;
            }
            catch(Exception ex)
            {
                Log(ex);
                return false;
            }
            
            
        }

        private async void Button_Click(object sender, object? e)
        {
            var cont = playButton.Content;
            playButton.IsEnabled = false;
            playButton.Content = new ProgressRing { IsActive = true };
            replayCount = 0;
            replayPromptShowed = false;
            var token = ClientToken;
            if (rawPlaying)
            {
                rawPlaying = false;
                cts.Cancel();
                Program.ExitApp(true);
                return;
            }
            else if (playing)
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
            var format = (SettingUtility.GetOrAddSettings("playingType", "2")) switch
            {
                "1" => "mp3",
                "2" => "wav",
                "3" => "flac",
                "4" => "raw",
                _ => "wav"
            };
            UriBuilder builder = new();
            
            Uri source;
            if (SettingUtility.OldBackend)
            {
                source = new(c.BaseAddress, $"/api/audio/{format}?token={token}&clientName={Environment.MachineName}");

            }
            else if(bool.Parse(SettingUtility.GetOrAddSettings("OverrideAudioCloneOptions", "False")))
            {
                source = new Uri($"http://127.0.0.1:{AudioCloneHelper.Port}/api/audio/{format}?token={SettingUtility.GetOrAddSettings("OverrideAudioCloneToken", "null")}&clientName={Environment.MachineName}");
            }
            else
            {
                var rsp = await c.GetAsync($"/api/device/BootAudioClone?token={token}");
                var addr = await rsp.Content.ReadAsStringAsync();
                var baseAddr = "http:" + c.BaseAddress.ToString().Split(':')[1] + ":" + string.Format(addr,format,Environment.MachineName); 
                source = new Uri(baseAddr);
            }
            
            Log($"Playing at address:{source.ToString()}");

            playButton.Content = localize("StopPlay");
            playButton.IsEnabled = true;

            if (format == "raw")
            {
                if (bool.Parse(SettingUtility.GetOrAddSettings("PromptRawPlayback", "True"))) await ShowRawPlaybackPrompt();
                rawPlaying = true;
                PlayRaw(source, cts.Token);
                return;

            }

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
                updater.MusicProperties.Artist = "AudioCopy @ 0xeeeeeeeeeeee";
                updater.Update();

                smtc.PropertyChanged += (s,e) => 
                {
                    Smtc_ButtonPressed(new(), new());
                };

                

            }
            catch (Exception ex)
            {
                await LogAndDialogue(ex, "播放流", null, null, this);
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
        int replayCount = 0;
        bool replayPromptShowed = false;

        private async void MediaPlayer_MediaEnded(MediaPlayer sender, object args)
        {
            replayCount++;
            if(replayCount > 30 && !replayPromptShowed)
            {
                replayPromptShowed = true;              
                this.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, async () =>
                {
                    await ShowDialogue(localize("Info"), localize("PlayingWinUIMediaPlayerElementBug"), localize("Accept"), null, this);
                    if (rawPlaying)
                    {
                        rawPlaying = false;
                        cts.Cancel();
                        Program.ExitApp(true);
                        return;
                    }
                    else if (playing)
                    {
                        playing = false;
                        mediaPlayer.Pause();
                        mediaPlayer.Dispose();
                        playButton.Content = String.Format(localize("PlayString"), deviceName);
                        return;
                    }
                });
                return;
            }
            sender.Play();
        }

        private async void PlayRaw(Uri source, CancellationToken token)
        {
            try
            {


                AudioQualityObject body;
                try
                {
                    var _token = SettingUtility.GetOrAddSettings("udid", AlgorithmServices.MakeRandString(128));
                    var rsp = await c.GetAsync($"api/audio/GetAudioFormat?token={_token}");
                    body = JsonSerializer.Deserialize<AudioQualityObject>(await rsp.Content.ReadAsStringAsync());
                }
                catch (Exception)
                {
                    throw new NotSupportedException("Cannot get host audio settings.");
                }

                var waveFormat = new WaveFormat(body.sampleRate, 16, body.channels);

                var bufferedProvider = new BufferedWaveProvider(waveFormat)
                {
                    BufferDuration = TimeSpan.FromSeconds(2),
                    DiscardOnBufferOverflow = true
                };

                Task producer = Task.Run(async () =>
                {
                    try
                    {
                        using (HttpClient rawClient = new HttpClient())
                        using (var stream = await rawClient.GetStreamAsync(source))
                        {
                            byte[] buffer = new byte[int.TryParse(SettingUtility.GetOrAddSettings("rawBufferSize", "4096"), out var result) ? result : 4096];
                            while (!token.IsCancellationRequested)
                            {
                                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                                if (bytesRead > 0)
                                {
                                    bufferedProvider.AddSamples(buffer, 0, bytesRead);
                                }
                                else
                                {
                                    await Task.Delay(50, token);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log(ex, "网络接收", this);
                    }
                }, token);

                Task consumer = Task.Run(async () =>
                {
                    try
                    {
                        using (var waveOut = new WaveOutEvent())
                        {
                            waveOut.Init(bufferedProvider);
                            waveOut.Play();

                            while (waveOut.PlaybackState == PlaybackState.Playing && !token.IsCancellationRequested)
                            {
                                await Task.Delay(50, token);
                            }

                            waveOut.Stop();
                        }
                    }
                    catch (Exception ex)
                    {
                        Log(ex, "播放", this);
                    }
                }, token);

                await Task.WhenAll(producer, consumer);
            }
            catch(Exception ex)
            {
                await LogAndDialogue(ex, "播放", localize("Accept"), null, this);
            }
        }

        private void Smtc_ButtonPressed(object sender, object args)
        {
            var updater = smtc.DisplayUpdater;
            updater.Type = MediaPlaybackType.Music;
            updater.MusicProperties.Title = string.Format(localize("AudioFrom"), deviceName);
			updater.Update();
        }

        private void radioButtons_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var id = int.Parse((e.OriginalSource as RadioButton).Name.Split('_')[1]);
            SettingUtility.SetSettings("playingType", id.ToString());
        }

        private async void Page_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(radioButton_4, localize("RawAudioDescribe"));

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
                case "4":
                    radioButton_4.IsChecked = true;
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
                        var body = JsonSerializer.Deserialize<AudioQualityObject>(new StreamReader((rsp).Content.ReadAsStream()).ReadToEnd());
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
        private async Task<bool> ShowRawPlaybackPrompt()
        {
            var tcs = new TaskCompletionSource<bool>();
            var dialog = new ContentDialog
            {
                Title = localize("Info"),
                Content = new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = localize("RawAudioInfo"), TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap },
                        new CheckBox { Name = "NoPrompt", Content = localize("RawAudioNoPrompt") }
                    }
                },
                PrimaryButtonText = localize("Accept"),
                CloseButtonText = localize("Cancel"),
                XamlRoot = this.XamlRoot
            };

            bool noLongerPrompt = false;
            var stackPanel = dialog.Content as StackPanel;
            var cb = stackPanel.Children[1] as CheckBox;
            cb.Checked += (s, e) => noLongerPrompt = true;
            cb.Unchecked += (s, e) => noLongerPrompt = false;

            var result = await dialog.ShowAsync();
            if (noLongerPrompt)
                SettingUtility.SetSettings("PromptRawPlayback", "False");
            return result == ContentDialogResult.Primary;
        }

        private async void EnableNewBackend_Click(object sender, RoutedEventArgs e)
        {
            await ShowDialogue(localize("Info"), localize("NewBackendPrompt"), localize("Accept"), null, this);
            SettingUtility.SetSettings("OldBackend", "False");
            var c = EnableNewAPI.Content;
            EnableNewAPI.Content = new ProgressRing { IsActive = true };          
            await Program.KillBackend();
            await Program.BootBackend();
            EnableNewAPI.Content = c;
            NewBackendInfoBar.Visibility = Visibility.Collapsed;
            SettingUtility.SetSettings("NoShowNewBackend", "True");
        }

        private void HideInfoBar_Click(object sender, RoutedEventArgs e)
        {
            SettingUtility.SetSettings("NoShowNewBackend", "True");
            NewBackendInfoBar.Visibility = Visibility.Collapsed;
        }

        public class AudioQualityObject
        {
            public int sampleRate { get; set; }
            public int bitsPerSample { get; set; }
            public int channels { get; set; }
            public bool isMp3Ready { get; set; }
        }

        public class BlockingStream : Stream
        {
            private readonly BlockingCollection<byte[]> _buffers = new BlockingCollection<byte[]>();
            private byte[] _currentBuffer;
            private int _position;



            public override void Write(byte[] buffer, int offset, int count)
            {
                var data = new byte[count];
                Array.Copy(buffer, offset, data, 0, count);
                _buffers.Add(data);
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (_currentBuffer == null || _position >= _currentBuffer.Length)
                {
                    if (!_buffers.TryTake(out _currentBuffer, Timeout.Infinite))
                        return 0; // 流结束
                    _position = 0;
                }

                int bytesToCopy = Math.Min(count, _currentBuffer.Length - _position);
                Array.Copy(_currentBuffer, _position, buffer, offset, bytesToCopy);
                _position += bytesToCopy;
                return bytesToCopy;
            }

            

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException();
            public override long Position { get; set; }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
        }
    }
}
