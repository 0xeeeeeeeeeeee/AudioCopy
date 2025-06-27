/*
*	 File: PairingPageV2.xaml.cs
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
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Policy;
using System.Threading.Tasks;
using System.Timers;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AudioCopyUI
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PairingPageV2 : Page
    {
        private double PairButtonWidth;
        private Timer _buttonClickTimer;

        int AdvancedPanelCount = 0;

        public ObservableCollection<object> FoundClients { get; set; }

        List<string> DiscoveredClientsUDID = new();

        public PairingPageV2()
        {
            InitializeComponent();

            FoundClients = new ObservableCollection<object>();
            ItemsSrc.ItemsSource = FoundClients;
            DiscoveredClientsUDID.Add(SettingUtility.GetOrAddSettings("udid", AlgorithmServices.MakeRandString(128))); //配对自己没有意义

            InitializeTimer();
        }

        private void InitializeTimer()
        {
            if (AudioCopyUI_MiddleWare.BackendHelper.DiscoveredClients is null) return;
            _buttonClickTimer = new Timer(1000);
            _buttonClickTimer.Elapsed += OnTimerElapsed;
            _buttonClickTimer.AutoReset = true;
            _buttonClickTimer.Start();
        }

        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (!Program.AppRunning) return;
            DispatcherQueue.TryEnqueue(async () =>
            {
                foreach (var item in AudioCopyUI_MiddleWare.BackendHelper.DiscoveredClients)
                {
                    try
                    {
                        if (!DiscoveredClientsUDID.Contains(item.Value.udid))
                        {
                            if (AudioCopyUI_MiddleWare.BackendHelper.DiscoveredClientsAddress is not null && AudioCopyUI_MiddleWare.BackendHelper.DiscoveredClientsAddress.ContainsKey(item.Key))
                            {
                                FoundClients.Add(new Devices { Description = $"{item.Value.DeviceModel} ({AudioCopyUI_MiddleWare.BackendHelper.DiscoveredClientsAddress?[item.Key].First()})", Name = item.Value.Name, UDID = "DeviceID:" + item.Key, deviceType = item.Value.GetDeviceTypeIcon() });
                            }
                            else
                            {
                                try
                                {
                                    var rsp = new HttpClient().GetAsync($"http://{item.Value.Name}:23456/api/device/GetIPAddress");
                                    var ips = (await rsp.Result.Content.ReadAsStringAsync()).Split(',');
                                    string best = null;
                                    long lowestLatency = long.MaxValue;

                                    foreach (var ip in ips)
                                    {
                                        try
                                        {
                                            using (Ping ping = new Ping())
                                            {
                                                var reply = await ping.SendPingAsync(ip, 800);
                                                if (reply.Status == IPStatus.Success && reply.RoundtripTime < lowestLatency)
                                                {
                                                    lowestLatency = reply.RoundtripTime;
                                                    best = ip;
                                                }
                                            }
                                        }
                                        catch (Exception) { }
                                    }


                                    FoundClients.Add(new Devices { Description = $"{item.Value.DeviceModel} ({best})", Name = item.Value.Name, UDID = "DeviceID:" + item.Key, deviceType = item.Value.GetDeviceTypeIcon() });
                                    AudioCopyUI_MiddleWare.BackendHelper.DiscoveredClientsAddress.AddOrUpdate(item.Key, Enumerable.ToList([best]), (b, l) => l.Append(b).ToList());

                                }
                                catch (Exception ex)
                                {
                                    Log(ex, $"find client {item.Value.Name}", this);
                                    continue;
                                }
                            }
                            DiscoveredClientsUDID.Add(item.Value.udid);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log(ex, "add clients", this);
                    }
                }

                if (AdvancedPanelCount > 20) FacedWithProblem.Content = localize("FacedWithProblem1");
                else AdvancedPanelCount++;
            });
        }



        private async void ItemPairingButton_Click(object sender, RoutedEventArgs e)
        {
            var button = e.OriginalSource as Button;
            string ID = "";
            if (button != null)
            {
                ID = (ToolTipService.GetToolTip(button) as string).Split(':')[1];

            }

            var ips = AudioCopyUI_MiddleWare.BackendHelper.DiscoveredClientsAddress[ID];
            var info = AudioCopyUI_MiddleWare.BackendHelper.DiscoveredClients[ID];

            await TryPairing(ips, info.Name);
        }


        async Task TryPairing(List<string> ips, string name)
        {
            var udid = SettingUtility.GetOrAddSettings("udid", AlgorithmServices.MakeRandString(128));
            HttpClient c = new();
            c.Timeout = TimeSpan.FromSeconds(10);

            foreach (var item in ips)
            {
                var addr = $"{item}:23456";

                await TryPairing(addr, name);
            }
        }

        async Task TryPairing(string addr, string name)
        {
            var udid = SettingUtility.GetOrAddSettings("udid", AlgorithmServices.MakeRandString(128));
            HttpClient c = new();
            c.Timeout = TimeSpan.FromSeconds(10);

            try
            {
                var rsp = await c.GetAsync($"http://{addr}/api/token/RequestPairID?udid={udid}&name={Environment.MachineName}&version=2.2");

                if (rsp.IsSuccessStatusCode)
                {
                    var selected = await ShowPairingDialogue(name, (await rsp.Content.ReadAsStringAsync()).Split(',', StringSplitOptions.RemoveEmptyEntries), localize("Accept"), localize("Cancel"));

                    if (selected is null) return;

                    rsp = await c.GetAsync($"http://{addr}/api/token/ValidatePairID?udid={udid}&name={Environment.MachineName}&selected={Uri.EscapeDataString(selected ?? "null")}");

                    if (rsp.StatusCode != HttpStatusCode.Created) goto err;

                    rsp = await c.GetAsync($"http://{addr}/api/token/TryAuth?token={udid}");

                    if (await rsp.Content.ReadAsStringAsync() == "OK")
                    {
                        SettingUtility.SetSettings("sourceAddress", $"http://{addr}");
                        SettingUtility.SetSettings("PairedDevicePort", new Uri($"http://{addr}").Port.ToString());
                        rsp = await c.GetAsync($"http://{addr}/api/device/GetClientName?token={udid}");
                        SettingUtility.SetSettings("PairedDeviceName", await rsp.Content.ReadAsStringAsync());

                        await ShowDialogue(localize("Info"), localize("PairDone"), localize("Accept"), null, this);
                        return;
                    }
                    else goto err;
                }
                else
                {
                    goto err;
                }

            err:
                switch (await rsp.Content.ReadAsStringAsync())
                {
                    case "\"Magic, time or version mismatch\"":
                        await ShowDialogue(localize("Error"), localize("ClientModified"), localize("Accept"), null, this);
                        break;
                    case "\"Selected is invalid.\"":
                        await ShowDialogue(localize("Error"), localize("InputInvalid"), localize("Accept"), null, this);
                        break;
                    case "\"Pairing is canceled or not started yet.\"":
                    case "\"You have been banned\"":
                        await ShowDialogue(localize("Error"), localize("ClientDenied"), localize("Accept"), null, this);
                        break;
                    case "\"Hasn't discovered you yet.\"":
                        rsp = await Backend.DiscoverHelper.ManualDiscover(addr);
                        if (!rsp.IsSuccessStatusCode) goto err;
                        else await TryPairing(addr, name);
                        break;
                    default:
                        await ShowDialogue(localize("Error"), localize("PairFailed", $"Code:{rsp.StatusCode} Message:{await rsp.Content.ReadAsStringAsync()}"), localize("Accept"), null, this);
                        break;
                }
                return;
            }
            catch (Exception ex)
            {
                if (!await LogAndDialogue(ex, localize("Pair.Content"), localize("Accept"), localize("SearchOnline"), this))
                {
                    var question = $"{ex.GetType().Name} {ex.Message}";
                    bool result = await Windows.System.Launcher.LaunchUriAsync(new Uri($"https://www.bing.com/search?q={Uri.EscapeDataString(question)}"));
                }

                return;
            }

        }

        private async void IPSubmitButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(addressListBox.Text.Trim()))
            {
                if (IPAddress.TryParse(addressListBox.Text, out _))
                {
                    await TryPairing([addressListBox.Text.Trim()], $"\"{addressListBox.Text.Trim()}\"");
                }
                else if (Uri.TryCreate("http://" + addressListBox.Text.Trim(), default, out _))
                {
                    await TryPairing(addressListBox.Text.Trim(), $"\"{addressListBox.Text.Trim()}\"");

                }
                else
                {
                    await ShowDialogue(localize("Info"), localize("PairAddressNotCorrect"), localize("Accept"), null, this);
                }

            }
        }


        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (bool.Parse(SettingUtility.GetOrAddSettings("NoDiscover", "False")) || int.Parse(SettingUtility.GetOrAddSettings("backendPort", "23456")) != 23456)
            {
                NoDiscoverBar.Title = localize("Info");
                NoDiscoverBar.Content = new TextBlock
                {
                    Text = localize("DiscoverDisabled"),
                    Margin = new Thickness(0, 0, 0, 12)
                };
                NoDiscoverBar.IsOpen = true;

                return;
            }

            foreach (var item in AudioCopyUI_MiddleWare.BackendHelper.DiscoveredClients)
            {
                try
                {
                    if (!DiscoveredClientsUDID.Contains(item.Value.udid))
                    {
                        FoundClients.Add(new Devices { Description = $"{item.Value.DeviceModel} ({AudioCopyUI_MiddleWare.BackendHelper.DiscoveredClientsAddress[item.Key].First()})", Name = item.Value.Name, UDID = "DeviceID:" + item.Key });
                        DiscoveredClientsUDID.Add(item.Value.udid);
                    }
                }
                catch (Exception ex)
                {
                    Log(ex, "add clients", this);
                }


            }
        }


        private async Task<string?> ShowPairingDialogue(string name, string[] lists, string primaryButtonText, string secondaryButtonText)
        {
            List<string> selected = new();

            StackPanel buttons1 = new StackPanel { Orientation = Orientation.Horizontal };
            StackPanel buttons2 = new StackPanel { Orientation = Orientation.Horizontal };
            StackPanel buttons3 = new StackPanel { Orientation = Orientation.Horizontal };
            StackPanel buttons4 = new StackPanel { Orientation = Orientation.Horizontal };

            TextBox inputBox = new TextBox
            {
                Name = "InputBox",
                Margin = new Thickness(8, 0, 0, 0),
                FontSize = 36,
                IsReadOnly = true
            };

            int i = 0, j = 1;
            foreach (var item in lists)
            {
                var btn = new Button
                {
                    Content = item,
                    FontSize = 36,
                    Margin = new Thickness(8, 0, 8, 4)
                };
                btn.Click += (s, e) =>
                {
                    selected.Add((e.OriginalSource as Button).Content as string);
                    if (selected.Count > 0) inputBox.Text = inputBox.Text = selected.Aggregate((a, b) => $"{a} {b}");
                    else inputBox.Text = "";
                };
                if (j == 1) buttons1.Children.Add(btn);
                else if (j == 2) buttons2.Children.Add(btn);
                else if (j == 3) buttons3.Children.Add(btn);
                else buttons4.Children.Add(btn);

                if (i == lists.Length / 4 - 1)
                {
                    j++;
                    i = 0;
                }
                else
                {
                    i++;

                }

            }

            FontIcon bkspIcon = new FontIcon
            {
                Glyph = "\uE750"
            };

            Button bksp = new Button
            {
                Content = bkspIcon,
                Margin = new Thickness(8, 0, 0, 0)
            };

            bksp.Click += (s, e) =>
            {
                if (selected.Count == 0) return;
                selected.RemoveAt(selected.Count - 1);
                if (selected.Count > 0) inputBox.Text = selected.Aggregate((a, b) => $"{a} {b}");
                else inputBox.Text = "";
            };

            var dialog = new ContentDialog
            {

                Title = localize("PairConfirm", name),
                Content =
                new ScrollView
                {
                    Content = new StackPanel
                    {
                        Children =
                        {
                            buttons1,
                            buttons2,
                            buttons3,
                            buttons4,
                            new StackPanel
                            {
                                Children =
                                {
                                    inputBox,
                                    bksp
                                },
                                Orientation = Orientation.Horizontal,
                                Margin = new Thickness(0, 8, 0, 0)
                            }
                        },
                        Orientation = Orientation.Vertical
                    }
                },
                PrimaryButtonText = primaryButtonText,
                SecondaryButtonText = secondaryButtonText,
                XamlRoot = MainWindow.xamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                return selected.Aggregate((a, b) => $"{a},{b}");
            }
            return null;
        }

        public class Devices
        {
            public required string Name { get; set; }
            public required string Description { get; set; }
            public string UDID { get; set; }
            public string deviceType { get; set; } = "\uE977";
        }

        private void FacedWithProblem_Click(object sender, RoutedEventArgs e)
        {
            ipAddressBox.Text = localize("AddressListBoxInfo") + Environment.NewLine + Environment.NewLine + localize("YourIP") + PairingPage.GetLocalNetworkAddresses().Aggregate("", (a, b) => $"{a}{Environment.NewLine}{b}");

        }

        private void V1PairingPage_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(PairingPage));
        }
    }
}
