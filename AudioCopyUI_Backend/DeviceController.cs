using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using static AudioCopyUI_MiddleWare.BackendHelper;

namespace AudioCopyUI.Backend
{
    public class DeviceController
    {
        [RequiresUnreferencedCode("something here :)")]
        public static void Init(RouteGroupBuilder group)
        {
            group.MapGet("/BootAudioClone", async (string token) =>
            {
                if (!TokenController.Auth(token))
                {
                    return Results.Unauthorized();
                }
                if (BootAudioClone is null) throw new ArgumentNullException();
                if (!BootAudioClone.IsCompleted)
                {
                    BootAudioClone?.Start();
                    await BootAudioClone;
                }
                return Results.Text(CloneAddress);
            });

            Stopwatch sw = Stopwatch.StartNew();
            MediaInfo? i = null;

            group.MapGet("/GetSMTCInfo", async (string token) =>
            {
                if (!TokenController.Auth(token))
                {
                    return Results.Unauthorized();
                }
                

                i = await GetSMTCAsync();

                if (i is null) return Results.NoContent();
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
                    LogEx(ex, "Get album photo", "Backend-DeviceController");
                }

            fallback:
                return Results.File(
                    System.IO.File.ReadAllBytes(
                        Path.Combine(LocalStateFolder,"AudioCopy.png"))
                        , "image/png", "AudioCopy.png"); //返回一个默认值

            });

            if (!bool.Parse(GetOrAddSettings("NoDiscover", "False")))
            {
                group.MapGet("/Information", () =>
                {

                    return Results.Text($"{Environment.MachineName};{AudioCopyVersion};{ThisDeviceModel}");

                });
            }
        }


        private static async Task<MediaInfo?> GetSMTCAsync()
        {
            MediaInfo? i = null;
            var tcs = new TaskCompletionSource<MediaInfo?>();

            Dispatch(new(() =>
            {
                try
                {
                    var result = GetCurrentMediaInfoAsync();

                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }));

            return await tcs.Task;
        }

        
    }
}
