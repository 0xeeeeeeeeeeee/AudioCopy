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
        public static string BackendVersionCode = "unknown";
        public static ConcurrentQueue<string> logs = new();
        internal static bool AlreadyAddMyself;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int MessageBox(IntPtr hWnd, String text, String caption, uint type);

        public static void ExitApp(bool reboot = false)
        {
            __FlushLog__();
            if (!reboot && !bool.Parse(SettingUtility.GetOrAddSettings("KeepBackendRun", "False"))) KillBackend();
            Thread.Sleep(100);
            if (reboot)
            {
                HttpClient c = new();
                c.BaseAddress = new($"http://127.0.0.1:{SettingUtility.GetOrAddSettings("defaultPort", "23456")}/");
                _ = c.GetAsync($"api/device/RebootClient?hostToken={SettingUtility.HostToken}&delay=50");
                Thread.Sleep(30);
            }
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

        public static void KillBackend()
        {
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

        internal static async Task UpgradeBackend(bool force = false)
        {
            Program.KillBackend();
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
                Log(string.Format(localize("Init_Stage2"), Path.Exists(path) ? "更新" : "安装并初始化", ver), "showToGUI");
                uri = new Uri("ms-appx:///Assets/backend.zip");
                StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(uri);
                ZipArchive zipArchive = new ZipArchive(await file.OpenStreamForReadAsync(), ZipArchiveMode.Read);
                var destPath = Path.Combine(LocalStateFolder, "backend");
                Directory.CreateDirectory(destPath);
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
                var backendAsbPath = Path.Combine(LocalStateFolder, @"backend\libAudioCopy-Backend.dll");
                var backendAsb = Assembly.LoadFrom(backendAsbPath);
                var backendHash = await AlgorithmServices.ComputeFileSHA256Async(backendAsbPath);
                Log($"Backend assembly info:{backendAsb.FullName} SHA256:{backendHash}");
            }
            catch
            {
                Log($"Backend assembly info unavailable!","warn");

            }

        }

        public static int BackendPort = -1;

        public static async Task BootBackend(int port = -1)
        {
//            if (port != -1)
//            {
//                KillBackend();
//                Log($"User override port to:{port}");
//            }

//            var backendPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, @"backend\libAudioCopy-Backend.exe");
//            var i = new ProcessStartInfo
//            {
//                FileName = backendPath,
//                WorkingDirectory = Path.Combine(LocalStateFolder, "backend"),
//                RedirectStandardError = true,
//                RedirectStandardOutput = true,
//                RedirectStandardInput = true,
//                UseShellExecute = false,
//                CreateNoWindow = true,

//            };

//            var hostToken = AlgorithmServices.MakeRandString(256);
//            SettingUtility.SetSettings("hostToken", hostToken);

//            if (port == -1) port = int.Parse(SettingUtility.GetOrAddSettings("backendPort", "23456"));
//            else SettingUtility.SetSettings("backendPort", port.ToString());
//            BackendPort = port;


//            var options = JsonSerializer.Deserialize<Dictionary<string, string>>(SettingUtility.GetSetting("backendOptions") ?? "null");
//            if (bool.Parse(SettingUtility.GetOrAddSettings("ForceDefaultBackendSettings", "False")) || options is null)
//            {
//                i.EnvironmentVariables.Add("ASPNETCORE_URLS", $"http://+:{port}");
//#if DEBUG
//                i.EnvironmentVariables.Add("ASPNETCORE_ENVIRONMENT", "Development");
//#endif
//            }
//            else
//            {
//                Log($"Options: {SettingUtility.GetSetting("backendOptions")}");
//                foreach (var item in options)
//                {
//                    i.EnvironmentVariables.Add(item.Key, item.Value);
//                }

//                string format;

//                if ((format = SettingUtility.GetOrAddSettings("AudioDeviceName", "1")) != "1")
//                {
//                    i.EnvironmentVariables.Add("AudioCopy_DefaultAudioQuality", format);
//                }

//                if (!string.IsNullOrWhiteSpace(SettingUtility.GetOrAddSettings("AudioDeviceName", "")))
//                {
//                    var device = SettingUtility.GetOrAddSettings("AudioDeviceName", "");
//                    i.EnvironmentVariables.Add("AudioCopy_DefaultDeviceName", device);
//                }
//            }

//            i.EnvironmentVariables.Add("AudioCopy_hostToken", hostToken);
//            i.EnvironmentVariables.Add("ASPNETCORE_CONTENTROOT", Path.Combine(LocalStateFolder, "wwwroot"));
//            i.EnvironmentVariables.Add("ASPNETCORE_WEBROOT", Path.Combine(LocalStateFolder, "wwwroot"));


//            if(port > 0 && port != 23456)
//            {
//                i.EnvironmentVariables["ASPNETCORE_URLS"] = $"http://+:{port}";
//            }

//            backendProcess = new();
//            backendProcess.StartInfo = i;
//            Exception? exc = null;
//            bool loaded = false;
//            backendProcess.OutputDataReceived += (sender, e) => { if (e.Data != null) Log(e.Data ?? "", "backend_stdout"); };
//            backendProcess.ErrorDataReceived += (sender, e) =>
//            {
//                if (e.Data != null)
//                {
//                    Log(e.Data ?? "", "backend_stderr");
//                    if (e.Data.StartsWith("ERROR!"))
//                    {
//                        var str = e.Data.Substring(6);
//                        BackendExceptionObject ex = JsonSerializer.Deserialize<BackendExceptionObject>(str);
//                        if (ex is not null)
//                        {
//                            try
//                            {
//                                Log(ex.ToException(), "后端", backendProcess);
//                                if (___PublicStackOn___) exc = ex.ToException();
//                                else Log(ex.ToException(),"backend","BackendProcess");
//                            }
//                            catch (Exception ex1)
//                            {
//                                Log(new Exception($"Failed to convert exception string:{ex} because {ex1}",ex1), "后端", backendProcess);

//                            }

//                        }
//                        else
//                        {
//                            Log(new Exception($"Unreadable exception string:{ex}"), "后端", backendProcess);
//                        }
//                    }
//                }
//            };
                
            //Log(localize("Init_Stage3"), "showToGUI");
            //await Task.Run(() =>
            //{
            //    backendProcess.Start();
            //});
            //backendProcess.BeginOutputReadLine();
            //backendProcess.BeginErrorReadLine();
            //Log(localize("Init_Stage4"), "showToGUI");

            //CancellationTokenSource cts = new();
            //cts.CancelAfter(10000);

            //await Task.Run(async () =>
            //{
            //    Stopwatch sw = Stopwatch.StartNew();
            //    HttpClient c = new();
            //    c.BaseAddress = new($"http://127.0.0.1:{SettingUtility.GetOrAddSettings("backendPort", "23456")}/");
            //    c.Timeout = new TimeSpan(0, 0, 5);



            //    Exception? exception = null;
            //    while (true)
            //    {
            //        if (exc is not null) throw exc;
            //        try
            //        {
            //            if ((await c.GetAsync("/index")).StatusCode == System.Net.HttpStatusCode.Unauthorized)
            //            {
            //                Log("Backend booted.");
            //                return;
            //            }
            //        }
            //        catch (Exception ex)
            //        {
            //            Log(ex, "后端启动", Program.BootBackend);
            //            exception = ex;
            //        }
            //        await Task.Delay(50);

            //        if (sw.Elapsed.TotalSeconds > 15)
            //        {
            //            if (exception is not null) throw new InvalidOperationException(string.Format(localize("Init_StageFail"), $"{exception.GetType().Name}:{exception.Message}"), exception);

            //            throw new InvalidOperationException(string.Format(localize("Init_StageFail"), "Request timeout."));
            //        }
            //    }
            //});

            


        }

        public static async Task ChangeLang(string target)
        {
            SettingUtility.SetSettings("Language", target == "default" ? Windows.Globalization.Language.CurrentInputMethodLanguageTag : target);
            Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = Localizer.Match(target);
            await Task.Delay(5);
            var uri = new Uri("ms-appx:///Assets/ApplyLocalization.ps1");
            StorageFile f = await StorageFile.GetFileFromApplicationUriAsync(uri);
            Process.Start(new ProcessStartInfo { FileName = "powershell.exe", Arguments = $"-File {f.Path} \"{localize("/Setting/AdvancedSetting_ApplyLocate") }\"", UseShellExecute = true });
        }



        [global::System.STAThreadAttribute]
        static async Task Main(string[] args)
        {
            try
            {
                if (File.Exists(Path.Combine(LocalStateFolder, "wait.txt")))
                {
                    _ = MessageBox(new IntPtr(0), $"点击确定来继续启动", localize("Info"), 0);
                }
                if (!Directory.Exists(Path.Combine(LocalStateFolder, "logs"))) Directory.CreateDirectory(Path.Combine(LocalStateFolder, "logs"));
                ___PublicStackOn___ = true;

                _LoggerInit_(Path.Combine(LocalStateFolder, "logs"));
            }
            catch (Exception ex)
            {
                Environment.FailFast(ex.Message, ex);
                Environment.Exit(1);
            }
            try
            {
                
                Log("Starting GUI...");
                global::WinRT.ComWrappersSupport.InitializeComWrappers();
                global::Microsoft.UI.Xaml.Application.Start((p) =>
                {
                    var context = new global::Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(global::Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
                    global::System.Threading.SynchronizationContext.SetSynchronizationContext(context);
                    new App();
                });
            }
            catch (Exception ex)
            {
                Crash(ex);
            }

        }

        internal static void Crash(Exception ex)
        {
            try
            {
                Log(ex, true);
                __FlushLog__();
                KillBackend();
            }
            finally
            {
                Thread.Sleep(100);
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
            Window.Closed += Window_Closed;
            Window.Activate();
            WindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(Window);
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            Program.ExitApp();
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
}
