using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AudioCopyUI_MiddleWare
{
    public static class BackendHelper
    {
        public static Action<string, string>? SetSettings;
        public static Action<string>? Log;
        public static Action<Exception,string,string>? LogEx;
        public static Action<Task>? Dispatch;
        public static Action? CancelPair;

        public static Task? BootAudioClone;

        public static Func<string, string>? localize;
        public static Func<bool>? IsWindowActive;
        public static Func<string, string, string, string?, Task<bool>>? ShowDialogueWithRoot;
        public static Func<string, object, Task<bool>>? ShowSpecialDialogue;
        public static Func<MediaInfo?>? GetCurrentMediaInfoAsync;
        public static Func<string, bool>? ExistsSetting;
        public static Func<string, string, string>? GetOrAddSettings;
        public static Func<string,object, object>? CallSomething;

        public static string? LocalStateFolder;
        public static string? CloneAddress;
        public static string? AudioCopyVersion;
        public static string? ThisDeviceModel;
        public static double BackendAPIVersion;

        public class MediaInfo
        {
            public string Title { get; set; }
            public string Artist { get; set; }
            public string AlbumArtist { get; set; }
            public string AlbumTitle { get; set; }
            public string PlaybackType { get; set; }
            public string? AlbumArtBase64 { get; set; }
        }
    }
}
