/*
*	 File: GlobalUtility.cs
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace AudioCopyUI_ReceiverOnly
{
    class GlobalUtility
    {
        public static string GlobalDataPath = "";

        public static string LocalStateFolder => ApplicationData.Current.LocalFolder.Path;


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
                CloseButtonText = close ?? "",
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
        static ConcurrentQueue<string> buffer = new ConcurrentQueue<string>();

        public static string ___LogPath___ => filePath;
        public static void _LoggerInit_(string path)
        {
            running = true;
            filePath = Path.Combine(path, $"{DateTime.Now:yyyy-MM-dd-hh-mm-ss}.log");
            File.WriteAllText(filePath, $"Logger start at:{DateTime.Now}\r\n");
            writer = new Thread(WriteLog);
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
                writer = new Thread(WriteLog);
                writer.Start();
            }
        }

        public static void Log(string msg) => Log(msg, "info"); //fix the vs auto completion

        public static void Log(Exception e) => Log(e, false);

        public static void Log(Exception e, bool isCritical) => Log($"{(isCritical ? "A critical " : "")}{e.GetType().Name} error: {e.Message} {e.StackTrace}", isCritical ? "Critical" : "error");

        public static void Log(Exception e, string message = "", object sender = null) => Log($"{sender?.GetType().Name} report a {e.GetType().Name} error when trying to {message} \r\n error message: {e.Message} {e.StackTrace}", "error");

        public static async Task<bool> LogAndDialogue(Exception e, string whatDoing = "", string priButtonText = "好的", string subButtonText = null, Page element = null)
        {
            Log(e, whatDoing, element);
            return await GlobalUtility.ShowDialogue("错误", $"{whatDoing}时发生了错误：{e.Message}", priButtonText ?? "好的", subButtonText, element);
        }

        public static void Log(string msg, string level = "info")
        {
#if DEBUG
            Debug.Write($"[{level} @ {DateTime.Now}] {(msg.Contains('\r') ? "mutil-line log:\r\n" : "")}{msg}{(msg.Contains('\r') ? "\r\nmutil-line log ended." : "")}\r\n");
#endif
            buffer.Enqueue($"[{level} @ {DateTime.Now}] {(msg.Contains('\r') ? "mutil-line log:\r\n" : "")}{msg}{(msg.Contains('\r') ? "\r\nmutil-line log ended." : "")}\r\n");



        }

#if DEBUG
        public static void LogDebug(string msg, string level = "info") => Log(msg, level);
#else
        public static void LogDebug(string msg, string level = "info") { }
#endif

    }


    public class AlgorithmServices //懒，干脆放一块得了
    {
        private const string StringTable = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";

        //[DebuggerNonUserCode()]
        public static string MakeRandString(int length) => string.Concat(Enumerable.Repeat(StringTable, length / StringTable.Length + 5)).OrderBy(x => Guid.NewGuid()).Take(length).Select(x => (char)x).Aggregate("", (x, y) => x + y);

    }
}
