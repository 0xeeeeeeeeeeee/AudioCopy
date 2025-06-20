/*
 * Copyright (C) 2025 Your Name
 * Licensed under GPLv2. See LICENSE for details.
 */

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using NAudio.SoundFont;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.ApplicationModel;
using Windows.Storage;


namespace AudioCopyUI
{
    class GlobalUtility
    {

        public static string VersionString = $"{Package.Current.Id.Version.Major}.{Package.Current.Id.Version.Minor}.{Package.Current.Id.Version.Build}.{Package.Current.Id.Version.Revision}";

        public static string LocalStateFolder => ApplicationData.Current.LocalFolder.Path;

        public static ContentDialog _____SplashDialog_____;

        static object locker = new();
        //static ConcurrentDictionary<object, bool> isShowing = new();

        public static async Task<bool> ShowDialogue(string title, string content, string priButtonText, string? subButtonText, Page element)
            => await ShowDialogue(title, content, priButtonText, subButtonText, element.Content.XamlRoot);

		public static async Task<bool> ShowDialogue(string title, string content, string priButtonText, string? subButtonText, XamlRoot element)
        {
            ContentDialog confirmDialog = new ContentDialog
            {
                XamlRoot = element,
                Title = title,
                Content = content,
                PrimaryButtonText = priButtonText,
                CloseButtonText = subButtonText,
                DefaultButton = ContentDialogButton.Primary
            };
            return await ShowDialogue(confirmDialog, element, ContentDialogResult.Primary);
        }

        public static async Task<bool> ShowDialogue(ContentDialog confirmDialog, object element, ContentDialogResult resultButton)
        {
            try
            {
                lock (locker) { }
                var result = (await confirmDialog.ShowAsync()) == resultButton;
                lock (locker) { }
                return result;

            }
            catch (Exception ex)
            {
                Log("Trying to show many dialog at one time.","error");
                await Task.Delay(Random.Shared.Next(1000, 5000));
                return await ShowDialogue(confirmDialog, element, resultButton);
            }




        }

        [DebuggerNonUserCode()]
        public static string localize(string key)
        {
            if (App.loader is null) return $"Localization not inited, key:{key}";
            var str = App.loader.GetString(key);
            //if(str is null) str = App.loader.GetString(key+".Text");.Replace("[line]", Environment.NewLine
            return string.IsNullOrWhiteSpace(str) ? $"Localization resource not found:{key}" : str.Replace("[line]", Environment.NewLine);
        }

        public static string localize(string key, params string[]? args) => string.Format(localize(key),args);


        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        public static bool IsWindowActive()
        {
            IntPtr foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero)
                return false;

            GetWindowThreadProcessId(foregroundWindow, out uint processId);
            uint currentProcessId = (uint)Process.GetCurrentProcess().Id;

            return processId == currentProcessId;
        }


    }


    [DebuggerNonUserCode()]
    class SettingUtility
    {
        private static ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

        public static string HostToken => GetOrAddSettings("hostToken", "abcd");

        public static bool OldBackend => false;

        public static string GetSetting(string key) => localSettings.Values[key] as string;
        public static void TryGetSettings(string key, out string target, string defaultValue = "")
        {
            if (localSettings.Values.TryGetValue(key, out var value))
            {
                target = value as string ?? throw new InvalidOperationException();
            }
            else
            {
                target = defaultValue;
            }
        }

        public static void SetSettings(string key, string value)
        {
            localSettings.Values[key] = value;
        }

        public static string GetOrAddSettings(string key, string defaultValue = "")
        {
            if (localSettings.Values.TryGetValue(key, out var value))
            {
                return value as string ?? throw new InvalidOperationException();
            }
            else
            {
                SetSettings(key, defaultValue);
                return defaultValue;
            }
        }

        public static bool Exists(string key) => localSettings.Values.ContainsKey(key);
        
    }


    public class Localizer
    {
        readonly public static Dictionary<string, string[]> matches = new Dictionary<string, string[]>
        {
            {
                "zh-Hant", new []
                {
                    "zh-Hant", "zh-hk", "zh-mo", "zh-tw", "zh-hant-hk", "zh-hant-mo", "zh-hant-tw"
                }
            },
            {
                "fr", new []
                {
                    "fr", "fr-be", "fr-ca", "fr-ch", "fr-fr", "fr-lu", "fr-015", "fr-cd", "fr-ci", "fr-cm", "fr-ht", "fr-ma", "fr-mc", "fr-ml", "fr-re", "frc-latn", "frp-latn", "fr-155", "fr-029", "fr-021", "fr-011"
                }
            },
            {
                "ru", new []
                {
                    "ru", "ru-ru"
                }
            },
            {
                "es", new []
                {
                    "es", "es-cl", "es-co", "es-es", "es-mx", "es-ar", "es-bo", "es-cr", "es-do", "es-ec", "es-gt", "es-hn", "es-ni", "es-pa", "es-pe", "es-pr", "es-py", "es-sv", "es-us", "es-uy", "es-ve", "es-019", "es-419"
                }
            },
            {
                "ar", new []
                {
                    "ar", "ar-sa", "ar-ae", "ar-bh", "ar-dz", "ar-eg", "ar-iq", "ar-jo", "ar-kw", "ar-lb", "ar-ly", "ar-ma", "ar-om", "ar-qa", "ar-sy", "ar-tn", "ar-ye"
                }
            },
            {
                "ja", new []
                {
                    "ja", "ja-jp"
                }
            }
        };

        readonly public static string[] locate = {
            "العربية/Arabic", "Deutsch/German", "English (United Kingdom)/English (United Kingdom)", "English (United States)/English (United States)",
            "Español/Spanish", "Français/French", "Italiano/Italian", "日本語/Japanese", "한국어/Korean", "Polski/Polish",
            "Português (Brasil)/Portuguese (Brazil)", "Русский/Russian", "Türkçe/Turkish", "简体中文/Simplified Chinese", "繁體中文/Traditional Chinese" };

        readonly public static string[] locateId =
            { "ar", "de", "en-GB", "en-US", "es", "fr", "it",
            "ja", "ko", "pl", "pt-BR", "ru", "tr", "zh-CN", "zh-Hant" };
        public static string current => Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride;




        public static string Match(string source)
        {
            if (source == "default")
            {
                try
                {
                    return Match(Windows.Globalization.Language.CurrentInputMethodLanguageTag);
                }
                catch
                {
                    return "en-US";
                }
            }
            var mapped = matches.TakeWhile((l) => l.Value.Contains(source));
            if (mapped.Count() > 0) return mapped.ToArray()[0].Key;
            if (locateId.Contains(source)) return source;
            return "en-US";
        }

        
    }
}
