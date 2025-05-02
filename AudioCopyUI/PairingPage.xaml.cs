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
using Microsoft.UI.Xaml.Navigation;
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
using Microsoft.UI.Dispatching;
using System.Text.Json;


// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AudioCopyUI
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
            PairTimer();
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await Task.Delay(1); //避免卡顿
            EmojiRepeater.ItemsSource = TypicalEmojis;
            await Task.Delay(10);
            address = GetLocalNetworkAddresses();            
            await Task.Delay(10);
            connectionEmojiBox.Text = GenerateConnectionEmoji(address).Aggregate("", (a, b) => a + b);
            await Task.Delay(10);
            ipAddressBox.Text = address.Aggregate("", (a, b) => $"{a}{Environment.NewLine}{b}");
            await Task.Delay(10);
        }


        #region detect&pairing
        private async Task TryPair(List<string> devices)
        {
            try
            {
                foreach (var item in devices)
                {
                    HttpClient c = new();
                    c.BaseAddress = new($"http://{item}:{SettingUtility.GetOrAddSettings("defaultPort", "23456")}/");
                    c.Timeout = TimeSpan.FromSeconds(5);
                    var rsp = await c.GetAsync($"/RequirePair?udid=AudioCopy&name={Environment.MachineName}");
                    if (rsp.IsSuccessStatusCode)
                    {
                        var rspString = new StreamReader(rsp.Content.ReadAsStream()).ReadToEnd();
                        if (rspString.StartsWith("AudioCopy"))
                        {
                            if (await ShowDialogue("info", $"要和{rspString.Substring(9)}配对吗？", "ok", "no", this))
                            {
                                rsp = await c.GetAsync($"/RequirePair?udid={SettingUtility.GetOrAddSettings("udid", AlgorithmServices.MakeRandString(128))}&name={Environment.MachineName}");
                                if (rsp.IsSuccessStatusCode)
                                {
                                    SettingUtility.SetSettings("sourceAddress", c.BaseAddress.ToString());
                                    await ShowDialogue("提示", "链接成功", "好的", null, this);
                                }
                                else if (rsp.StatusCode == HttpStatusCode.BadRequest)
                                {
                                    if (new StreamReader(rsp.Content.ReadAsStream()).ReadToEnd().Trim() == "源不合法")
                                    {
                                        await ShowDialogue("提示", "你的后端配置不允许此操作，可在设置修改。", "好的", null, this);
                                    }

                                }
                            }
                        }
                    }
                    
                }
            }
            catch (TaskCanceledException)
            {
                await ShowDialogue("提示", $"连接超时，请检查地址是否正确或者更换地址。", "好的", null, this);
            }
            catch (Exception ex)
            {
                if (!await ShowDialogue("提示", $"发生了{ex.GetType().Name}错误：{ex.Message}", "好的", "搜索解决方案", this))
                {
                    var question = $"{ex.GetType().Name} {ex.Message}";
                    bool result = await Windows.System.Launcher.LaunchUriAsync(new Uri($"https://www.bing.com/search?q={Uri.EscapeDataString(question)}"));
                }
            }
        }

        static string[] TypicalEmojis = [
  "😀", "😡", "😭", "😱", "😴", "🤮", "😎", "🥶",
  "👍", "👎", "✌️", "🖖", "👋", "👏", "🤘", "🙏",
  "👨‍⚕️", "👩‍🎓", "🧑‍🚀", "🧙‍♂️", "🧛‍♀️", "🧟‍♂️", "🧞‍♀️", "🧝‍♂️",
  "🐶", "🐱", "🐭", "🐸", "🐧", "🐵", "🦊", "🐷",
  "🍎", "🍉", "🍌", "🍇", "🍕", "🍔", "🍟", "🍩",
  "🚗", "🚕", "🚌", "🚑", "🚀", "✈️", "🚲", "🏍️",
  "📱", "💻", "🎧", "🎸", "🕶️", "💡", "🖊️", "📦",
  "🌞", "🌧️", "⛄", "🌈", "🌪️", "🔥", "🌊", "🌸"
];

        static List<string> GenerateConnectionEmoji(List<string> addresses, bool force = false)
        {
            var emojis = new List<string>();
            var bitList = new List<bool>();

            foreach (string addr in addresses)
            {
                var nums = addr.Split('.');
                int type;
                if (nums[0] == "192") type = 0; 
                else if (nums[0] == "10") type = 1; 
                else if (nums[0] == "172") type = 2; 
                else continue;

                bitList.Add((type & 2) != 0);
                bitList.Add((type & 1) != 0);

                byte[] ipBytes = IPAddress.Parse(addr).GetAddressBytes();
                foreach (byte b in ipBytes)
                {
                    for (int i = 7; i >= 0; i--)
                        bitList.Add((b & (1 << i)) != 0);
                }
            }

            int pad = (6 - (bitList.Count % 6)) % 6;
            for (int i = 0; i < pad; i++)
                bitList.Add(false);

            for (int i = 0; i < bitList.Count; i += 6)
            {
                int idx = 0;
                for (int j = 0; j < 6; j++)
                {
                    if (bitList[i + j])
                        idx |= 1 << (5 - j);
                }
                emojis.Add(TypicalEmojis[idx]);
            }

            return emojis;
        }

        static bool IsLocalNetwork(string ipAddress)
        {
            return ipAddress.StartsWith("192.168.") || ipAddress.StartsWith("10.") ||
                   (ipAddress.StartsWith("172.") && int.TryParse(ipAddress.Split('.')[1], out int secondOctet) && secondOctet >= 16 && secondOctet <= 31);
        }

        private static List<string> GetLocalNetworkAddresses()
        {
            List<string> address = new();


            var interfaces = NetworkInterface.GetAllNetworkInterfaces();

            foreach (var networkInterface in interfaces)
            {
                if (networkInterface.OperationalStatus == OperationalStatus.Up &&
                    networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    !networkInterface.Description.Contains("Vmware", StringComparison.OrdinalIgnoreCase) &&
                    !networkInterface.Description.Contains("VirtualBox", StringComparison.OrdinalIgnoreCase))
                {
                    var ipProperties = networkInterface.GetIPProperties();
                    foreach (var ipAddress in ipProperties.UnicastAddresses)
                    {
                        if (bool.Parse(SettingUtility.GetOrAddSettings("ShowAllAdapter", "False")))
                        {
                            address.Add(ipAddress.Address.ToString());

                        }
                        else if (ipAddress.Address.AddressFamily == AddressFamily.InterNetwork &&
                            IsLocalNetwork(ipAddress.Address.ToString()))
                        {
                            address.Add(ipAddress.Address.ToString());
                        }
                    }
                }
            }

            if (bool.Parse(SettingUtility.GetOrAddSettings("ShowAllAdapter", "False"))) return address;

            List<string> localIP = [.. address.Where(item => item.Trim().StartsWith("192.168."))];
            return address.TakeWhile((a) => !a.StartsWith("192")).Aggregate(localIP as IEnumerable<string>, (a, b) => a.Append(b)).ToList();
        }

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
        #endregion


        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
        }


        List<string> inputed = new(), address = new();


        private void EmojiButtonClicked(object sender, RoutedEventArgs e)
        {
            selectedText.Text += (e.OriginalSource as Button).Content as string;
            inputed.Add((e.OriginalSource as Button).Content as string);

        }


        private void PairTimer()
        {
            _timer = new Timer(3000);
            _timer.Elapsed += (s, e) =>
            {
                this.DispatcherQueue.TryEnqueue(
                    DispatcherQueuePriority.Normal,
                    () => _ = Pair()
                );
            };
            _timer.Start();
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

        private async void PairButton_Click(object sender, RoutedEventArgs e)
        {
            this.DispatcherQueue.TryEnqueue(
                    DispatcherQueuePriority.Normal,
                    () => Page_Loaded(new(),new())
                );
            await Pair();
        }

        private async Task Pair()
        {
            HttpClient httpClient = new();
            httpClient.BaseAddress = new($"http://127.0.0.1:{SettingUtility.GetOrAddSettings("defaultPort", "23456")}/");
            httpClient.Timeout = TimeSpan.FromSeconds(5);
            try
            {
                var response = await httpClient.GetAsync($"api/token/listPairing?hostToken={SettingUtility.HostToken}");
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync(default);
                    var pairingList = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(jsonString);

                    if (pairingList != null && pairingList.Count > 0)
                    {
                        foreach (var item in pairingList)
                        {
                            bool userConfirmed = await ShowDialogue("配对请求", $"检测到来自{item.Value}的配对请求，是否添加？", "确定", "取消", this);
                            if (userConfirmed)
                            {
                                var addResponse = httpClient.PostAsync($"api/token/add?token={item.Key}&hostToken={SettingUtility.HostToken}", null).GetAwaiter().GetResult();
                                if (addResponse.IsSuccessStatusCode)
                                {
                                    try
                                    {
                                        httpClient.DeleteAsync($"api/token/removePairing?token={item.Key}&hostToken={SettingUtility.HostToken}").GetAwaiter().GetResult();
                                    }
                                    catch (Exception)
                                    {
                                        await ShowDialogue("成功", "配对已添加成功，但是无法将其从待配对列表中删除，可以通过重启客户端来解决这个问题", "知道了", null, this);
                                        return;
                                    }
                                    Dictionary<string, string> kvp;
                                    try
                                    {
                                        kvp = JsonSerializer.Deserialize<Dictionary<string, string>>(SettingUtility.GetSetting("deviceMapping")) ?? new();

                                    }
                                    catch (Exception)
                                    {
                                        kvp = new();
                                    }
                                    kvp.TryAdd(item.Key, item.Value);

                                    SettingUtility.SetSettings("deviceMapping", JsonSerializer.Serialize(kvp));

                                    await ShowDialogue("成功", "配对已添加成功！", "好的", null, this);
                                }
                                else if(new StreamReader(addResponse.Content.ReadAsStream()).ReadToEnd() == "已存在") //不用管他
                                {
                                    try
                                    {
                                        httpClient.DeleteAsync($"api/token/removePairing?token={item.Key}&hostToken={SettingUtility.HostToken}").GetAwaiter().GetResult();
                                    }
                                    catch (Exception) { }
                                    await ShowDialogue("成功", "配对已添加成功！", "好的", null, this);

                                }
                                else
                                {
                                    try
                                    {
                                        httpClient.DeleteAsync($"api/token/removePairing?token={item.Key}&hostToken={SettingUtility.HostToken}").GetAwaiter().GetResult();
                                    }
                                    catch (Exception) { }
                                    await ShowDialogue("错误", "添加配对失败，请重试。", "好的", null, this);
                                }
                            }
                        }

                    }
                }
            }
            catch (TaskCanceledException)
            {

            }
            catch (Exception ex)
            {
                if (!await ShowDialogue("提示", $"发生了{ex.GetType().Name}错误：{ex.Message}", "好的", "搜索解决方案", this))
                {
                    var question = $"{ex.GetType().Name} {ex.Message}";
                    bool result = await Windows.System.Launcher.LaunchUriAsync(new Uri($"https://www.bing.com/search?q={Uri.EscapeDataString(question)}"));
                }
            }
        }

        

        private async void IpSubmitButtonClick(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(addressListBox.Text.Trim()))
            {
                if (IPAddress.TryParse(addressListBox.Text.Trim(), out _))
                {
                    await TryPair([addressListBox.Text.Trim()]);
                }
                else
                {
                    await ShowDialogue("提示", "地址无效", "好的", null, this);
                }
            }
        }

    }
}
