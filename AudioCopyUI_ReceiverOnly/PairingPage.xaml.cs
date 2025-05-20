/*
*	 File: PairingPage.xaml.cs
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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using HttpClient = System.Net.Http.HttpClient;
using System.Timers;
using Timer = System.Timers.Timer;
using AudioCopyUI_ReceiverOnly;
using Windows.System;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml;
using static AudioCopyUI_ReceiverOnly.GlobalUtility;
using static AudioCopyUI_ReceiverOnly.Logger;
using static AudioCopyUI_ReceiverOnly.Localizer;
using System.Diagnostics;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AudioCopyUI_ReceiverOnly
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PairingPage : Page
    {
        private Timer _timer;

        public PairingPage()
        {
            this.InitializeComponent();
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {

            EmojiRepeater.ItemsSource = TypicalEmojis;
        }


        #region detect&pairing
        private async Task TryPair(List<string> devices)
        {
            try
            {
                foreach (var item in devices)
                {
                    HttpClient c = new HttpClient();
                    c.BaseAddress = new Uri($"http://{item}:{SettingUtility.GetOrAddSettings("defaultPort", "23456")}/");
                    c.Timeout = TimeSpan.FromSeconds(5);
                    var rsp = await c.GetAsync($"/RequirePair?udid=AudioCopy&name={Environment.MachineName}");
                    if (rsp.IsSuccessStatusCode)
                    {
                        var rspString = await rsp.Content.ReadAsStringAsync();
                        if (rspString.StartsWith("AudioCopy"))
                        {
                            if (await ShowDialogue(localize("Info"), string.Format(localize("PairRequired"), rspString.Substring(9)), localize("Accept"), localize("Cancel"), this))
                            {
                                rsp = await c.GetAsync($"/RequirePair?udid={SettingUtility.GetOrAddSettings("udid", AlgorithmServices.MakeRandString(128))}&name={Environment.MachineName}");
                                if (rsp.IsSuccessStatusCode)
                                {
                                    SettingUtility.SetSettings("sourceAddress", c.BaseAddress.ToString());
                                    await ShowDialogue(localize("Info"), localize("PairDone"), localize("Accept"), null, this);
                                }
                                else if (rsp.StatusCode == HttpStatusCode.BadRequest)
                                {
                                    if ((await rsp.Content.ReadAsStringAsync()).Trim() == "源不合法")
                                    {
                                        await ShowDialogue(localize("Info"), localize("BackendNotAllow"), localize("Accept"), null, this);
                                    }

                                }
                            }
                        }
                    }

                }
            }
            catch (TaskCanceledException)
            {
                await ShowDialogue(localize("Info"), localize("PairTimeOut"), localize("Accept"), null, this);
            }
            catch (Exception ex)
            {
                if (!await LogAndDialogue(ex, localize("Pair.Content"), localize("Accept"), localize("SearchOnline"), this))
                {
                    var question = $"{ex.GetType().Name} {ex.Message}";
                    bool result = await Windows.System.Launcher.LaunchUriAsync(new Uri($"https://www.bing.com/search?q={Uri.EscapeDataString(question)}"));
                }
            }
        }

        static string[] TypicalEmojis = new string[] {
  "😀", "😡", "😭", "😱", "😴", "🤮", "😎", "🥶",
  "👍", "👎", "✌️", "🖖", "👋", "👏", "🤘", "🙏",
  "👨‍⚕️", "👩‍🎓", "🧑‍🚀", "🧙‍♂️", "🧛‍♀️", "🧟‍♂️", "🧞‍♀️", "🧝‍♂️",
  "🐶", "🐱", "🐭", "🐸", "🐧", "🐵", "🦊", "🐷",
  "🍎", "🍉", "🍌", "🍇", "🍕", "🍔", "🍟", "🍩",
  "🚗", "🚕", "🚌", "🚑", "🚀", "✈️", "🚲", "🏍️",
  "📱", "💻", "🎧", "🎸", "🕶️", "💡", "🖊️", "📦",
  "🌞", "🌧️", "⛄", "🌈", "🌪️", "🔥", "🌊", "🌸"
        };



        static List<string> DecodeEmojisToString(IEnumerable<string> emojiSequence)
        {
            var bitList = new List<bool>();
            foreach (var emoji in emojiSequence)
            {
                int idx = Array.IndexOf(TypicalEmojis, emoji);
                for (int j = 5; j >= 0; j--)
                    bitList.Add((idx & (1 << j)) != 0);
            }

            int entryBits = 2 + 32;
            int count = bitList.Count / entryBits;
            var addresses = new List<string>(count);

            for (int i = 0; i < count; i++)
            {
                int offset = i * entryBits;

                byte[] bytes = new byte[4];
                for (int b = 0; b < 4; b++)
                {
                    int v = 0;
                    for (int k = 0; k < 8; k++)
                    {
                        if (bitList[offset + 2 + b * 8 + k])
                            v |= 1 << (7 - k);
                    }
                    bytes[b] = (byte)v;
                }

                addresses.Add(new IPAddress(bytes).ToString());
            }

            return addresses;
        }

        static bool IsLocalNetwork(string ipAddress)
        {
            return ipAddress.StartsWith("192.168.") || ipAddress.StartsWith("10.") ||
                   (ipAddress.StartsWith("172.") && int.TryParse(ipAddress.Split('.')[1], out int secondOctet) && secondOctet >= 16 && secondOctet <= 31);
        }
        #endregion


        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
        }


        List<string> inputed = new List<string>(), address = new List<string>();


        private void EmojiButtonClicked(object sender, RoutedEventArgs e)
        {
            selectedText.Text += (e.OriginalSource as Button).Content as string;
            inputed.Add((e.OriginalSource as Button).Content as string);

        }



        private void BackspaceButtonClick(object sender, RoutedEventArgs e)
        {
            if (inputed.Count <= 0) return;
            inputed.RemoveAt(inputed.Count - 1);
            selectedText.Text = string.Concat(inputed);
        }

        private async void selectedText_TextChanged(object sender, TextChangedEventArgs e)
        {
            var devices = DecodeEmojisToString(inputed);
            if (devices.Count > 0)
            {
                addressListBox.Text = devices.Aggregate("", (a, b) => a + b + ",");
                if (devices.Aggregate(true, (a, b) => a && IsLocalNetwork(b)))
                {
                    await TryPair(devices);
                }
            }
            else
            {
                addressListBox.Text = "";
            }

        }

        private void portBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(portBox.Text, out var val))
            {
                SettingUtility.SetSettings("defaultPort", val.ToString());
            }
            else
            {
                SettingUtility.SetSettings("defaultPort", "23456");
            }
        }





        private async void IpSubmitButtonClick(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(addressListBox.Text.Trim()))
            {
                if (IPAddress.TryParse(addressListBox.Text.Trim(), out _))
                {
                    await TryPair(new List<string> { addressListBox.Text.Trim() });
                }
                else
                {
                    await ShowDialogue(localize("Info"), localize("PairAddressNotCorrect"), localize("Accept"), null, this);
                }
            }
        }

    }
}
