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
using Microsoft.UI.Xaml.Shapes;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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

        public static void ExitApp()
        {
            __FlushLog__();
            KillBackend();
            Thread.Sleep(100);
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
                new Thread(() =>
                {
                    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
                    static extern int MessageBox(IntPtr hWnd, String text, String caption, uint type);
                    _ = MessageBox(new IntPtr(0), $"正在{(Path.Exists(path)? "更新" :"安装并初始化")}后端，可能会花上更多的时间来启动，若杀毒软件有提示请放行。", "提示", 0);
                }).Start();

                Log("Upgrading backend...");
                uri = new Uri("ms-appx:///Assets/backend.zip");
                StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(uri);
                ZipArchive zipArchive = new ZipArchive(await file.OpenStreamForReadAsync(), ZipArchiveMode.Read);
                var destPath = Path.Combine(LocalStateFolder, "backend");
                Directory.CreateDirectory(destPath);
                zipArchive.ExtractToDirectory(destPath, true);
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
            ProcessStartInfo i = new ProcessStartInfo
            {
                FileName = "taskkill.exe",
                Arguments = "/t /f /im \"libAudioCopy-Backend.exe\" ",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            Log("Killing exist backend...");

            var p = Process.Start(i);
            p.WaitForExit();
            Log($"Taskkill write stdout:{p.StandardOutput.ReadToEnd()} stderr:{p.StandardError.ReadToEnd()}");



            var backendPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, @"backend\libAudioCopy-Backend.exe");
            i = new ProcessStartInfo
            {
                FileName = backendPath,
                WorkingDirectory = Path.Combine(LocalStateFolder, "backend"),
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,

            };


            await UpgradeBackend(bool.Parse(SettingUtility.GetOrAddSettings("ForceUpgradeBackend", "False")));
            SettingUtility.SetSettings("ForceUpgradeBackend", "False");

            var hostToken = AlgorithmServices.MakeRandString(256);
            SettingUtility.SetSettings("hostToken", hostToken);

            var options = JsonSerializer.Deserialize<Dictionary<string, string>>(SettingUtility.GetSetting("backendOptions") ?? "null");
            if(bool.Parse(SettingUtility.GetOrAddSettings("ForceDefaultBackendSettings", "False")) || options is null)
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
            i.EnvironmentVariables.Add("ASPNETCORE_CONTENTROOT", Path.Combine(LocalStateFolder,"wwwroot"));
            i.EnvironmentVariables.Add("ASPNETCORE_WEBROOT", Path.Combine(LocalStateFolder, "wwwroot"));
            


            string format;

            if ((format = SettingUtility.GetOrAddSettings("AudioDeviceName", "1")) != "1")
            {
                i.EnvironmentVariables.Add("AudioCopy_DefaultAudioQuality", format);
            }

            if (!string.IsNullOrWhiteSpace(SettingUtility.GetOrAddSettings("AudioDeviceName", "")) )
            {
                var device = SettingUtility.GetOrAddSettings("AudioDeviceName", "");
                i.EnvironmentVariables.Add("AudioCopy_DefaultDeviceName", device);
            }

            backendProcess = new();
            backendProcess.StartInfo = i;

            backendProcess.OutputDataReceived += (sender, e) => { if (e.Data != null) Log(e.Data ?? "","backend_stdout"); };
            backendProcess.ErrorDataReceived += (sender, e) => { if (e.Data != null) Log(e.Data ?? "", "backend_stderr"); };
            Log("Starting backend...");
            backendProcess.Start();
            backendProcess.BeginOutputReadLine();
            backendProcess.BeginErrorReadLine();
        }



        [global::System.STAThreadAttribute]
        static async Task Main(string[] args)
        {
            try
            {
                if (File.Exists(Path.Combine(LocalStateFolder, "wait.txt")))
                {
                    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
                    static extern int MessageBox(IntPtr hWnd, String text, String caption, uint type);
                    _ = MessageBox(new IntPtr(0), $"点击确定来继续启动", "提示", 0);
                }
                if (!Directory.Exists(Path.Combine(LocalStateFolder, "logs"))) Directory.CreateDirectory(Path.Combine(LocalStateFolder, "logs"));
                _LoggerInit_(Path.Combine(LocalStateFolder, "logs"));
            }
            catch(Exception ex)
            {
                Environment.FailFast(ex.Message,ex);
                Environment.Exit(1);
            }         
            try
            {

                foreach (var item in ApplicationData.Current.LocalSettings.Values)
                {
                    Log($"Setting: {item.Key} : {item.Value}","diag");
                }

                await BootBackend();

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
            Log(ex, true);
            __FlushLog__();
            KillBackend();
            Thread.Sleep(100);
            Environment.FailFast(ex.Message, ex);
            Environment.Exit(1);
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
            Application.Current.UnhandledException += Current_UnhandledException;


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

        private void Current_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Log(e.Exception,true);
            __FlushLog__();
            try
            {
                //backendProcess.Kill();
            }
            catch (Exception) { }
            Thread.Sleep(100);
            Environment.FailFast(e.Message, e.Exception);
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
