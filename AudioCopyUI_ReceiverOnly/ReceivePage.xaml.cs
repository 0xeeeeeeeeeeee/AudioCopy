using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AudioCopyUI_ReceiverOnly
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ReceivePage : Page
    {
        private SystemMediaTransportControls smtc;

        private const string StringTable = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890"; //have to do this because of too many functions are unavailable in .net framework(universal windows use it)
        public static string MakeRandString(int length) => string.Concat(Enumerable.Repeat(StringTable, length / StringTable.Length + 5)).OrderBy(x => Guid.NewGuid()).Take(length).Select(x => (char)x).Aggregate("", (x, y) => x + y);


        public ReceivePage()
        {
            this.InitializeComponent();
            udidBox.Text = SettingUtility.GetOrAddSettings("udid", MakeRandString(128));
        }

        private async void Button_Click(object sender, object e)
        {
            var mediaPlayer = new MediaPlayer();
            mediaPlayer.Source = MediaSource.CreateFromUri(new Uri($"http://127.0.0.1:{SettingUtility.GetOrAddSettings("defaultPort", "23456")}/api/audio/wav?token={SettingUtility.GetOrAddSettings("udid", MakeRandString(128))}"));

            PlayerElement.SetMediaPlayer(mediaPlayer);
            mediaPlayer.Play();

            await Task.Delay(1500); //wait for update

            smtc = mediaPlayer.SystemMediaTransportControls;
            smtc.IsEnabled = true;
            smtc.IsPauseEnabled = false;
            smtc.ButtonPressed += Smtc_ButtonPressed;

            mediaPlayer.MediaEnded += Button_Click;//replay

            var updater = smtc.DisplayUpdater;
            updater.Type = MediaPlaybackType.Music;
            updater.MusicProperties.Title = "Audio from AudioCopy";
            updater.Update();
        }

        private void Smtc_ButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
        {
            var updater = smtc.DisplayUpdater;
            updater.Type = MediaPlaybackType.Music;
            updater.MusicProperties.Title = "Audio from AudioCopy";
            updater.Update();
        }
    }
}
