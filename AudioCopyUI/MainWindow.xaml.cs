/*
 * Copyright (C) 2025 Your Name
 * Licensed under GPLv2. See LICENSE for details.
 */

using ABI.System;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WinRT.Interop;
using Exception = System.Exception;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AudioCopyUI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public partial class MainWindow : Window
    {
        AppWindow m_AppWindow;
        private bool loaded = false;

        public static XamlRoot xamlRoot { get; private set; }

        public static InfoBar pairBar { get; set; }

        public static DispatcherQueue dispatcher { get; private set; }

        public static bool CloseWindow { get; set; }

        public MainWindow()
        {

            this.InitializeComponent();

            pairBar = PairBar;


            SystemBackdrop = new Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop();

            m_AppWindow = GetAppWindowForCurrentWindow();
            m_AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
            // Check to see if customization is supported.
            // Currently only supported on Windows 11.
            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                BackButton.Visibility = Visibility.Collapsed;
                var titleBar = m_AppWindow.TitleBar;
                titleBar.ExtendsContentIntoTitleBar = true;
                AppTitleBar.Loaded += AppTitleBar_Loaded;
                AppTitleBar.SizeChanged += AppTitleBar_SizeChanged;

                BackButton.Click += OnBackClicked;
                //BackButton.Visibility = Visibility.Visible;
                TitleTextBlock.Text = "AudioCopy";
            }
            else
            {
                // Title bar customization using these APIs is currently
                // supported only on Windows 11. In other cases, hide
                // the custom title bar element.
                AppTitleBar.Visibility = Visibility.Collapsed;
                // TODO Show alternative UI for any functionality in
                // the title bar, such as the back button, if used
            }

            new Thread(async () =>
            {
                Stopwatch sw = Stopwatch.StartNew();
                while (!loaded)
                {


                    this.DispatcherQueue.TryEnqueue(
                                        DispatcherQueuePriority.Normal,
                                        () =>
                                        {
                                            if (bool.Parse(SettingUtility.GetOrAddSettings("SkipSplash", "False")))
                                            {
                                                TitleTextBlock.Text = $"AudioCopy - {___PublicBuffer___}";
                                            }
                                            else
                                            {
                                                logsBox.Text = ___PublicBuffer___;
                                                if (sw.Elapsed.TotalSeconds > 10 && LocateDropdown.Visibility != Visibility.Visible) skipButton.Visibility = Visibility.Visible;
                                            }
                                        }
                                    );
                    await Task.Delay(10);

                }


                this.DispatcherQueue.TryEnqueue(
                                        DispatcherQueuePriority.Normal,
                                        () =>
                                        {
                                            TitleTextBlock.Text = "AudioCopy";
                                        }
                                        );

                


            }).Start();

        }

        private void PageFrame_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            //MainWindow.Current.AppWindow.Resize(new((int)e.PreviousSize.Width, (int)e.PreviousSize.Height));
        }

        private void MainNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
#if DEBUG
            Stopwatch sw = Stopwatch.StartNew();
