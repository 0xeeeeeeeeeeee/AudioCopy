/*
*	 File: Program.cs
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


#if !DISABLE_XAML_GENERATED_MAIN
#define DISABLE_XAML_GENERATED_MAIN
#endif

using AudioCopyUI_MiddleWare;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Management;
using Windows.Media.Control;
using Windows.Storage;

namespace AudioCopyUI
{
    public static class Program
    {
        public static int BackendPort = -1;
        public static string BackendVersionCode = "unknown";
        public static double BackendAPIVersion => BackendHelper.BackendAPIVersion;
        public static bool CloseApplication { get; private set; }
        public static CancellationTokenSource ApplicationCloseTokenSource { get; private set; }
        public static bool AppRunning { get; private set; }
        public static bool ExitHandled { get; internal set; }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int MessageBox(IntPtr hWnd, String text, String caption, uint type);

        [global::System.STAThreadAttribute]
        static async Task Main(string[] args)
        {
            try
            {
                if (!Directory.Exists(Path.Combine(LocalStateFolder, "logs"))) Directory.CreateDirectory(Path.Combine(LocalStateFolder, "logs"));

                ApplicationCloseTokenSource = new();

                Console.SetOut(new InterceptingTextWriter(Console.Out, "stdout"));
                Console.SetError(new InterceptingTextWriter(Console.Out, "stderr"));

                if (args.Length > 0)
                {
                    var tag = args[0].Split(':')[1];
                    if (tag == "fromTray")
                    {
                        _LoggerInit_(SettingUtility.GetOrAddSettings("logPath", "null"), true);
                    }
                    else if(tag == "nowindow")
                    {
                        global::Microsoft.UI.Xaml.Application.Start((p) =>
                        {
                            var context = new global::Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(global::Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
                            global::System.Threading.SynchronizationContext.SetSynchronizationContext(context);
                            //new App();
                        });
                        return;
                    }
                    else
                    {
                        SettingUtility.SetSettings("logPath", "null");
                    }
                }


                if (!SettingUtility.Exists("RealtimeLogging")) SettingUtility.SetSettings("RealtimeLogging", false.ToString());
                if (string.IsNullOrEmpty(___LogPath___)) _LoggerInit_(Path.Combine(LocalStateFolder, "logs"));
                Log($"Bootup args:{string.Concat(args)}");
                ___PublicStackOn___ = true;


                if (File.Exists(Path.Combine(LocalStateFolder, "wait.txt")))
                {
                    _ = MessageBox(new IntPtr(0), $"点击确定来继续启动", localize("Info"), 0);
                }

                if (File.Exists(Path.Combine(LocalStateFolder, "overrideSetting.json"))) //用来救援（不用手动挂载注册表了）
                {
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string?>>(File.ReadAllText(Path.Combine(LocalStateFolder, "overrideSetting.json"))) ?? new();
                    foreach (var item in dict)
                    {
                        if (!ApplicationData.Current.LocalSettings.Values.ContainsKey(item.Key))
                        {
                            if (item.Value is not null)
                                ApplicationData.Current.LocalSettings.Values[item.Key] = item.Value;
                        }
                        else
                        {
                            if (item.Value is not null)
                                ApplicationData.Current.LocalSettings.Values[item.Key] = item.Value;
                            else
                                ApplicationData.Current.LocalSettings.Values.Remove(item.Key);
                        }
                        Log($"Override setting {item.Key} to {item.Value}...");
                    }
                    File.Move(Path.Combine(LocalStateFolder, "overrideSetting.json"), Path.Combine(LocalStateFolder, "overrideSetting.json.old"));
                }

                if (bool.Parse(SettingUtility.GetOrAddSettings("ResetEverything", "False")))
                {
                    Log("Resetting...");
                    await Program.KillBackend();
                    SettingViews.AdvancedSetting.ClearDirectory(LocalStateFolder);
                    SettingUtility.SetSettings("ResetEverything", "False");

                }

                appThread = new(BootGUI);
                appThread.Start();

                Thread backendThread = new(async () =>
                {
                    Log("Starting backend...");
                    await BootBackend(-1, true);
                });
                InitBackend();
                backendThread.Start();

                appThread.Join();
                Debug.WriteLine("appThread ended.");
                await Task.Delay(-1, ApplicationCloseTokenSource.Token);
            }
            catch (Exception ex)
            {
                Crash(ex);
            }
            finally
            {
                Log("App is closing...");
            }

        }

        #region misc

        public static void ExitApp(bool reboot = false)
        {
            __FlushLog__();
            Thread.Sleep(100);
            if (reboot)
            {
                var script =
$$"""
function changeLang
{
    [System.Console]::Title = "Please wait..."

    taskkill.exe /f /im "AudioCopyUI.exe" 2> $null 1>$null

    Start-Sleep -Seconds 1

    Start-Process "audiocopy:"

    exit 
}

Clear-Host;changeLang

""";
                var proc = new Process();
                proc.StartInfo.FileName = "powershell.exe";
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardInput = true;
                proc.StartInfo.CreateNoWindow = false;
                proc.Start();
                var procWriter = proc.StandardInput;
                if (procWriter != null)
                {
                    procWriter.AutoFlush = true;
                    procWriter.WriteLine(script);
                }
            }
            ApplicationCloseTokenSource.Cancel();
            Environment.Exit(0);
        }

        public static void RebootApp(int delay = 50) => ExitApp(true);

        internal static void Crash(Exception ex)
        {
            try
            {
                Log(ex, true);
                __FlushLog__();
                _ = KillBackend();
            }
            finally
            {



                string innerExceptionInfo = "None";
                if (ex.InnerException != null)
                {
                    innerExceptionInfo =
$"""
Type: {ex.InnerException.GetType().Name}                        
Message: {ex.InnerException.Message}
StackTrace:
{ex.InnerException.StackTrace}

""";
                }

                if (___LogPath___ == "") //通常没法获取数据目录代表着WinRT没被初始化
                {
                    _ = MessageBox(new IntPtr(0), "AudioCopy crashed.\r\nPlease send me the entire report according to the instructions in the report.", "Crashed", 0);
                    var miniLogPath = Path.Combine(Directory.CreateTempSubdirectory("audiocopy_").FullName, "crash.log");
                    File.WriteAllText(miniLogPath,
$"""
You receive this crash report because of we can't write log, probably means the WinRT Component is not ready.
Is the UWP runtime on your computer normal?

To feedback, please hold Windows-R and input "%userprofile%\AppData\Local\Packages\", then compress the folder "0xeeeeeeeeeeee.AudioCopy_f91nmrsqwpk6y", and email it to me at hexadecimal0x12e@icloud.com along with this report,
or write a issue at https://github.com/0xeeeeeeeeeeee/AudioCopy/issues and upload them. You also needed to provide the version of the AudioCopy you used.

This report is in {Path.GetDirectoryName(miniLogPath)}
---
Exception type: {ex.GetType().Name}
Message: {ex.Message}
StackTrace:
{ex.StackTrace}

From:{(ex.TargetSite is not null ? ex.TargetSite.ToString() : "unknown")}
InnerException:
{innerExceptionInfo}

Exception data:
{string.Join("\r\n", ex.Data.Cast<System.Collections.DictionaryEntry>().Select(k => $"{k.Key} : {k.Value}"))}

Environment:
OS version: {Environment.OSVersion}
CLR Version:{Environment.Version}
Command line: {Environment.CommandLine}
Current directory: {Environment.CurrentDirectory}
"""
                        );
                    Process.Start(new ProcessStartInfo { FileName = miniLogPath, UseShellExecute = true });
                    Environment.FailFast(ex.Message, ex);
                    Environment.Exit(1);
                }

                var logMessage =
$"""
{localize("CrashReportHeader", VersionString, Path.Combine(LocalStateFolder, "crashlog"))}
Exception type: {ex.GetType().Name}
Message: {ex.Message}
StackTrace:
{ex.StackTrace}

From:{(ex.TargetSite is not null ? ex.TargetSite.ToString() : "unknown")}
InnerException:
{innerExceptionInfo}

Exception data:
{string.Join("\r\n", ex.Data.Cast<System.Collections.DictionaryEntry>().Select(k => $"{k.Key} : {k.Value}"))}

Settings:
{ApplicationData.Current.LocalSettings.Values.Aggregate("", (s, k) => s += $"{k.Key} : {k.Value as string} \r\n")}

Environment:
OS version: {Environment.OSVersion}
CLR Version:{Environment.Version}
Command line: {Environment.CommandLine}
Current directory: {Environment.CurrentDirectory}

Latest log:
{(string.IsNullOrEmpty(___LogPath___) ? "Unavailable" : File.ReadAllText(___LogPath___))}

(report ended here)
""";
                Directory.CreateDirectory(Path.Combine(LocalStateFolder, "crashlog"));
                string logPath;
                try
                {
                    logPath = Path.Combine(LocalStateFolder, "crashlog\\", $"Crashlog-{DateTime.Now:yyyy-MM-dd-hh-mm-ss}.log");
                    File.WriteAllText(logPath, logMessage);
                }
                catch (Exception) //避免最坏的情况（整个UWP运行时都不可用）
                {
                    logPath = Path.Combine(Directory.CreateTempSubdirectory("audiocopy_").FullName, "crash.log");
                    File.WriteAllText(logPath, logMessage);
                }
                Thread.Sleep(100);
                Process.Start(new ProcessStartInfo { FileName = logPath, UseShellExecute = true });
                Environment.FailFast(ex.Message, ex);
                Environment.Exit(1);
            }
        }

        private static async Task<AudioCopyUI_MiddleWare.BackendHelper.MediaInfo?> GetCurrentMediaInfoAsync()
        {
            var sessions = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var currentSession = sessions.GetCurrentSession();
            if (currentSession == null) return null;

            var mediaProperties = await currentSession.TryGetMediaPropertiesAsync();
            var info = new AudioCopyUI_MiddleWare.BackendHelper.MediaInfo
            {
                Title = mediaProperties.Title,
                Artist = mediaProperties.Artist,
                AlbumArtist = mediaProperties.AlbumArtist,
                AlbumTitle = mediaProperties.AlbumTitle,
                PlaybackType = mediaProperties.PlaybackType.ToString() ?? "Music"
            };

            if (mediaProperties.Thumbnail != null)
            {
                using var stream = await mediaProperties.Thumbnail.OpenReadAsync();
                using var ms = new MemoryStream();
                await stream.AsStreamForRead().CopyToAsync(ms);
                info.AlbumArtBase64 = Convert.ToBase64String(ms.ToArray());
            }

            return info;
        }

        public static async Task ChangeLang(string target)
        {
            SettingUtility.SetSettings("Language", target == "default" ? Windows.Globalization.Language.CurrentInputMethodLanguageTag : target);
            Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = Localizer.Match(target);
            await Task.Delay(10);

            var script =
$$"""
function changeLang
{
    [System.Console]::Title = "Applying localization settings"

    $decoded = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String("{{Convert.ToBase64String(Encoding.UTF8.GetBytes(localize("/Setting/AdvancedSetting_ApplyLocate")))}}"))

    Write-Host -BackgroundColor Green -ForegroundColor White $decoded


    taskkill.exe /f /im "AudioCopyUI.exe" 2> $null 1>$null

    for ($i = 0; $i -lt 3; $i++) 
    {
        Start-Process "audiocopy:nowindow" 

        Start-Sleep -Seconds 2

        taskkill.exe /f /im "AudioCopyUI.exe" 2> $null 1>$null
    }

    Start-Process "audiocopy:" 
    exit
}

Clear-Host;changeLang

""";


            var proc = new Process();
            proc.StartInfo.FileName = "powershell.exe";
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardInput = true;
            proc.StartInfo.CreateNoWindow = false;
            proc.Start();
            var procWriter = proc.StandardInput;
            if (procWriter != null)
            {
                procWriter.AutoFlush = true;
                procWriter.WriteLine(script);
            }

        }

        public static string GetDeviceModel()
        {
            string? model = null;
            int type = -1;

            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystemProduct");

                foreach (var obj in searcher.Get())
                {
                    var motherboardModel = obj["Name"]?.ToString();
                    model = motherboardModel;
                    var manf = obj["Vendor"]?.ToString();
                    if(!model.ToLower().Contains(manf.ToLower())) model = $"{manf} {model}";
                    break;
                }
            }
            catch (Exception ex)
            {
                Log(ex, "Get device model", GetDeviceModel);
            }

            if (model == null)
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystemProduct");
                    foreach (var obj in searcher.Get())
                    {
                        var motherboardModel = obj["Name"]?.ToString();
                        if (!string.IsNullOrEmpty(motherboardModel))
                        {
                            model = motherboardModel;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log(ex, "Get motherboard model", GetDeviceModel);
                }

            }

            if (model == null)
            {
                model = "Unknown Model";
            }

            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_SystemEnclosure"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var chassisTypes = obj["ChassisTypes"] as ushort[];

                        if (chassisTypes != null)
                        {
                            foreach (var typ in chassisTypes)
                            {
                                type = typ;
                                break;
                            }
                        }
                        else //另外的方法
                        {
                            try
                            {
                                bool hasBattery = false;
                                using (var searcher1 = new ManagementObjectSearcher("SELECT * FROM Win32_Battery"))
                                {
                                    foreach (ManagementObject battery in searcher.Get())
                                    {
                                        var name = (battery["Name"] as string) ?? "none".ToLower();
                                        if (!name.Contains("ups") || !name.Contains("usb")) //防止使用了USB UPS的场景下误判
                                            hasBattery = true;
                                        break;
                                    }
                                }
                                type = hasBattery ? 8 : 3;

                            }
                            catch (Exception ex)
                            {
                                Log(ex, "Get device type by battery", GetDeviceModel);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log(ex, "Get device type", GetDeviceModel);
            }

            switch (type)
            {
                case 3:
                case 4:
                case 5:
                case 6:
                case 7:
                case 13:
                case 15:
                case 16:
                case 17:
                case 18:
                    BackendHelper.ThisDeviceTypeID = DeviceType.Desktop;
                    break;

                case 8:
                case 9:
                case 10:
                case 14:
                    BackendHelper.ThisDeviceTypeID = DeviceType.Laptop;
                    break;

                case 11:
                case 23:
                case 30:
                case 31:
                    BackendHelper.ThisDeviceTypeID = DeviceType.Tablet;
                    break;


                default:
                    BackendHelper.ThisDeviceTypeID = DeviceType.Unknown;
                    break;
            }

            var typeStr = type switch
            {
                3 => "Desktop",
                4 => "Low Profile Desktop",
                5 => "Pizza Box",
                6 => "Mini Tower",
                7 => "Tower",
                8 => "Portable",
                9 => "Laptop",
                10 => "Notebook",
                11 => "Hand Held",
                12 => "Docking Station",
                13 => "All in One",
                14 => "Sub Notebook",
                15 => "Space-Saving",
                16 => "Lunch Box",
                17 => "Main System Chassis",
                18 => "Expansion Chassis",
                21 => "Peripheral Chassis",
                23 => "Tablet",
                30 => "Tablet (Convertible)",
                31 => "Detachable",
                _ => "Unknown type"
            };

            if (model.ToLower().Contains(typeStr.ToLower())) return model;

            return model + " " + typeStr;
        }


        #endregion

        #region backend

        public static async Task KillBackend()
        {
            await StopNewBackend();
        }

        public static async Task BootBackend(int port = -1, bool STA = false)
        {
            await BootNewBackend(port, STA);
        }

        public static async Task StopNewBackend()
        {
            try
            {
                await Backend.Backend.backend.StopAsync();
            }
            catch (Exception) { }
        }

        internal static async Task UpgradeBackend(bool force = false)
        {
            try
            {
                var uri = new Uri("ms-appx:///Assets/backend_version.txt");
                StorageFile version = await StorageFile.GetFileFromApplicationUriAsync(uri);
                var ver = await FileIO.ReadTextAsync(version);
                var path = Path.Combine(LocalStateFolder, @"backend\version.txt");
                if (!File.Exists(path))
                {
                    Log($"backend version: {ver}(package) none(local)");
                }
                else
                {
                    Log($"backend version: {ver}(package) {File.ReadAllText(path)}(local)");
                }


                if (force || !Path.Exists(path) || File.ReadAllText(path) != ver)
                {
                    await AudioCloneHelper.Kill();
                    Log(string.Format(localize("Init_Stage2"), Path.Exists(path) ? localize("Init_Install") : localize("Init_Upgrade"), ver), "showToGUI");
                    uri = new Uri("ms-appx:///Assets/backend.zip");
                    StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(uri);
                    ZipArchive zipArchive = new ZipArchive(await file.OpenStreamForReadAsync(), ZipArchiveMode.Read);
                    var destPath = LocalStateFolder;
                    if (Directory.Exists(Path.Combine(destPath, "backend")))
                    {
                        try
                        {
                            Directory.Delete(Path.Combine(destPath, "backend"), true);
                        }
                        catch
                        {

                        }
                    }
                    await Task.Run(() =>
                    {
                        zipArchive.ExtractToDirectory(destPath, true);
                    });
                    File.WriteAllText(path, ver);
                }

                if (!Directory.Exists(Path.Combine(LocalStateFolder, @"wwwroot")))
                {
                    Directory.CreateDirectory(Path.Combine(LocalStateFolder, @"wwwroot"));
                }

                BackendVersionCode = ver;
                try
                {
                    var backendAsbPath = Path.Combine(LocalStateFolder, @"backend\AudioClone.Server.dll");
                    var backendAsb = Assembly.LoadFrom(backendAsbPath);
                    var backendHash = await AlgorithmServices.ComputeFileSHA256Async(backendAsbPath);
                    Log($"Backend assembly info:{backendAsb.FullName} SHA256:{backendHash}");
                }
                catch
                {
                    Log($"Backend assembly info unavailable!", "warn");

                }
            }
            catch(Exception ex)
            {
                await ShowDialogue(localize("Info"), localize("AppCorrupted")+$"\r\n({ex.GetType().Name} exception: {ex.Message})", localize("Accept"), null, MainWindow.xamlRoot);
                Environment.Exit(1);
            }

        }

        public static async Task BootNewBackend(int port = -1, bool STA = false)
        {
            if (Backend.Backend.Running)
            {
                if (port != -1)
                {
                    await Backend.Backend.backend.StopAsync(default);
                    await BootBackend(port);
                    return;
                }
                else
                {
                    return;
                }
            }
            Log($"Port: {port}");
            if (port == -1) port = int.Parse(SettingUtility.GetOrAddSettings("backendPort", "23456"));
            BackendPort = port;

            try
            {
                Backend.Backend.Init($"http://+:{BackendPort}", STA);
            }
            catch (Exception ex)
            {
                Log(ex, "Boot new backend", "Backend");
            }
            
        }

        #endregion

        #region init

        public static void InitBackend()
        {
            BackendHelper.GetOrAddSettings = SettingUtility.GetOrAddSettings;
            BackendHelper.SetSettings = SettingUtility.SetSettings;
            BackendHelper.ExistsSetting = SettingUtility.Exists;
            BackendHelper.LocalStateFolder = LocalStateFolder;
            BackendHelper.AudioCopyVersion = VersionString;

            BackendHelper.Log = Log;
            BackendHelper.LogEx = Log;
            BackendHelper.localize = localize;

            BackendHelper.Dispatch = new(async (f) =>
            {
                var tcs = new TaskCompletionSource();

                MainWindow.dispatcher.TryEnqueue(DispatcherQueuePriority.High, async () =>
                {
                    try
                    {
                        f.Start();

                        await f;

                        tcs.SetResult();
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                        throw;
                    }


                });
                await tcs.Task;

            });

            BackendHelper.GetCurrentMediaInfoAsync = new(() =>
            {
                return GetCurrentMediaInfoAsync().GetAwaiter().GetResult();
            });

            BackendHelper.BootAudioClone = new(() =>
            {
                AudioCloneHelper.Boot().GetAwaiter().GetResult();
                BackendHelper.CloneAddress = $"{AudioCloneHelper.Port}/api/audio/{{0}}?token={AudioCloneHelper.Token}&clientName={{1}}";
            });

            BackendHelper.ShowDialogueWithRoot = new Func<string, string, string, string?, Task<bool>>(async (a, b, c, d) =>
            {
                var tcs = new TaskCompletionSource<bool>();

                MainWindow.dispatcher.TryEnqueue(DispatcherQueuePriority.High, async () =>
                {

                    try
                    {                   
                        var result = await ShowDialogue(a, b, c, d, MainWindow.xamlRoot);

                        tcs.SetResult(result);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                        throw;
                    }


                });
                return await tcs.Task;
            });

            BackendHelper.ShowSpecialDialogue = new Func<string, object, Task<bool>>(async (a, b) =>
            {
                var tcs = new TaskCompletionSource<bool>();

                MainWindow.dispatcher.TryEnqueue(DispatcherQueuePriority.High, async () =>
                {

                    try
                    {
                        ContentDialog c = new();
                        switch (a)
                        {
                            case "PairDetected":
                                var kvp = (KeyValuePair<string, string[]>)b;
                                var Title = localize("PairDetected", kvp.Key);
                                var Content = localize("Select") + "\r\n" + kvp.Value.Aggregate((x, y) => $"{x} {y}");

                                MainWindow.pairBar.Title = Title;
                                MainWindow.pairBar.Visibility = Visibility.Visible;
                                MainWindow.pairBar.IsOpen = true;
                                MainWindow.pairBar.Content = new TextBlock
                                {
                                    Text = Content,
                                    FontSize = 36,
                                    TextWrapping = TextWrapping.Wrap,
                                    Margin = new Thickness(0, 0, 0, 24)
                                };

                                break;
                            case "PairFinished":
                                MainWindow.pairBar.IsOpen = false;
                                MainWindow.pairBar.IsClosable = false;

                                break;
                        }
                        tcs.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                        throw;
                    }


                });
                return await tcs.Task;
            });



            BackendHelper.ThisDeviceModel = GetDeviceModel();

            if (BackendHelper.ThisDeviceModel.Contains("VMWare") || BackendHelper.ThisDeviceModel.Contains("VirtualBox") || BackendHelper.ThisDeviceModel.Contains("Hyper-V"))
            {
                SettingUtility.SetSettings("ShowAllAdapter", true.ToString());

            }

            BackendHelper.ThisDeviceUdid = SettingUtility.GetOrAddSettings("udid", AlgorithmServices.MakeRandString(128)); 
        }

        internal static async Task PostInit()
        {
            if (!bool.Parse(SettingUtility.GetOrAddSettings("DisableTray", "False")))
            {
                AudioCopyUI_MiddleWare.TrayHelper.GUIRunning = true;
                AudioCopyUI_MiddleWare.TrayHelper.Resource.Shutdown = localize("Tray_Shutdown");
                AudioCopyUI_MiddleWare.TrayHelper.Resource.Close = localize("Tray_Close");
                AudioCopyUI_MiddleWare.TrayHelper.Resource.Launch = localize("Tray_Launch");
                AudioCopyUI_MiddleWare.TrayHelper.Resource.Exit = localize("Tray_ExitOptions");
                AudioCopyUI_MiddleWare.TrayHelper.Resource.DisconnectWarn = localize("/Setting/AudioQuality_RebootRequired");
                AudioCopyUI_MiddleWare.TrayHelper.Resource.Reboot = localize("RebootApp");

                AudioCopyUI_MiddleWare.TrayHelper.NoKeepClone = bool.Parse(SettingUtility.GetOrAddSettings("NoKeepCloneRun", "False"));
                AudioCopyUI_MiddleWare.TrayHelper.IsNotStandalone = true;
                AudioCopyUI_MiddleWare.TrayHelper.KeepBackendAsDefault = SettingUtility.GetOrAddSettings("CloseAction", "null") == "MinimizeToTray";

                AudioCopyUI_MiddleWare.TrayHelper.Shutdown = new Action(() => ExitApp(false));
                AudioCopyUI_MiddleWare.TrayHelper.BootApp = new(async () =>
                {
                    if (AppRunning) //带到前台
                    {
                        MainWindow.dispatcher.TryEnqueue(DispatcherQueuePriority.High,
                            () =>
                            {
                                if (App.Window is not null)
                                {
                                    App.Window.Activate();
                                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Window);
                                    if (hwnd != IntPtr.Zero)
                                    {
                                        NativeMethods.SetForegroundWindow(hwnd);
                                        NativeMethods.SetFocus(hwnd);
                                    }
                                }
                            });
                        return;
                    }
                    Log("Restarting GUI...");
                    SettingUtility.SetSettings("logPath", ___LogPath___);
                    Process.Start(new ProcessStartInfo { FileName = "audiocopy:fromTray", UseShellExecute = true });
                    await Task.Delay(800);
                    Program.ExitApp();
                });
                AudioCopyUI_MiddleWare.TrayHelper.CloseApp = CloseGUI;
                AudioCopyUI_MiddleWare.TrayHelper.RebootApp = new(() => { ExitApp(true); });
                Log("Booting tray...");
                Thread trayThread = new(AudioCopyUI_Tray.Program.Main);
                trayThread.Start();
            }
            else Log("User disabled tray.");


            foreach (var item in ApplicationData.Current.LocalSettings.Values)
            {
                Log($"Setting: {item.Key} : {item.Value}", "diag");
            }

        }


        #endregion

        #region GUI

        static Thread appThread = new(BootGUI);

        static void BootGUI()
        {
            AppRunning = true;  
            Log("Starting GUI...");
            //from:App.g.i.cs
            global::WinRT.ComWrappersSupport.InitializeComWrappers(); 
            global::Microsoft.UI.Xaml.Application.Start((p) =>
            {
                var context = new global::Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(global::Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
                global::System.Threading.SynchronizationContext.SetSynchronizationContext(context);
                new App();
            });
            AppRunning = false;
        }

        public static bool RebootGUI = false;

        public static void CloseGUI()
        {
            MainWindow.dispatcher.TryEnqueue(
                                        DispatcherQueuePriority.High,
                                        () =>
                                        {
                                            AppRunning = false;
                                            App.Window?.Close();
                                            AudioCopyUI_MiddleWare.TrayHelper.GUIRunning = false;
                                            Log("GUI closed.");
                                        }
                                        );
        }

        #endregion
    
    }

}
