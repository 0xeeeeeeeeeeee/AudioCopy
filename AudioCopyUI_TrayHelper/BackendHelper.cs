using System;
using System.Collections.Concurrent;
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
        public static Action? BootAudioClone;

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
        public static DeviceType? ThisDeviceTypeID;
        public static string? ThisDeviceUdid;
        public static double BackendAPIVersion;

        public static ConcurrentDictionary<string, DevicesInfo>? DiscoveredClients;
        public static ConcurrentDictionary<string, List<string>>? DiscoveredClientsAddress;


        public class MediaInfo
        {
            public string Title { get; set; }
            public string Artist { get; set; }
            public string AlbumArtist { get; set; }
            public string AlbumTitle { get; set; }
            public string PlaybackType { get; set; }
            public string? AlbumArtBase64 { get; set; }
        }

        public class DevicesInfo
        {
            public string Name { get; set; }
            public string udid { get; set; }
            public string AudioCopyVersion { get; set; }
            public string DeviceModel { get; set; }
            public DeviceType DeviceType { get; set; } 

            public string GetDeviceTypeIcon()
            {
                return DeviceType switch
                {
                    DeviceType.Desktop => "\uE977",
                    DeviceType.Laptop => "\uE7F8",
                    DeviceType.Phone => "\uE8EA",
                    DeviceType.Tablet => "\uE70A",
                    _ => "\uE703"
                };
            }

            public static bool operator ==(DevicesInfo? a, DevicesInfo? b) => a?.udid == b?.udid;
            public static bool operator !=(DevicesInfo? a, DevicesInfo? b) => a?.udid != b?.udid;

            public override bool Equals(object? obj)
            {
                if (obj is not DevicesInfo) return false;
                return (obj as DevicesInfo).udid == this.udid;
            }

            public override int GetHashCode()
            {
                return udid.GetHashCode();
            }

        }
    }

    public enum DeviceType 
    {
        Unknown,
        Desktop,
        Laptop,
        Phone,
        Tablet,
        Other,
        

    }
}
