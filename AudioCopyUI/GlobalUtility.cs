/*
 * Copyright (C) 2025 Your Name
 * Licensed under GPLv2. See LICENSE for details.
 */

using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace AudioCopyUI
{
    class GlobalUtility
    {


        public static string LocalStateFolder => ApplicationData.Current.LocalFolder.Path;

        static ConcurrentDictionary<Page, object> locker = new();
        static ConcurrentDictionary<Page, bool> isShowing = new();

        /// <summary>
        /// Show a dialogue with the given title, content, and button text.
        /// </summary>
        /// <param name="title"></param>
        /// <param name="content"></param>
        /// <param name="priButtonText"></param>
        /// <param name="subButtonText"></param>
        /// <param name="element"></param>
        /// <returns></returns>
        public static async Task<bool> ShowDialogue(string title, string content, string priButtonText, string? subButtonText, Page element)
        {
            try
            {
                ContentDialog confirmDialog = new ContentDialog
                {
                    XamlRoot = element.Content.XamlRoot, 
                    Title = title,
                    Content = content,
                    PrimaryButtonText = priButtonText,
                    CloseButtonText = subButtonText,
                    DefaultButton = ContentDialogButton.Primary
                };
                Task t = new(() =>
                {
                    while (true)
                    {
                        if (isShowing.TryGetValue(element, out var key))
                        {
                            if (!key) break;
                        }
                        else break;//not exist
                    }
                });
                t.Start();
                await t;
                isShowing.AddOrUpdate(element, (_) => true, (_, _) => true);
                var result = (await confirmDialog.ShowAsync()) == ContentDialogResult.Primary;
                isShowing[element] = false;
                return result;
            }
            catch (Exception ex)
            {
                Log("Trying to show many dialog at one time.","error");
                await Task.Delay(Random.Shared.Next(100, 500));
                return await ShowDialogue(title, content, priButtonText, subButtonText, element);
            }




        }


        

    }



    class SettingUtility
    {
        private static ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

        public static string HostToken => GetOrAddSettings("hostToken", "abcd");

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
        static ConcurrentQueue<string> buffer = new();

        public static string ___LogPath___ => filePath;
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

        public static void Log(Exception e,bool isCritical = false) => Log($"{(isCritical ? "A critical " : "")}{e.GetType().Name} error: {e.Message} {e.StackTrace}",isCritical ? "Critical" : "error");

        public static void Log(string msg, string level = "info")
        {
#if DEBUG
            Debug.Write($"[{level} @ {DateTime.Now}] {(msg.Contains('\r') ? "mutil-line log:\r\n" : "")}{msg}{(msg.Contains('\r') ? "mutil-line log ended." : "")}\r\n");
#endif
            buffer.Enqueue($"[{level} @ {DateTime.Now}] {(msg.Contains('\r')? "mutil-line log:\r\n" : "")}{msg}{(msg.Contains('\r') ? "mutil-line log ended." : "")}\r\n");



        }

#if DEBUG
        public static void LogDebug(string msg, string level = "info") => Log(msg, level);
#else
        public static void LogDebug(string msg, string level = "info") { }
#endif

    }
}
