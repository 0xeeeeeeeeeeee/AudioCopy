/*
 * Copyright (C) 2025 Your Name
 * Licensed under GPLv2. See LICENSE for details.
 */

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.Storage;

namespace AudioCopyUI
{
    class GlobalUtility
    {

        public static string LocalStateFolder => ApplicationData.Current.LocalFolder.Path;

        public static ContentDialog _____SplashDialog_____;

        static ConcurrentDictionary<Page, object> locker = new();
        static ConcurrentDictionary<object, bool> isShowing = new();

        public static async Task<bool> ShowDialogue(string title, string content, string priButtonText, string? subButtonText, Page element)
            => await ___ShowDialogue__WithRoot___(title, content, priButtonText, subButtonText, element.Content.XamlRoot);


        public static async Task<bool> ___ShowDialogue__WithRoot___(string title, string content, string priButtonText, string? subButtonText, XamlRoot element)
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


        private static async Task<bool> ShowDialogue(ContentDialog confirmDialog, object element, ContentDialogResult resultButton)
        {
            try
            {
                Task t = new(() =>
                {
                    while (true)
                    {
                        try
                        {


                            if (isShowing.TryGetValue(element, out var key))
                            {
                                if (!key) break;
                            }
                            else break;//not exist
                        }
                        catch { return; }
                    }
                });
                t.Start();
                await t;
                isShowing.AddOrUpdate(element, (_) => true, (_, _) => true);
                var result = (await confirmDialog.ShowAsync()) == resultButton;
                isShowing[element] = false;
                return result;
            }
            catch (Exception ex)
            {
                Log("Trying to show many dialog at one time.","error");
                await Task.Delay(Random.Shared.Next(1000, 5000));
                return await ShowDialogue(confirmDialog, element, resultButton);
            }




        }


        public static string localize(string key)
        {
            if (App.loader is null) return $"Localization not inited, key:{key}";
            var str = App.loader.GetString(key);
            //if(str is null) str = App.loader.GetString(key+".Text");.Replace("[line]", Environment.NewLine
            return string.IsNullOrWhiteSpace(str) ? $"Localization resource not found:{key}" : str.Replace("[line]", Environment.NewLine);
        }

       

    }



    class SettingUtility
    {
        private static ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

        public static string HostToken => GetOrAddSettings("hostToken", "abcd");

        public static bool OldBackend => bool.Parse(SettingUtility.GetOrAddSettings("OldBackend", "False"));

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

    class Logger
    {
        static string filePath = "";
        static ConcurrentQueue<string> buffer = new(), publicBuffer = new();

        public static string ___LogPath___ => filePath;
        public static bool ___PublicStackOn___ = false;
        public static void _LoggerInit_(string path)
        {    
            running = true;
            filePath = Path.Combine(path, $"{DateTime.Now:yyyy-MM-dd-hh-mm-ss}.log");
            File.WriteAllText(filePath, $"Logger start at:{DateTime.Now}\r\n");
            writer = new(WriteLog);
            writer.Start();
        }

        static void WriteLog()
        {
            while (running)
            {
                if (buffer.TryDequeue(out var str))
                {
                    File.AppendAllText(filePath, str);
                }
            }
        }

        static Thread writer;
        private static bool running;
        public static string ___PublicBuffer___ = "";

        public static void __FlushLog__(bool restart = false)
        {
            running = false;
            foreach (var item in buffer)
            {
                File.AppendAllText(filePath, item);
            }
            if (restart)
            {
                running = true;
                writer = new(WriteLog);
                writer.Start();
            }
        }


        public static void Log(string msg) => Log(msg, "info"); //fix the vs auto completion

        public static void Log(Exception e) => Log(e,false);

        public static void Log(Exception e, bool isCritical) => Log($"{(isCritical ? "A critical " : "")}{e.GetType().Name} error: {e.Message} {e.StackTrace}",isCritical ? "Critical" : "error");

        public static void Log(Exception e, string message = "", object? sender = null) => Log($"{sender?.GetType().Name} report a {e.GetType().Name} error when trying to {message} \r\n error message: {e.Message} {e.StackTrace}{(e.Data.Contains("RemoteStackTrace") ? e.Data["RemoteStackTrace"] : "")}", "error");

        public static async Task<bool> LogAndDialogue(Exception e, string whatDoing = "", string? priButtonText = null, string? subButtonText = null, Page element = null,string append = "")
            => await LogAndDialogue(e, whatDoing, priButtonText, subButtonText, element, element.XamlRoot);

        public static async Task<bool> LogAndDialogue(Exception e, string whatDoing = "", string? priButtonText = null, string? subButtonText = null, object? obj = null, XamlRoot? root = null,string append = "")
        {
            Log(e,whatDoing,obj);
            return await ___ShowDialogue__WithRoot___(localize("Error"), string.Format(localize("LogAndDialogue_Content"), whatDoing, e.GetType().Name, e.Message) + (string.IsNullOrWhiteSpace(append) ? "" : "\r\n" + append), priButtonText ?? localize("Accept"), subButtonText, root);
        }

        public static void Log(string msg, string level = "info")
        {
#if DEBUG
            Debug.Write($"[{level} @ {DateTime.Now}] {(msg.Contains('\r') ? "mutil-line log:\r\n" : "")}{msg}{(msg.Contains('\r') ? "\r\nmutil-line log ended." : "")}\r\n");
#endif
            buffer.Enqueue($"[{level} @ {DateTime.Now}] {(msg.Contains('\r')? "mutil-line log:\r\n" : "")}{msg}{(msg.Contains('\r') ? "\r\nmutil-line log ended." : "")}\r\n");

            if (___PublicStackOn___  && level == "showToGUI") ___PublicBuffer___ = msg;


        }

#if DEBUG
        public static void LogDebug(string msg, string level = "info") => Log(msg, level);
#else
        public static void LogDebug(string msg, string level = "info") { }
#endif

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
            "Default", "العربية/Arabic", "Deutsch/German", "English (United Kingdom)/English (United Kingdom)", "English (United States)/English (United States)",
            "Español/Spanish", "Français/French", "Italiano/Italian", "日本語/Japanese", "한국어/Korean", "Polski/Polish",
            "Português (Brasil)/Portuguese (Brazil)", "Русский/Russian", "Türkçe/Turkish", "简体中文/Simplified Chinese", "繁體中文/Traditional Chinese" };

        readonly public static string[] locateId =
            { "default", "ar", "de", "en-GB", "en-US", "es", "fr", "it",
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
