using System;
using Windows.Foundation;
using Windows.System.Profile;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AudioCopyUI_ReceiverOnly
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {

            this.InitializeComponent();

            var scaleFactor = Program.globalScale;
            if (!Program.isPC)
            {
                NavView1.Visibility = Visibility.Collapsed;
                var bounds = Window.Current.Bounds;
                mainPanel.Height = bounds.Height * scaleFactor;
                mainPanel.Width = bounds.Width * scaleFactor;
            }
            else
            {
                mainView.Visibility = Visibility.Collapsed;
            }

            (Program.isPC ? NavView1 : NavView).SelectedItem = NavView.MenuItems[0];
            Navigate(typeof(ReceivePage));
        }


        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                Navigate(typeof(SettingPage));
                return;
            }

            if (args.SelectedItem is NavigationViewItem selectedItem)
            {

                string pageTag = selectedItem.Tag.ToString();
                switch (pageTag)
                {
                    case "ReceivePage":
                        Navigate(typeof(ReceivePage));
                        break;
                    case "PairPage":
                        Navigate(typeof(PairingPage));
                        break;
                }
            }
        }

        private void Navigate(Type type)
        {
            if (Program.isPC) PageFrame1.Navigate(type);
            else PageFrame.Navigate(type);
        }
    }
}
