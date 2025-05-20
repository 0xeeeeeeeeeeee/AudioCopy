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


using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.Foundation.Metadata;
using Windows.System.Profile;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;
using static AudioCopyUI_ReceiverOnly.GlobalUtility;
using static AudioCopyUI_ReceiverOnly.Logger;
using Path = System.IO.Path;
namespace AudioCopyUI_ReceiverOnly
{
    public static class Program
    {
        public static double globalScale;
        public static bool isPC;

        static void Main(string[] args)
        {
            if (Environment.OSVersion.Version < new Version(10, 0, 19041))
            {
                Environment.FailFast($"Your planform (version {Environment.OSVersion.Version}) is unsupported. Upgrade to Windows 10 10.0.19041 or later version to continue use.");
                ExitApp(false);
            }

            if (!Directory.Exists(System.IO.Path.Combine(LocalStateFolder, "logs"))) Directory.CreateDirectory(Path.Combine(LocalStateFolder, "logs"));
            Logger._LoggerInit_(Path.Combine(LocalStateFolder, "logs"));
            string deviceFamily = AnalyticsInfo.VersionInfo.DeviceFamily;
            double scaleFactor;
            switch (deviceFamily)
            {
                case "Windows.Xbox":
                    scaleFactor = 0.5;
                    break;
                case "Windows.Mobile":
                    scaleFactor = 1.2;
                    break;
                case "Windows.Team":
                    scaleFactor = 0.3;
                    break;
                case "Windows.Holographic":
                    scaleFactor = 0.8;
                    break;
                case "Windows.Desktop":
                default:
                    scaleFactor = 1.0;
                    break;
            }
            globalScale = scaleFactor;
            isPC = deviceFamily == "Windows.Desktop";
            Log($"Planform:{deviceFamily}");
            Localizer.___InitLocalize___();
            if (ApiInformation.IsMethodPresent("Windows.UI.Xaml.Application","Start"))
            {
                global::Windows.UI.Xaml.Application.Start((p) =>
                {
                    new App();
                });
            }
            else
            {
                Environment.FailFast("Your platfrom is unsupported.");
                Windows.UI.Xaml.Application.Current.Exit();
            }
            
        }

        public static async Task ChangeLang(string target)
        {
            SettingUtility.SetSettings("Language", target == "default" ? Windows.Globalization.Language.CurrentInputMethodLanguageTag : target);
            ExitApp(true);

        }
        public static void ExitApp(bool restart = false)
        {
            __FlushLog__();
            if (restart)
            {
                if (ApiInformation.IsMethodPresent(
                 "Windows.ApplicationModel.Core.CoreApplication",
                 "RequestRestartAsync"))
                {
                    _ = CoreApplication.RequestRestartAsync("reboot");
                }
                else
                {
                    Windows.UI.Xaml.Application.Current.Exit();
                }
            }
            else
            {
                Windows.UI.Xaml.Application.Current.Exit();
            }
        }

        internal static void Crash(Exception ex)
        {
            try
            {
                Log(ex, true);
                __FlushLog__();
            }
            finally
            {
                Environment.FailFast(ex.Message, ex);
                Windows.UI.Xaml.Application.Current.Exit();
            }


        }
    }


    /// <summary>
    /// 提供特定于应用程序的行为，以补充默认的应用程序类。
    /// </summary>
    sealed partial class App : Application
    {
        /// <summary>
        /// 初始化单一实例应用程序对象。这是执行的创作代码的第一行，
        /// 已执行，逻辑上等同于 main() 或 WinMain()。
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
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

        }

        private void Current_UnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 在应用程序由最终用户正常启动时进行调用。
        /// 将在启动应用程序以打开特定文件等情况下使用。
        /// </summary>
        /// <param name="e">有关启动请求和过程的详细信息。</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {



            Frame rootFrame = Window.Current.Content as Frame;

            // 不要在窗口已包含内容时重复应用程序初始化，
            // 只需确保窗口处于活动状态
            if (rootFrame == null)
            {
                // 创建要充当导航上下文的框架，并导航到第一页
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    //TODO: 从之前挂起的应用程序加载状态
                }

                // 将框架放在当前窗口中
                Window.Current.Content = rootFrame;
            }

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    // 当导航堆栈尚未还原时，导航到第一页，
                    // 并通过将所需信息作为导航参数传入来配置
                    // 参数
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                }
                // 确保当前窗口处于活动状态
                Window.Current.Activate();
            }

            



        }

        /// <summary>
        /// 导航到特定页失败时调用
        /// </summary>
        ///<param name="sender">导航失败的框架</param>
        ///<param name="e">有关导航失败的详细信息</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            Program.Crash(new InvalidOperationException("Failed to load Page " + e.SourcePageType.FullName));
        }

        /// <summary>
        /// 在将要挂起应用程序执行时调用。  在不知道应用程序
        /// 无需知道应用程序会被终止还是会恢复，
        /// 并让内存内容保持不变。
        /// </summary>
        /// <param name="sender">挂起的请求的源。</param>
        /// <param name="e">有关挂起请求的详细信息。</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            //TODO: 保存应用程序状态并停止任何后台活动
            deferral.Complete();
        }
    }
}