#endif
            if (args.IsSettingsSelected)
            {
                PageFrame.Navigate(typeof(SettingPage));
#if DEBUG
                sw.Stop();
                Log($"It takes {sw.Elapsed.TotalMilliseconds}ms to navigate to SettingPage.","diag");
#endif
                return;
            }

            if (args.SelectedItem is NavigationViewItem selectedItem)
            {

                string pageTag = selectedItem.Tag.ToString();
                switch (pageTag)
                {

                    case "TransferPage":
                        PageFrame.Navigate(typeof(TransferPage));
                        break;
                    case "ReceivePage":
                        PageFrame.Navigate(typeof(ReceivePage));
                        break;
                    case "PairPage":
                        PageFrame.Navigate(typeof(PairingPageV2));
                        break;


                }
#if DEBUG
                sw.Stop();
                Log($"It takes {sw.Elapsed.TotalMilliseconds}ms to navigate to {pageTag}", "diag");
#endif

            }
        }



        public void Resize(int width, int height) => m_AppWindow.Resize(new(width, height));

        public AppWindow Window => m_AppWindow;

        public Button BackButton => AppTitleBarBackButton;

        public bool isTemporarilySetPort = false;

        private Thread BackendThread;

        private async void AppTitleBar_Loaded(object sender, RoutedEventArgs e)
        {
            ___PublicStackOn___ = true;
            xamlRoot = AppTitleBar.XamlRoot;
            dispatcher = DispatcherQueue.GetForCurrentThread(); 
            if (bool.Parse(SettingUtility.GetOrAddSettings("SkipSplash", "False")))
            {
                MainNavigationView.Visibility = Visibility.Visible;

                PageFrame.Navigate(typeof(ReceivePage), null, new DrillInNavigationTransitionInfo());
                splashPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                PageFrame.Navigate(typeof(SplashScreen), null, new SuppressNavigationTransitionInfo());

            }

            if (SettingUtility.GetOrAddSettings("Language", "default") == "default")
            {
                foreach (var item in Localizer.locate)
                {
                    var i = new MenuFlyoutItem { Text = item };
                    i.Click += LangChanged;
                    OptionsFlyout.Items.Add(i);

                }
                logsBox.Visibility = Visibility.Collapsed;
                LocateDropdown.Visibility = Visibility.Visible;
                OptionsFlyout.ShowAt(LocateDropdown);
                return;
            }
            try
            {
                try
                {
                    
                    await Program.UpgradeBackend(bool.Parse(SettingUtility.GetOrAddSettings("ForceUpgradeBackend", "False")));
                    SettingUtility.SetSettings("ForceUpgradeBackend", "False");
                    //await Program.BootBackend();
                    ___PublicStackOn___ = false;
                    await Program.PostInit();
                    loaded = true;

                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("Failed to bind to address"))
                    {
                        try
                        {
                            await Program.BootBackend(Random.Shared.Next(1024, 65535));
                            isTemporarilySetPort = true;
                            ___PublicStackOn___ = false;
                            await Program.PostInit();
                        }
                        catch
                        {
                            throw;
                        }

                    }
                    else throw;

                }
            }
            catch (Exception ex)
            {
#if DEBUG
                throw;
#endif
                if (await LogAndDialogue(ex, localize("Init_Desc"), localize("Init_TryReboot"), localize("Init_ContinueBoot"), this, AppTitleBar.XamlRoot,localize("Init_TryReset")))
                {
                    Program.ExitApp(true);
                }
            }

            finally
            {           
                ___PublicStackOn___ = false;

                if (!bool.Parse(SettingUtility.GetOrAddSettings("SkipSplash", "False")))
                {
                    MainNavigationView.Visibility = Visibility.Visible;

                    PageFrame.Navigate(typeof(ReceivePage), null, new DrillInNavigationTransitionInfo());
                    
                    splashPanel.Visibility = Visibility.Collapsed;
                }
                TitleTextBlock.Text = "AudioCopy";
                SetTitleBar(AppTitleBar);
                BackButton.Visibility = Visibility.Visible;
                if (AppWindowTitleBar.IsCustomizationSupported())
                {
                    SetDragRegionForCustomTitleBar(m_AppWindow);
                }
            }
            
        }

        private async void Window_Closed(object sender, WindowEventArgs args)
        {
            if (!Program.AppRunning) return;
            var NoKeepCloneRun = bool.Parse(SettingUtility.GetOrAddSettings("NoKeepCloneRun", "False"));
            if (Program.ExitHandled)
            {
                args.Handled = false;
                return;
            }
            
            args.Handled = true;
            Log("Closing...");
            string closePref = SettingUtility.GetOrAddSettings("CloseAction", "null");
            Program.ExitHandled = closePref != "null";
            if (closePref == "null")
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = localize("Info"),
                    Content = localize("ExitOptions"), 
                    PrimaryButtonText = localize("ExitOption1"), 
                    SecondaryButtonText = localize("ExitOption2"), 
                    CloseButtonText = localize("Cancel"), 
                    XamlRoot = xamlRoot
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
                if(NoKeepCloneRun)
                {
                    if (await ShowDialogue(localize("Info"), localize("/Setting/AudioQuality_RebootRequired"), localize("Accept"), localize("Cancel"), xamlRoot))
                    {
                        await AudioCloneHelper.Kill();
                    }
                    else
                    {
                        Program.ExitHandled = false;
                        return;
                    }
                }
                Program.ExitApp();
            }
            else if (closePref == "MinimizeToTray")
            {
                Program.CloseGUI();
                args.Handled = true;
            }
        }

        private void OnBackClicked(object sender, RoutedEventArgs e)
        {
            if (PageFrame.CanGoBack)
            {
                PageFrame.GoBack();
            }
        }

        private void AppTitleBar_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (AppWindowTitleBar.IsCustomizationSupported()
                && m_AppWindow.TitleBar.ExtendsContentIntoTitleBar)
            {
                // Update drag region if the size of the title bar changes.
                SetDragRegionForCustomTitleBar(m_AppWindow);
            }
        }

        private AppWindow GetAppWindowForCurrentWindow()
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId wndId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            return AppWindow.GetFromWindowId(wndId);
        }

        private void SetDragRegionForCustomTitleBar(AppWindow appWindow)
        {
            if (AppWindowTitleBar.IsCustomizationSupported()
                && appWindow.TitleBar.ExtendsContentIntoTitleBar)
            {
                double scaleAdjustment = GetScaleAdjustment();

                RightPaddingColumn.Width = new GridLength(appWindow.TitleBar.RightInset / scaleAdjustment);
                LeftPaddingColumn.Width = new GridLength(appWindow.TitleBar.LeftInset / scaleAdjustment);

                List<Windows.Graphics.RectInt32> dragRectsList = new();

                Windows.Graphics.RectInt32 dragRectL;
                dragRectL.X = (int)((LeftPaddingColumn.ActualWidth + IconColumn.ActualWidth) * scaleAdjustment);
                dragRectL.Y = 0;
                dragRectL.Height = (int)((AppTitleBar.ActualHeight) * scaleAdjustment);
                dragRectL.Width = (int)((TitleColumn.ActualWidth
                                        + DragColumn.ActualWidth) * scaleAdjustment);
                dragRectsList.Add(dragRectL);

                Windows.Graphics.RectInt32[] dragRects = dragRectsList.ToArray();
                appWindow.TitleBar.SetDragRectangles(dragRects);
            }
        }

        [DllImport("Shcore.dll", SetLastError = true)]
        internal static extern int GetDpiForMonitor(IntPtr hmonitor, Monitor_DPI_Type dpiType, out uint dpiX, out uint dpiY);

        internal enum Monitor_DPI_Type : int
        {
            MDT_Effective_DPI = 0,
            MDT_Angular_DPI = 1,
            MDT_Raw_DPI = 2,
            MDT_Default = MDT_Effective_DPI
        }

        private double GetScaleAdjustment()
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            DisplayArea displayArea = DisplayArea.GetFromWindowId(wndId, DisplayAreaFallback.Primary);
            IntPtr hMonitor = Win32Interop.GetMonitorFromDisplayId(displayArea.DisplayId);

            // Get DPI.
            int result = GetDpiForMonitor(hMonitor, Monitor_DPI_Type.MDT_Default, out uint dpiX, out uint _);
            if (result != 0)
            {
                throw new Exception("Could not get DPI for monitor.");
            }

            uint scaleFactorPercent = (uint)(((long)dpiX * 100 + (96 >> 1)) / 96);
            return scaleFactorPercent / 100.0;
        }



        private async void skipButton_Click(object sender, RoutedEventArgs e)
        {
            PageFrame.Navigate(typeof(ReceivePage));
            splashPanel.Visibility = Visibility.Collapsed;
            MainNavigationView.Visibility = Visibility.Visible;
        }

        private void PageFrame_Navigated(object sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            if(e.Parameter is ContentDialog d)
            {
                MainNavigationView.IsPaneVisible = false;
            }
            else
            {
                MainNavigationView.IsPaneVisible = true;    
            }
        }

        private async void LangChanged(object sender, RoutedEventArgs e)
        {
            var text = (e.OriginalSource as MenuFlyoutItem).Text;

            var id = Localizer.locateId[Array.IndexOf(Localizer.locate, text)];

            await Program.ChangeLang(id);

        }

        private void CancelPairButton_Click(object sender, RoutedEventArgs e)
        {
            //PairBar.IsClosable = true;
            AudioCopyUI_MiddleWare.BackendHelper.CancelPair();
            PairBar.IsOpen = false;

        }

        private void PairBar_Closing(InfoBar sender, InfoBarClosingEventArgs args)
        {
            CancelPairButton.IsEnabled = true;
            //CancelPairButton.Content = localize("Cancel");
        }
    }

}
