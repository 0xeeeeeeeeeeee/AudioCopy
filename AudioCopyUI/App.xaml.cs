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
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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

            Log("结束已有的后端...","showToGUI");

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
                Log($"正在{(Path.Exists(path) ? "更新" : "安装并初始化")}后端到v{ver}，可能会花上更多的时间来启动，若杀毒软件有提示请放行。", "showToGUI");
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
        }

        public static async Task BootBackend()
        {
            var backendPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, @"backend\libAudioCopy-Backend.exe");
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

            var options = JsonSerializer.Deserialize<Dictionary<string, string>>(SettingUtility.GetSetting("backendOptions") ?? "null");
            if (bool.Parse(SettingUtility.GetOrAddSettings("ForceDefaultBackendSettings", "False")) || options is null)
            {
                i.EnvironmentVariables.Add("ASPNETCORE_URLS", $"http://+:{SettingUtility.GetOrAddSettings("backendPort", "23456")}");
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
            }

            i.EnvironmentVariables.Add("AudioCopy_hostToken", hostToken);
            i.EnvironmentVariables.Add("ASPNETCORE_CONTENTROOT", Path.Combine(LocalStateFolder, "wwwroot"));
            i.EnvironmentVariables.Add("ASPNETCORE_WEBROOT", Path.Combine(LocalStateFolder, "wwwroot"));



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
                    if (e.Data.StartsWith("ERROR!"))
                    {
                        var str = e.Data.Substring(6);
                        BackendExceptionObject ex = JsonSerializer.Deserialize<BackendExceptionObject>(str);
                        if (ex is not null)
                        {
                            try
                            {
                                Log(ex.ToException(), "后端", backendProcess);
                                exc = ex.ToException();
                            }
                            catch(Exception ex1)
                            {
                                Log(new Exception($"Failed to convert exception string:{ex} because {ex1}",ex1), "后端", backendProcess);

                            }

                        }
                        else
                        {
                            Log(new Exception($"Unreadable exception string:{ex}"), "后端", backendProcess);
                        }
                    }
                }
            };
                
            Log("启动后端中(若杀毒软件提示请放行)...", "showToGUI");
            backendProcess.Start();
            backendProcess.BeginOutputReadLine();
            backendProcess.BeginErrorReadLine();
            Log("等待后端启动...", "showToGUI");

            CancellationTokenSource cts = new();
            cts.CancelAfter(10000);

            await Task.Run(async () =>
            {
                Stopwatch sw = Stopwatch.StartNew();
                HttpClient c = new();
                c.BaseAddress = new($"http://127.0.0.1:{SettingUtility.GetOrAddSettings("defaultPort", "23456")}/");
                c.Timeout = new TimeSpan(0, 0, 5);



                Exception? exception = null;
                while (true)
                {
                    if (exc is not null) throw exc;
                    try
                    {
                        if ((await c.GetAsync("/index")).StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        {
                            Log("后端启动成功", "showToGUI");
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
                        if (exception is not null) throw new InvalidOperationException($"后端启动失败({exception.GetType().Name}:{exception.Message})\r\n请检查后端是否被杀毒软件拦截或被其他程序占用", exception);

                        throw new InvalidOperationException($"后端启动失败(等待状态超时)\r\n请检查后端是否被杀毒软件拦截或被其他程序占用");
                    }
                }
            });

            


        }


        public class BackendExceptionObject
        {
            public string Type { get; set; }
            public string Message { get; set; }
            public string StackTrace { get; set; }
            public string? InnerException { get; set; }

            public Exception ToException()
            {
                var obj = this;

                Exception inner = InnerException is null ? null : new Exception(InnerException);
                

                Type exType = null;
                if (!string.IsNullOrWhiteSpace(obj.Type))
                {
                    exType = System.Type.GetType(obj.Type)
                        ?? AppDomain.CurrentDomain.GetAssemblies()
                            .Select(a => a.GetType(obj.Type))
                            .FirstOrDefault(t => t != null);
                }

                Exception ex = null;
                if (exType != null && typeof(Exception).IsAssignableFrom(exType))
                {
                    try
                    {
                        ex = (Exception)Activator.CreateInstance(exType, obj.Message, inner);
                    }
                    catch
                    {
                        try
                        {
                            ex = (Exception)Activator.CreateInstance(exType, obj.Message);
                        }
                        catch
                        {
                            try
                            {
                                ex = (Exception)Activator.CreateInstance(exType);
                            }
                            catch
                            {
                                ex = new Exception(obj.Message, inner);
                            }
                        }
                    }
                }

                if (ex == null)
                {
                    ex = new Exception(obj.Message, inner);
                }

                if (!string.IsNullOrWhiteSpace(obj.StackTrace))
                {
                    ex.Data["RemoteStackTrace"] = obj.StackTrace;
                }

                return ex;
            }

        }


        [global::System.STAThreadAttribute]
        static async Task Main(string[] args)
        {
            try
            {
                if (File.Exists(Path.Combine(LocalStateFolder, "wait.txt")))
                {
                    _ = MessageBox(new IntPtr(0), $"点击确定来继续启动", "提示", 0);
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
                Program.Crash(e.ExceptionObject as Exception);
            };


            //TaskScheduler.UnobservedTaskException += (sender, e) =>
            //{ 
            //    Program.Crash(e.Exception);
            //};

            // TODO This code defaults the app to a single instance app. If you need multi instance app, remove this part.
            // Read: https://docs.microsoft.com/en-us/windows/apps/windows-app-sdk/migrate-to-windows-app-sdk/guides/applifecycle#single-instancing-in-applicationonlaunched
            // If this is the first instance launched, then register it as the "main" instance.
            // If this isn't the first instance launched, then "main" will already be registered,
            // so retrieve it.
            var mainInstance = Microsoft.Windows.AppLifecycle.AppInstance.FindOrRegisterForKey("main");
            var activatedEventArgs = Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs();

            // If the instance that's executing the OnLaunched handler right now
            // isn't the "main" instance.
            if (!mainInstance.IsCurrent)
            {
                // Redirect the activation (and args) to the "main" instance, and exit.
                await mainInstance.RedirectActivationToAsync(activatedEventArgs);
                System.Diagnostics.Process.GetCurrentProcess().Kill();
                return;
            }


            // TODO This code handles app activation types. Add any other activation kinds you want to handle.
            // Read: https://docs.microsoft.com/en-us/windows/apps/windows-app-sdk/migrate-to-windows-app-sdk/guides/applifecycle#file-type-association
            if (activatedEventArgs.Kind == ExtendedActivationKind.File)
            {
                OnFileActivated(activatedEventArgs);
            }




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
    }
}
