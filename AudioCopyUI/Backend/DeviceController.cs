using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.Media.Control;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Tab;

namespace AudioCopyUI.Backend
{
    internal class DeviceController
    {
        public static void Init(RouteGroupBuilder group)
        {
            group.MapGet("/BootAudioClone", (string token) =>
            {
                if (!TokenController.Auth(token))
                {
                    return Results.Unauthorized();
                }
                AudioCloneHelper.Boot();
                return Results.Ok(AudioCloneHelper.Port.ToString());
            });

            group.MapGet("/GetSMTCInfo", async (string token) =>
            {
                if (!TokenController.Auth(token))
                {
                    return Results.Unauthorized();
                }

                var i = await GetSMTCAsync();
                i.AlbumArtBase64 = null;

                return Results.Ok(i);
            });

            group.MapGet("/GetAlbumPhoto", async (string token) =>
            {
                try
                {
                    if (!TokenController.Auth(token))
                    {
                        goto fallback;
                    }

                    MediaInfo? body = await GetSMTCAsync();
                    

                    if (body != null && !string.IsNullOrEmpty(body.AlbumArtBase64))
                    {
                        string base64 = body.AlbumArtBase64;
                        var bytes = Convert.FromBase64String(base64);
                        var head = bytes.Take(64);
                        var headStr = Encoding.UTF8.GetString(head.ToArray());
                        if (headStr.Contains("PNG")) return Results.File(bytes, "image/png", "album.png");
                        return Results.File(bytes, "image/jpeg", "album.jpg");
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"A {ex.GetType().Name} error happens:{ex.Message}");
                }

            fallback:
                return Results.File(
                    System.IO.File.ReadAllBytes(
                        Path.Combine(GlobalUtility.LocalStateFolder,"staticResource\\",
                        "AudioCopy.png")), "image/png", "AudioCopy.png"); //返回一个默认值

            });
        }


        private static async Task<MediaInfo> GetSMTCAsync()
        {
            MediaInfo? i = null;
            var tcs = new TaskCompletionSource<MediaInfo>();

            MainWindow.dispatcher.TryEnqueue(async () =>
            {
                try
                {
                    var result = await GetCurrentMediaInfoAsync();

                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            return await tcs.Task;
        }

        public static async Task<MediaInfo> GetCurrentMediaInfoAsync()
        {
            var sessions = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var currentSession = sessions.GetCurrentSession();
            if (currentSession == null) return null;

            var mediaProperties = await currentSession.TryGetMediaPropertiesAsync();
            var info = new MediaInfo
            {
                Title = mediaProperties.Title,
                Artist = mediaProperties.Artist,
                AlbumArtist = mediaProperties.AlbumArtist,
                AlbumTitle = mediaProperties.AlbumTitle,
                PlaybackType = mediaProperties.PlaybackType.ToString()
            };

            // 获取专辑封面
            if (mediaProperties.Thumbnail != null)
            {
                using var stream = await mediaProperties.Thumbnail.OpenReadAsync();
                using var ms = new MemoryStream();
                await stream.AsStreamForRead().CopyToAsync(ms);
                info.AlbumArtBase64 = Convert.ToBase64String(ms.ToArray());
            }

            return info;
        }

        public class MediaInfo
        {
            public string Title { get; set; }
            public string Artist { get; set; }
            public string AlbumArtist { get; set; }
            public string AlbumTitle { get; set; }
            public string PlaybackType { get; set; }
            public string AlbumArtBase64 { get; set; }
        }
    }
}
