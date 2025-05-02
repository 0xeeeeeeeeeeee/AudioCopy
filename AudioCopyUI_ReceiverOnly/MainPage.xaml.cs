using Windows.UI.Xaml.Controls;

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
            NavView.SelectedItem = NavView.MenuItems[0]; // 默认选中第一个菜单项
            PageFrame.Navigate(typeof(ReceivePage)); // 默认导航到 HomePage
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                PageFrame.Navigate(typeof(SettingPage));
                return;
            }

            if (args.SelectedItem is NavigationViewItem selectedItem)
            {

                string pageTag = selectedItem.Tag.ToString();
                switch (pageTag)
                {

                    case "ReceivePage":
                        PageFrame.Navigate(typeof(ReceivePage));
                        break;
                    case "PairPage":
                        PageFrame.Navigate(typeof(PairingPage));
                        break;


                }
            }
        }
    }
}
