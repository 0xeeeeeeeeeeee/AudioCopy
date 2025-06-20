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
                if (SettingUtility.GetOrAddSettings("Language","default") != "default")
                {
                    var locate = SettingUtility.GetOrAddSettings("Language", Windows.Globalization.Language.CurrentInputMethodLanguageTag);
                    Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = locate;
                }
                else
                {
                    //var inputTag = Windows.Globalization.Language.CurrentInputMethodLanguageTag;
                    //string? selectedLang = Localizer.Match(inputTag);
                    Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = "en-US";
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
