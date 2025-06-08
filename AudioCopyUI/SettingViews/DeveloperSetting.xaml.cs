using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AudioCopyUI.SettingViews
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class DeveloperSetting : Page
    {
        public DeveloperSetting()
        {
            InitializeComponent();
            LoadSettings();

        }

        private Dictionary<string, string> settings = new();

        private void LoadSettings()
        {
            settings = new(ApplicationData.Current.LocalSettings.Values.ToDictionary()
                .Select(x => new KeyValuePair<string, string>(x.Key, x.Value.ToString()))
                .OrderBy(x => x.Key));
            SettingsComboBox.ItemsSource = settings.ToList();
            if (settings.Count > 0)
                SettingsComboBox.SelectedIndex = 0;
        }

        private void SettingsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SettingsComboBox.SelectedItem is KeyValuePair<string, string> selected)
            {
                SettingValueTextBox.Text = selected.Value;
            }
        }

        private void ModifySetting_Click(object sender, RoutedEventArgs e)
        {
            if (SettingsComboBox.SelectedItem is KeyValuePair<string, string> selected)
            {
                string key = selected.Key;
                string newValue = SettingValueTextBox.Text;
                SettingUtility.SetSettings(key, newValue);
                LoadSettings();
                SettingsComboBox.SelectedValue = key;
            }
        }

        private void AddSetting_Click(object sender, RoutedEventArgs e)
        {
            string newKey = NewSettingKeyTextBox.Text.Trim();
            string newValue = NewSettingValueTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(newKey) && !settings.ContainsKey(newKey))
            {
                SettingUtility.SetSettings(newKey, newValue);
                LoadSettings();
                SettingsComboBox.SelectedValue = newKey;
            }
        }

        private void DeleteSetting_Click(object sender, RoutedEventArgs e)
        {
            if (SettingsComboBox.SelectedItem is KeyValuePair<string, string> selected)
            {
                var kvp = ApplicationData.Current.LocalSettings.Values;
                var dict = kvp.ToDictionary();
                ApplicationData.Current.LocalSettings.Values.Clear();
                dict.Remove(selected.Key);
                foreach (var item in dict)
                {
                    ApplicationData.Current.LocalSettings.Values[item.Key] = item.Value;
                }

            }
        }

        private void OptionsChanged(object sender, RoutedEventArgs e)
        {
            SettingUtility.SetSettings("V1PairCompatibility", (V1PairingCheckBox.IsChecked ?? false).ToString());  
        }

        private async void OverrideCloneAddress_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(OverrideCloneAddress.Text) || string.IsNullOrWhiteSpace(OverrideCloneToken.Text))
            {
                SettingUtility.SetSettings("OverrideAudioClonePort", "null");
                SettingUtility.SetSettings("OverrideAudioCloneToken", "null");
                SettingUtility.SetSettings("OverrideAudioCloneOptions", "False");
                return;
            }
            if (uint.TryParse(OverrideCloneAddress.Text, out uint v) && v < 65535 && v != 23456 )
            {
                SettingUtility.SetSettings("OverrideAudioClonePort", OverrideCloneAddress.Text);
            }
            else
            {
                await ShowDialogue(localize("Error"), "端口不合法", localize("Accept"), null, this);

            }

            SettingUtility.SetSettings("OverrideAudioCloneToken", OverrideCloneToken.Text);

        }
    }
}
