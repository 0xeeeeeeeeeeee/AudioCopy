
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml.Controls;

namespace AudioCopyUI_ReceiverOnly
{
    class GlobalUtility
    {


        public static string GlobalDataPath = "";

        static ConcurrentDictionary<Page, object> locker = new ConcurrentDictionary<Page, object>();
        static ConcurrentDictionary<Page, bool> isShowing = new ConcurrentDictionary<Page, bool>();


        public static async Task<bool> ShowDialogue(string title, string content, string pri, string close, Page element)
        {
            ContentDialog confirmDialog = new ContentDialog
            {
                XamlRoot = element.Content.XamlRoot, // 关键：设置XamlRoot
                Title = title,
                Content = content,
                PrimaryButtonText = pri,
                CloseButtonText = close,
                DefaultButton = ContentDialogButton.Primary
            };
            while (true)
            {
                if (isShowing.TryGetValue(element, out var key))
                {
                    if (!key) break;
                }
                else break;//not exist
            }
            isShowing.AddOrUpdate(element, (garbage) => true, (garbage, garbage1) => true);
            var result = (await confirmDialog.ShowAsync()) == ContentDialogResult.Primary;
            isShowing[element] = false;
            return result;



        }


        public static bool ShowDialogueSync(string title, string content, string pri, string close, Page element)
        {
            lock (locker.GetOrAdd(element, new object()))
            {
                ContentDialog confirmDialog = new ContentDialog
                {
                    XamlRoot = element.Content.XamlRoot, // 关键：设置XamlRoot
                    Title = title,
                    Content = content,
                    PrimaryButtonText = pri,
                    CloseButtonText = close,
                    DefaultButton = ContentDialogButton.Primary
                };

                return confirmDialog.ShowAsync().GetAwaiter().GetResult() == ContentDialogResult.Primary;
            }

        }

    }



    class SettingUtility
    {
        private static ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        public static string GetSetting(string key) => localSettings.Values[key] as string;
        public static void SetSettingValueOnExist(string key, ref string target, string defaultValue = "")
        {
            if (localSettings.Values.TryGetValue(key, out var value))
            {
                target = value as string;
            }
            else
            {
                target = defaultValue;
            }
        }

        public static void SetSettingValueOnExist(string key, Action<string> setter, string defaultValue = default)
        {
            if (localSettings.Values.TryGetValue(key, out var value))
            {
                setter(value as string);
            }
            else
            {
                setter(defaultValue);
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
                return value as string;
            }
            else
            {
                SetSettings(key, defaultValue);
                return defaultValue;
            }
        }
    }

    class Logger
    {

        static string filePath = "";
        static Stack<string> buffer = new Stack<string>();
        public static void _LoggerInit_(string path)
        {

            filePath = Path.Combine(path, $"{DateTime.Now:yyyy-MM-dd-hh-mm-ss}.log");
            File.WriteAllText(filePath, $"Logger start at:{DateTime.Now}\r\n");
            writer = new Thread(WriteLog);
            writer.Start();
        }

        static void WriteLog()
        {
            while (true)
            {
                if (buffer.TryPop(out var str))
                {
                    File.AppendAllText(filePath, str);
                }
            }
        }

        static Thread writer;

        public static void Log(string msg) => Log(msg, "info");//fix the vs auto completion

        public static void Log(string msg, string level = "info")
        {
#if DEBUG
            Debug.Write($"[{level} @ {DateTime.Now}] {msg}\r\n");
#endif
            if (level == "Critical")//crashed
            {
                Debug.Write($"[{level} @ {DateTime.Now}] {msg}\r\n");
                buffer.Push($"[{level} @ {DateTime.Now}] {msg}\r\n");
            }
            else
            {
                buffer.Push($"[{level} @ {DateTime.Now}] {msg}\r\n");

            }


        }

#if DEBUG
        public static void LogDebug(string msg, string level = "info") => Log(msg, level);
#else
        public static void LogDebug(string msg, string level = "info") { }
#endif

    }
}
