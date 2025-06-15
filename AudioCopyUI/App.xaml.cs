/*
*	 File: App.xaml.cs
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
using Microsoft.UI.Xaml.Shapes;
using Microsoft.Windows.AppLifecycle;
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
using Windows.ApplicationModel.Resources;
using Windows.ApplicationModel.Resources.Core;
using Windows.Storage;
using static AudioCopyUI.Logger;
using Path = System.IO.Path;
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AudioCopyUI
{

    public static class Program
    {
        private static Process? backendProcess = null;
        public static ConcurrentQueue<string> logs = new();
        internal static bool AlreadyAddMyself;

        public static int BackendPort = -1;
        public static string BackendVersionCode = "unknown";
        public static bool IsBackendRunning => !(backendProcess is not null ? backendProcess.HasExited : true);
        public static bool CloseApplication { get; private set; }
        public static CancellationTokenSource ApplicationCloseTokenSource { get; private set; }
        public static bool AppRunning { get; private set; }
        public static bool ExitHandled { get; internal set; }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int MessageBox(IntPtr hWnd, String text, String caption, uint type);

        public static void ExitApp(bool reboot = false)
        {
            __FlushLog__();
            Thread.Sleep(100);
            if (reboot)
            {
                HttpClient c = new();
                c.BaseAddress = new($"http://127.0.0.1:{SettingUtility.GetOrAddSettings("defaultPort", "23456")}/");
                _ = c.GetAsync($"api/device/RebootClient?hostToken={SettingUtility.HostToken}&delay=50");
                Thread.Sleep(10000);
            }
            ApplicationCloseTokenSource.Cancel();
            Environment.Exit(0);
        }

        

        public static void RebootApp(int delay = 50)
        {
            __FlushLog__();
            Thread.Sleep(100);
            HttpClient c = new();
            c.BaseAddress = new($"http://127.0.0.1:{SettingUtility.GetOrAddSettings("defaultPort", "23456")}/");
            _ = c.GetAsync($"api/device/RebootClient?hostToken={SettingUtility.HostToken}&delay={delay}");
            Thread.Sleep(45);
            Environment.Exit(0);
        }

        public static async Task KillBackend()
        {
            //await KillOldBackend();
            await StopNewBackend();
            await AudioCloneHelper.Kill();
        }

        public static async Task BootBackend(int port = -1,bool STA = false)
        {
            //if (SettingUtility.OldBackend) await BootOldBackend(port);
            await BootNewBackend(port, STA);
        }

        public static async Task KillOldBackend()
        {
            throw new NotSupportedException("Old backend is no longer supported.");
            if (backendProcess is not null)
                try
                {
                    backendProcess.Kill();
                }
                catch (Exception) { }

            ProcessStartInfo i = new ProcessStartInfo
            {
                FileName = "taskkill.exe",
                Arguments = "/t /f /im \"libAudioCopy-Backend.exe\" ",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            Log(localize("Init_Stage1"),"showToGUI");

            var p = Process.Start(i);
            p.WaitForExit();
            Log($"Taskkill write stdout:{p.StandardOutput.ReadToEnd()} stderr:{p.StandardError.ReadToEnd()}");
            

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
                Log(string.Format(localize("Init_Stage2"), Path.Exists(path) ? "更新" : "安装并初始化", ver), "showToGUI");
                uri = new Uri("ms-appx:///Assets/backend.zip");
                StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(uri);
                ZipArchive zipArchive = new ZipArchive(await file.OpenStreamForReadAsync(), ZipArchiveMode.Read);
                var destPath = LocalStateFolder;
                //Directory.CreateDirectory(destPath);
                if (force || (Path.Exists(path) && int.TryParse(File.ReadAllText(path).Split('.').FirstOrDefault(),out var v) && v == 1))
                {
                    Directory.Delete(Path.Combine(destPath,"backend"), true);
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
                Log($"Backend assembly info unavailable!","warn");

            }

        }

        

        public static async Task BootOldBackend(int port = -1)
        {
            throw new NotSupportedException("Old backend is no longer supported.");

            if (port != -1)
            {
                KillBackend();
                Log($"User override port to:{port}");
            }

            var backendPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, @"backend\libAudioCopy_Backend.exe");
            var i = new ProcessStartInfo
            {
                FileName = backendPath,
                WorkingDirectory = Path.Combine(LocalStateFolder, "backend"),
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,

            };

            var hostToken = AlgorithmServices.MakeRandString(256);
            SettingUtility.SetSettings("hostToken", hostToken);

            if (port == -1) port = int.Parse(SettingUtility.GetOrAddSettings("backendPort", "23456"));
            //else SettingUtility.SetSettings("backendPort", port.ToString());
            BackendPort = port;


            var options = JsonSerializer.Deserialize<Dictionary<string, string>>(SettingUtility.GetSetting("backendOptions") ?? "null");
            if (bool.Parse(SettingUtility.GetOrAddSettings("ForceDefaultBackendSettings", "False")) || options is null)
            {
                i.EnvironmentVariables.Add("ASPNETCORE_URLS", $"http://+:{port}");
#if DEBUG
                i.EnvironmentVariables.Add("ASPNETCORE_ENVIRONMENT", "Development");
#endif
            }
            else
            {
                Log($"Options: {SettingUtility.GetSetting("backendOptions")}");
                foreach (var item in options)
                {
                    i.EnvironmentVariables.Add(item.Key, item.Value);
                }

                string format;

                if ((format = SettingUtility.GetOrAddSettings("AudioDeviceName", "1")) != "1")
                {
                    i.EnvironmentVariables.Add("AudioCopy_DefaultAudioQuality", format);
                }

                if (!string.IsNullOrWhiteSpace(SettingUtility.GetOrAddSettings("AudioDeviceName", "")))
                {
                    var device = SettingUtility.GetOrAddSettings("AudioDeviceName", "");
                    i.EnvironmentVariables.Add("AudioCopy_DefaultDeviceName", device);
                }
            }

            i.EnvironmentVariables.Add("AudioCopy_hostToken", hostToken);
            i.EnvironmentVariables.Add("ASPNETCORE_CONTENTROOT", Path.Combine(LocalStateFolder, "wwwroot"));
            i.EnvironmentVariables.Add("ASPNETCORE_WEBROOT", Path.Combine(LocalStateFolder, "wwwroot"));


            if (port > 0 && port != 23456)
            {
                i.EnvironmentVariables["ASPNETCORE_URLS"] = $"http://+:{port}";
            }

            backendProcess = new();
            backendProcess.StartInfo = i;
            Exception? exc = null;
            bool loaded = false;
            backendProcess.OutputDataReceived += (sender, e) => { if (e.Data != null) Log(e.Data ?? "", "backend_stdout"); };
            backendProcess.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    Log(e.Data ?? "", "backend_stderr");
                }
            };

            Log(localize("Init_Stage3"), "showToGUI");
            await Task.Run(() =>
            {
                backendProcess.Start();
            });
            backendProcess.BeginOutputReadLine();
            backendProcess.BeginErrorReadLine();
            Log(localize("Init_Stage4"), "showToGUI");

            CancellationTokenSource cts = new();
            cts.CancelAfter(10000);

            await Task.Run(async () =>
            {
                Stopwatch sw = Stopwatch.StartNew();
                HttpClient c = new();
                c.BaseAddress = new($"http://127.0.0.1:{port}/");
                c.Timeout = new TimeSpan(0, 0, 5);
                Exception? exception = null;

                while (true)
                {
                    try
                    {
                        if ((await c.GetAsync("/index")).StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        {
                            Log("Backend booted.");
                            return;
                        }
                    }
                    catch(Exception ex)
                    {
                        Log(ex, "后端启动", Program.BootBackend);
                        exception = ex;
                    }
                    

                    if (sw.Elapsed.TotalSeconds > 20)
                    {
                        if (exception is not null) throw new InvalidOperationException(string.Format(localize("Init_StageFail"), $"{exception.GetType().Name}:{exception.Message}"), exception);

                        throw new InvalidOperationException(string.Format(localize("Init_StageFail"), "Request timeout."));
                    }
                }
            });




        }

        public static async Task BootNewBackend(int port = -1,bool STA = false)
        {
            if(Backend.Backend.Running)
            {
                if(port != -1)
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
            if(!SettingUtility.Exists("OldBackend")) SettingUtility.SetSettings("OldBackend", "True");
            Log($"Port: {port}");
            if (port == -1) port = int.Parse(SettingUtility.GetOrAddSettings("backendPort", "23456"));
            //else SettingUtility.SetSettings("backendPort", port.ToString());
            BackendPort = port;

            try
            {
                Backend.Backend.Init($"http://+:{BackendPort}",STA);
            }
            catch(Exception  ex)
            {
                Log(ex,"Boot new backend", "Backend");
            }

            await Task.Run(async () =>
            {
                Stopwatch sw = Stopwatch.StartNew();
                HttpClient c = new();
                c.BaseAddress = new($"http://127.0.0.1:{BackendPort}/");
                c.Timeout = new TimeSpan(0, 0, 5);



                Exception? exception = null;
                while (true)
                {
                    //if (exc is not null) throw exc;
                    try
                    {
                        if ((await (await c.GetAsync("/Detect")).Content.ReadAsStringAsync()) == "Ready")
                        {
                            Log("Backend booted.");
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log(ex, "后端启动", Program.BootBackend);
                        exception = ex;
                    }
                    await Task.Delay(50);

                    if (sw.Elapsed.TotalSeconds > 10)
                    {
                        if (exception is not null) throw new InvalidOperationException(string.Format(localize("Init_StageFail"), $"{exception.GetType().Name}:{exception.Message}"), exception);

                        throw new InvalidOperationException(string.Format(localize("Init_StageFail"), "Request timeout."));
                    }
                }
            });




        }

        public static async Task ChangeLang(string target)
        {
            SettingUtility.SetSettings("Language", target == "default" ? Windows.Globalization.Language.CurrentInputMethodLanguageTag : target);
            Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = Localizer.Match(target);
            await Task.Delay(10);
            var uri = new Uri("ms-appx:///Assets/ApplyLocalization.ps1");
            StorageFile f = await StorageFile.GetFileFromApplicationUriAsync(uri);
            Process.Start(new ProcessStartInfo { FileName = "powershell.exe", Arguments = $"-File \"{f.Path}\" \"{localize("/Setting/AdvancedSetting_ApplyLocate") }\"", UseShellExecute = true });
        }


        static string logPath = "";

        [global::System.STAThreadAttribute]
        static async Task Main(string[] args)
        {
            try
            {
                if (!Directory.Exists(Path.Combine(LocalStateFolder, "logs"))) Directory.CreateDirectory(Path.Combine(LocalStateFolder, "logs"));

                ApplicationCloseTokenSource = new();

                if (args.Length > 0)
                {
                    var tag = args[0].Split(':')[1];
                    if(tag == "fromTray")
                    {
                        _LoggerInit_(SettingUtility.GetOrAddSettings("logPath", "null"),true);
                    }
                    else
                    {
                        SettingUtility.SetSettings("logPath", "null");
                    }
                }

                Console.SetOut(new InterceptingTextWriter(Console.Out, "stdout"));

                Console.SetError(new InterceptingTextWriter(Console.Out, "stderr"));


#if DEBUG
                if (!SettingUtility.Exists("RealtimeLogging")) SettingUtility.SetSettings("RealtimeLogging", true.ToString());
#else
                if (!SettingUtility.Exists("RealtimeLogging")) SettingUtility.SetSettings("RealtimeLogging", false.ToString());  
#endif

                if (string.IsNullOrEmpty(___LogPath___)) _LoggerInit_(Path.Combine(LocalStateFolder, "logs"));

                //Console.WriteLine("This is a message from console.");
                //Console.Error.WriteLine("This is also a message from console.");

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

                if(bool.Parse(SettingUtility.GetOrAddSettings("ResetEverything", "False")))
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
                    await BootBackend(-1,true);
                });
                Thread trayThread = new(AudioCopyUI_Tray.Program.Main);
                backendThread.Start();
                if (!bool.Parse(SettingUtility.GetOrAddSettings("DisableTray", "False"))) trayThread.Start();

                appThread.Join();
                Debug.WriteLine("Appthread ended.");
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

        static void BootGUI()
        {
            AppRunning = true;
            AudioCopyUI_TrayHelper.TrayHelper.BootApp = new(async () =>
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
                Process.Start(new ProcessStartInfo { FileName = "audiocopy:fromTray" , UseShellExecute = true });
                await Task.Delay(800);
                Program.ExitApp();
            });
            AudioCopyUI_TrayHelper.TrayHelper.CloseApp = CloseGUI;
            AudioCopyUI_TrayHelper.TrayHelper.GetSMTC = new Action(async () =>
            {
                Backend.DeviceController.MediaInfo? i = null;
                MainWindow.dispatcher.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal,
                    async () =>
                    {
                        i = await Backend.DeviceController.GetCurrentMediaInfoAsync();
                    });
                await Task.Delay(1000);
                if (i is null)
                {
                    AudioCopyUI_TrayHelper.TrayHelper.Artist = "应用程序尚未启动";
                    AudioCopyUI_TrayHelper.TrayHelper.Title = "请先启动";
                    AudioCopyUI_TrayHelper.TrayHelper.listeningClient = -1;
                    return;
                }
                AudioCopyUI_TrayHelper.TrayHelper.Artist = i.Artist;
                AudioCopyUI_TrayHelper.TrayHelper.Title = i.Title;
                AudioCopyUI_TrayHelper.TrayHelper.listeningClient = 0; //todo:实现

            });
            AudioCopyUI_TrayHelper.TrayHelper.IsNotStandalone = true;
            AudioCopyUI_TrayHelper.TrayHelper.Shutdown = new Action(() => ExitApp(false));
            AudioCopyUI_TrayHelper.TrayHelper.KeepBackendAsDefault = SettingUtility.GetOrAddSettings("CloseAction", "null") == "MinimizeToTray";
            
            Log("Starting GUI...");
            global::WinRT.ComWrappersSupport.InitializeComWrappers(); //from:App.g.i.cs
            global::Microsoft.UI.Xaml.Application.Start((p) =>
            {
                var context = new global::Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(global::Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
                global::System.Threading.SynchronizationContext.SetSynchronizationContext(context);
                new App();
            });
            AppRunning = false;

        }

        static Thread appThread = new(BootGUI);

        public static bool RebootGUI = false;

        public static void CloseGUI()
        {
            MainWindow.dispatcher.TryEnqueue(
                                        DispatcherQueuePriority.High,
                                        () =>
                                        {
                                            App.Window?.Close();
                                            AudioCopyUI_TrayHelper.TrayHelper.GUIRunning = false;
                                            Log("GUI closed.");
                                        }
                                        );
        }

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
{localize("CrashReportHeader",VersionString, Path.Combine(LocalStateFolder, "crashlog"))}
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
{(string.IsNullOrEmpty(___LogPath___ ) ? "Unavailable" : File.ReadAllText(___LogPath___))}

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

        internal static async Task PostInit()
        {
            foreach (var item in ApplicationData.Current.LocalSettings.Values)
            {
                Log($"Setting: {item.Key} : {item.Value}", "diag");
            }


        }



    }


    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs e)
        {
            Application.Current.UnhandledException += (sender, e) =>
            {
                Program.Crash(e.Exception);
            };

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                try
                {
                    Program.Crash(e.ExceptionObject as Exception);
                }
                catch
                {
                    Program.Crash(new AggregateException("Cannot get the detailed exception info."));
                }
            };

            var mainInstance = Microsoft.Windows.AppLifecycle.AppInstance.FindOrRegisterForKey("main");
            var activatedEventArgs = Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs();

            if (!mainInstance.IsCurrent)
            {
                await mainInstance.RedirectActivationToAsync(activatedEventArgs);
                System.Diagnostics.Process.GetCurrentProcess().Kill();
                return;
            }

            if (activatedEventArgs.Kind == ExtendedActivationKind.File)
            {
                OnFileActivated(activatedEventArgs);
            }
            try
            {
                if (SettingUtility.Exists("Language"))
                {
                    var locate = SettingUtility.GetOrAddSettings("Language", Windows.Globalization.Language.CurrentInputMethodLanguageTag);
                    Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = locate;
                }
                else
                {
                    var inputTag = Windows.Globalization.Language.CurrentInputMethodLanguageTag;
                    string? selectedLang = Localizer.Match(inputTag);
                    Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = selectedLang ?? "en-US";
                }

                Log($"default lang:{Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride} Lang(in setting):{SettingUtility.GetOrAddSettings("Language", Windows.Globalization.Language.CurrentInputMethodLanguageTag)}");

            }
            catch(Exception ex)
            {
                Log(ex,"Set locate",this);
                Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride =  "en-US"; //fallback
                await Program.ChangeLang("en-US");
            }



            loader = ResourceLoader.GetForViewIndependentUse();

            // Initialize MainWindow here
            Window = new MainWindow();
            //Window.Closed += Window_Closed;
            Window.Activate();
            WindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(Window);

            AudioCopyUI_TrayHelper.TrayHelper.GUIRunning = true;
            AudioCopyUI_TrayHelper.TrayHelper.Resource.Shutdown = localize("Tray_Shutdown");
            AudioCopyUI_TrayHelper.TrayHelper.Resource.Close = localize("Tray_Close");
            AudioCopyUI_TrayHelper.TrayHelper.Resource.Exit = localize("Tray_Exit");
            AudioCopyUI_TrayHelper.TrayHelper.Resource.Launch = localize("Tray_Launch");
        }

        private async void Window_Closed(object sender, WindowEventArgs args)
        {
            string closePref = SettingUtility.GetOrAddSettings("CloseAction", "null");
            if (closePref == "null")
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = localize("Info"),
                    Content = localize("ExitOptions"), //"您希望关闭窗口时：\n\n- 完全退出程序\n- 最小化到托盘",
                    PrimaryButtonText = localize("ExitOption1"), //"完全退出",
                    SecondaryButtonText = localize("ExitOption2"), //"最小化到托盘",
                    CloseButtonText = localize("Cancel"), //"取消",
                    XamlRoot = MainWindow.xamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    SettingUtility.SetSettings("CloseAction", "Exit");
                    Program.ExitApp();
                }
                else if (result == ContentDialogResult.Secondary)
                {
                    SettingUtility.SetSettings("CloseAction", "MinimizeToTray");
                    Program.CloseGUI();
                }
                else
                {
                    args.Handled = true;
                }
            }
            else if (closePref == "Exit")
            {
                Program.ExitApp();
            }
            else if (closePref == "MinimizeToTray")
            {
                Program.CloseGUI();
                args.Handled = true;
            }
        }

        // TODO This is an example method for the case when app is activated through a file.
        // Feel free to remove this if you do not need this.
        public void OnFileActivated(AppActivationArguments activatedEventArgs)
        {

        }

        public static MainWindow Window { get; private set; }

        public static IntPtr WindowHandle { get; private set; }

        public static ResourceLoader? loader = null;

    }

    internal static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr SetFocus(IntPtr hWnd);
    }
}
