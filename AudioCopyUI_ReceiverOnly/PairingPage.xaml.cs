using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using static AudioCopyUI_ReceiverOnly.GlobalUtility;

namespace AudioCopyUI_ReceiverOnly
{
    public sealed partial class PairingPage : Page
    {
        public PairingPage()
        {
            this.InitializeComponent();
            EmojiListView.ItemsSource = TypicalEmojis;
        }

        private const string StringTable = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890"; //have to do this because of too many functions are unavailable in .net framework(universal windows use it)
        public static string MakeRandString(int length) => string.Concat(Enumerable.Repeat(StringTable, length / StringTable.Length + 5)).OrderBy(x => Guid.NewGuid()).Take(length).Select(x => (char)x).Aggregate("", (x, y) => x + y);


        List<string> inputed = new List<string>();
        int addressCount = 0;

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            selectedText.Text += (e.OriginalSource as Button).Content as string;
            inputed.Add((e.OriginalSource as Button).Content as string);
        }

        private async Task TryPair(List<string> devices)
        {
            try
            {
                foreach (var item in devices)
                {
                    HttpClient c = new HttpClient();
                    c.DefaultRequestHeaders.ConnectionClose = true;
                    c.BaseAddress = new Uri($"http://{item}:{SettingUtility.GetOrAddSettings("defaultPort", "23456")}/");
                    c.Timeout = TimeSpan.FromSeconds(5);
                    var rsp = await c.GetAsync($"/RequirePair?udid=AudioCopy&name={Environment.MachineName}");
                    if (rsp.IsSuccessStatusCode)
                    {
                        var rspString = await rsp.Content.ReadAsStringAsync();
                        if (rspString.StartsWith("AudioCopy"))
                        {
                            if (await ShowDialogue("info", $"要和{rspString.Substring(9)}配对吗？", "ok", "no", this))
                            {
                                rsp = await c.GetAsync($"/RequirePair?udid={SettingUtility.GetOrAddSettings("udid", MakeRandString(128))}&name={Environment.MachineName}");
                                if (rsp.IsSuccessStatusCode)
                                {
                                    await ShowDialogue(item, "paired", item, item, this);
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
                    var question = $"c# HttpClient报错{ex.GetType().Name} {ex.Message}";
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

        static List<string> GenerateConnectionEmoji()
        {
            var emojis = new List<string>();
            var bitList = new List<bool>();

            var addresses = GetLocalNetworkAddresses();
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

        private static List<string> GetLocalNetworkAddresses()
        {
            List<string> address = new List<string>();
            bool IsLocalNetwork(string ipAddress)
            {
                return ipAddress.StartsWith("192.168.") || ipAddress.StartsWith("10.") ||
                       (ipAddress.StartsWith("172.") && int.TryParse(ipAddress.Split('.')[1], out int secondOctet) && secondOctet >= 16 && secondOctet <= 31);
            }

            var interfaces = NetworkInterface.GetAllNetworkInterfaces();

            foreach (var networkInterface in interfaces)
            {
                if (networkInterface.OperationalStatus == OperationalStatus.Up &&
                    networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    var ipProperties = networkInterface.GetIPProperties();
                    foreach (var ipAddress in ipProperties.UnicastAddresses)
                    {
                        if (ipAddress.Address.AddressFamily == AddressFamily.InterNetwork &&
                            IsLocalNetwork(ipAddress.Address.ToString()))
                        {
                            address.Add(ipAddress.Address.ToString());
                        }
                    }
                }
            }
            return address;
        }

        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            var str = DecodeEmojisToString(inputed);
            TryPair(str);
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            var emojis = GenerateConnectionEmoji();
            selectedText.Text = emojis.Aggregate((a, b) => a + b);
            addressCount = Array.IndexOf(TypicalEmojis, emojis[0]);
            inputed = emojis.ToList();
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

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            if (inputed.Count <= 0) return;
            inputed.RemoveAt(inputed.Count - 1);
            selectedText.Text = string.Concat(inputed);
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
            HttpClient httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri($"http://127.0.0.1:{SettingUtility.GetOrAddSettings("defaultPort", "23456")}/");
            httpClient.Timeout = TimeSpan.FromSeconds(5);
            try
            {
                // 请求列表
                var response = await httpClient.GetAsync("api/token/listPairing?hostToken=abcd");
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    var pairingList = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(jsonString);

                    if (pairingList != null && pairingList.Count > 0)
                    {
                        // 在 UI 线程显示提示框
                        foreach (var item in pairingList)
                        {
                            bool userConfirmed = await ShowDialogue("配对请求", $"检测到来自{item.Value}的配对请求，是否添加？", "确定", "取消", this);
                            if (userConfirmed)
                            {
                                // 用户确认后发送添加请求
                                var addResponse = httpClient.PostAsync($"api/token/add?token={item.Key}&hostToken=abcd", null).GetAwaiter().GetResult();
                                if (addResponse.IsSuccessStatusCode)
                                {
                                    await ShowDialogue("成功", "配对已添加成功！", "好的", null, this);
                                }
                                else
                                {
                                    await ShowDialogue("错误", "添加配对失败，请重试。", "好的", null, this);
                                }
                            }
                        }

                    }
                }
            }
            catch (TaskCanceledException)
            {
                // 任务取消时退出循环
                //break;
            }
            catch (Exception ex)
            {
                await ShowDialogue("错误", $"监听时发生错误：{ex.Message}", "好的", null, this);

            }
        }

        private async void Button_Click_4(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(addressListBox.Text.Trim()))
            {
                if (IPAddress.TryParse(addressListBox.Text.Trim(), out _))
                {
                    var l = new List<string>
                    {
                        addressListBox.Text.Trim()
                    };
                    TryPair(l);
                }
                else
                {
                    await ShowDialogue("提示", "地址无效", "好的", null, this);
                }
            }
        }

        private void selectedText_TextChanged(object sender, TextChangedEventArgs e)
        {
            var devices = DecodeEmojisToString(inputed);
            if (devices.Count > 0)
            {
                addressListBox.Text = devices.Aggregate("", (a, b) => a + b + ",");
                try
                {
                    TryPair(devices);


                }
                catch (TaskCanceledException)
                {

                }
            }
            else
            {
                addressListBox.Text = "";
            }
        }
    }
}
