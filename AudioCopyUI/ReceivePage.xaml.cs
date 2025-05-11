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
using Microsoft.UI.Xaml.Controls;
using NAudio.Wave;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media;
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

        public ReceivePage()
        {
            this.InitializeComponent();
            if (!string.IsNullOrEmpty(SettingUtility.GetOrAddSettings("sourceAddress", "")))
                c.BaseAddress = new(SettingUtility.GetOrAddSettings("sourceAddress",""));

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
                        var rspString = new StreamReader(rsp.Content.ReadAsStream()).ReadToEnd();
                        if (rspString.StartsWith("AudioCopy"))
                        {
                            var name = rspString.Substring(9);
                            playButton.Content = $"播放来自{name}的音频";
                        }
                    }
                }catch (Exception) { }

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

            if (rawPlaying)
            {
                rawPlaying = false;
                cts.Cancel();
                Program.ExitApp(true);
                return;
            }
            if (!await TryConnect()) return;
            mediaPlayer.Pause();
            mediaPlayer.Source = null;
            var format = (SettingUtility.GetOrAddSettings("playingType", "2")) switch
            {
                "1" => "mp3",
                "2" => "wav",
                "3" => "flac",
                "4" => "raw",
                _ => "wav"
            };

            var token = SettingUtility.GetOrAddSettings("udid", AlgorithmServices.MakeRandString(128));
            Uri source = new(c.BaseAddress, $"/api/audio/{format}?token={token}&clientName={Environment.MachineName}");
            Log($"Playing at address:{source.ToString()}");
            if (format == "raw")
            {
                rawPlaying = true;
                playButton.Content = "停止播放";

                PlayRaw(source, cts.Token);

                return;

            }

            try
            {
                if (mediaPlayer == null)
                {
                    mediaPlayer = new MediaPlayer();
                    mediaPlayer.MediaEnded += MediaPlayer_MediaEnded;

                    PlayerElement.SetMediaPlayer(mediaPlayer);
                }

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
                    updater.MusicProperties.Title = "Audio from AudioCopy";
                    updater.Update();
                }
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

        private async void PlayRaw(Uri source, CancellationToken token)
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

            var waveFormat = new WaveFormat(body.sampleRate, 16 , body.channels);

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
                        byte[] buffer = new byte[int.TryParse(SettingUtility.GetOrAddSettings("rawBufferSize","4096"), out var result) ? result : 4096]; 
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
                    Log(ex, "网络接收",this);
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
                            await Task.Delay(50,token);
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

        private void Smtc_ButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
        {
            var updater = smtc.DisplayUpdater;
            updater.Type = MediaPlaybackType.Music;
            updater.MusicProperties.Title = "Audio from AudioCopy";
            updater.Update();
        }

        private void radioButtons_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var id = int.Parse((e.OriginalSource as RadioButton).Name.Split('_')[1]);
            SettingUtility.SetSettings("playingType", id.ToString());
        }

        private async void Page_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
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
