/*
*	 File: TransferPage.xaml.cs
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
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;

namespace AudioCopyUI
{
    public sealed partial class TransferPage : Page
    {
        private ObservableCollection<ClientInfo> Clients { get; set; } = new ObservableCollection<ClientInfo>();
        private ObservableCollection<string> BindedClientList { get; set; } = new ObservableCollection<string>();
        private Timer timer;
        private HttpClient c = new HttpClient();
        string token = SettingUtility.GetOrAddSettings("udid", AlgorithmServices.MakeRandString(128));
        List<string> existDevices = new();
        int lastCount = 0;

        public TransferPage()
        {
            this.InitializeComponent();
            ClientListView.ItemsSource = Clients;
            BindedClientListView.ItemsSource = BindedClientList; // 绑定数据源

            var sourceAddress = $"http://127.0.0.1:{SettingUtility.GetOrAddSettings("backendPort", "23456")}";
            c.BaseAddress = new Uri(sourceAddress);
            c.Timeout = TimeSpan.FromSeconds(5);

            //deviceMenu.Content = localize("ChooseDevices");
            //Localized_PairedDevices.Text = localize("PairedDevices");

            _ = UpdateClientsAsync();
            _ = LoadClientListAsync();
            //_ = LoadAudioDevicesAsync();

            StartTimer();
        }

        private async void OnOptionSelected(MenuFlyout menuFlyout, string selectedOption)
        {
            var id = Array.IndexOf(existDevices.ToArray(), selectedOption);
            //deviceMenu.Content = selectedOption;
            SettingUtility.SetSettings("AudioDeviceName", selectedOption);
            var rsp = await c.PutAsync(
                $"/api/audio/SetCaptureOptions?deviceId=-1{(SettingUtility.GetOrAddSettings("resampleType", "1") != "1" ? "&format=" + Uri.EscapeDataString(SettingUtility.GetOrAddSettings("resampleFormat", "48000,16,2")) : "" )}&token={token}"
                ,null);
            if (!rsp.IsSuccessStatusCode)
            {
                await ShowDialogue(localize("Error"), localize("FailedSetAudioDevices") + $"{await rsp.Content.ReadAsStringAsync()}", localize("Accept"), null, this);
            }
        }

        private void AddOptionToDropDown(MenuFlyout menuFlyout, string option)
        {            
            var menuItem = new MenuFlyoutItem { Text = option };
            menuItem.Click += (s, e) => OnOptionSelected(menuFlyout, option);
            menuFlyout.Items.Add(menuItem);
        }


        //private async Task LoadAudioDevicesAsync()
        //{
        //    try
        //    {
        //        var response = await c.GetStringAsync($"/api/device/GetAudioDevices?token={token}");
        //        var devices = JsonSerializer.Deserialize<string[]>(response);

        //        if (devices != null)
        //        {
        //            if (lastCount != devices.Length)
        //            {
        //                deviceMenuFlyout.Items.Clear();
        //                existDevices = new();
        //                lastCount = devices.Length;
        //            }
        //            foreach (var item in devices)
        //            {
        //                if (!existDevices.Contains(item))
        //                {
        //                    AddOptionToDropDown(deviceMenuFlyout, item);
        //                    existDevices.Add(item);
        //                }
                        
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Log($"LoadAudioDevicesAsync error: {ex.Message}", "warn");
        //    }
        //}



        private async Task LoadClientListAsync()
        {
            try
            {
                Dictionary<string, string> kvp;
                try
                {
                    kvp = JsonSerializer.Deserialize<Dictionary<string, string>>(SettingUtility.GetSetting("deviceMapping")) ?? new();

                }
                catch (Exception)
                {
                    kvp = new();
                }
                using (HttpClient client = new HttpClient())
                {
                    var response = await c.GetStringAsync($"/api/token/list?hostToken={SettingUtility.HostToken}");
                    var clientList = JsonSerializer.Deserialize<string[]>(response);
                    foreach (var clientName in clientList)
                    {
                        string clientDisplayName = "";
                        if (kvp.ContainsKey(clientName))
                        {
                            clientDisplayName = ($"{kvp[clientName]}                                     @{clientName}");
                        }
                        else
                        {
                            clientDisplayName = ($"unknown device                                        @{clientName}");

                        }
                        if (!BindedClientList.Contains(clientDisplayName)) BindedClientList.Add(clientDisplayName);
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"LoadClientListAsync error: {ex.Message}", "warn");

            }
        }

        private async void DeleteClient_Click(object sender, object? e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is string clientName)
            {
                if (!await ShowDialogue(localize("Info"), $"要删除{clientName.Split('@')[0].Replace(' ', '\0').Trim()}吗？", localize("Cancel"), "删除", this))
                {
                    BindedClientList.Remove(clientName); 
                    _ = c.DeleteAsync($"api/token/remove?token={clientName.Split('@').Last()}&hostToken={SettingUtility.HostToken}");
                    try
                    {
                        Dictionary<string, string> kvp;
                        try
                        {
                            kvp = JsonSerializer.Deserialize<Dictionary<string, string>>(SettingUtility.GetSetting("deviceMapping")) ?? new();

                        }
                        catch (Exception)
                        {
                            kvp = new();
                        }
                        kvp.Remove(clientName);
                        SettingUtility.SetSettings("deviceMapping", JsonSerializer.Serialize(kvp));
                    }
                    catch (Exception) { }
                    await ShowDialogue(localize("Info"), localize("Deleted"), localize("Accept"), null, this);
                }

            }
        }

        private void StartTimer()
        {
            timer = new Timer(3000);
            timer.Elapsed += (s, e) =>
            {
                this.DispatcherQueue.TryEnqueue(
                    DispatcherQueuePriority.Normal,
                    () =>
                    {
                        _ = UpdateClientsAsync();
                        _ = LoadClientListAsync();
                        //_ = LoadAudioDevicesAsync();
                        try
                        {
                            //backendStatusBox.Text = c.GetAsync("/index").GetAwaiter().GetResult().StatusCode == System.Net.HttpStatusCode.Unauthorized ? "后端状态：正常" : "后端状态：异常";
                        }
                        catch (Exception)
                        {
                            //backendStatusBox.Text = "后端状态：未知";
                            _ = ShowDialogue(localize("Info"), localize("BackendAbnormal"), localize("Accept"), null, this);
                        }
                    }
                );
            };
            timer.Start();
        }

        private async Task UpdateClientsAsync()
        {
            try
            {
                var rsp = await c.GetAsync($"/api/device/GetListeningClient?token={token}");
                var response = new StreamReader(rsp.Content.ReadAsStream()).ReadToEnd();
                var strs = JsonSerializer.Deserialize<string[]>(response);

                if (strs != null)
                {
                    var newClients = new ObservableCollection<ClientInfo>();

                    foreach (var str in strs)
                    {
                        if (str == "none@none") continue; //防止空值导致异常
                        //Log(response);
                        newClients.Add(new ClientInfo { IP = str.Split('@')[0], Name = str.Split('@')[1] });
                    }

                    SyncClientList(newClients);
                }
            }
            catch (Exception ex)
            {
                Log($"UpdateClientsAsync error: {ex.Message}","warn");
            }
        }

        private void SyncClientList(ObservableCollection<ClientInfo> newClients)
        {
            for (int i = Clients.Count - 1; i >= 0; i--)
            {
                if (!newClients.Any(c => c.IP == Clients[i].IP && c.Name == Clients[i].Name))
                {
                    Clients.RemoveAt(i);
                }
            }

            
            foreach (var newClient in newClients)
            {
                if (!Clients.Any(c => c.IP == newClient.IP && c.Name == newClient.Name))
                {
                    Clients.Add(newClient);
                }
            }
        }

        private class ClientItem
        {
            public string Item1 { get; set; } // IP
            public string Item2 { get; set; } // Name
        }

        private class ClientInfo
        {
            public string IP { get; set; }
            public string Name { get; set; }
        }

        private async void RebootBackend_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await Program.BootBackend();
        }

        private void Button_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Clients.Add(new ClientInfo { IP = "123.456.789.123", Name = "123123123" });
            Clients.Add(new ClientInfo { IP = "123.456.789.123", Name = "123123123" });
            Clients.Add(new ClientInfo { IP = "123.456.789.123", Name = "123123123" });
        }
        
    }
}

