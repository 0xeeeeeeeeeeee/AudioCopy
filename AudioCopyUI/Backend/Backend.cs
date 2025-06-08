using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventSource;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AudioCopyUI.Backend
{
    internal class Backend
    {
        public static WebApplication backend;

        public static Action<Action>? Dispatcher { get; private set; } = null;

        private static MyEventListener? _eventListener; 

        public const int VersionCode = 2;


        public static void Init(string uri = "http://+:23456")
        {
            if (bool.Parse(SettingUtility.GetOrAddSettings("OldBackend", "False")))
            {
                _ = Program.BootBackend();
                return;
            }

            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            //builder.Logging.AddEventSourceLogger();

            //_eventListener ??= new MyEventListener();
#if DEBUG
            builder.Logging.AddDebug();
#endif

            if (bool.Parse(SettingUtility.GetOrAddSettings("EnableSwagger", "False")))
            {
                builder.Services.AddEndpointsApiExplorer();
                builder.Services.AddSwaggerGen(options =>
                {
                    options.SwaggerDoc("v1", new OpenApiInfo
                    {
                        Title = "AudioCopy Integrated Backend API",
                        Version = "v2"
                    });
                });
            }
            backend = builder.Build();
            if (bool.Parse(SettingUtility.GetOrAddSettings("EnableSwagger", "False")))
            {
                backend.UseSwagger();
                backend.UseSwaggerUI(options =>
                {
                    options.SwaggerEndpoint("/swagger/v1/swagger.json", "AudioCopy Integrated Backend API");
                    options.RoutePrefix = "swagger";
                    options.DocumentTitle = $"AudioCopy Swagger @ {Assembly.GetExecutingAssembly().GetName().Version}";
                });
            }
            backend.MapGet("/index", async (v) => 
            {
                v.Response.ContentType = "text/html";
                await v.Response.WriteAsync(
                
$""""""
<!DOCTYPE html><html><head><meta charset='utf-8'/>
    <title>AudioCopy</title>
</head>
<body>
    <img src="api/device/GetAlbumPhoto" width="100" height="100">
    <br>   
    <a href="https://github.com/0xeeeeeeeeeeee/AudioCopy">AudioCopy</a>
    <br>
    AudioCopy Version <code>{Assembly.GetExecutingAssembly().GetName().Version} </code>
</body>
</html>
"""""");
            });
            backend.MapGet("/Detect", async (string token = "") =>
            {
                if (string.IsNullOrWhiteSpace(token)) return Results.Text("Ready");
                else if (TokenController.Auth(token)) return Results.Text("OK");
                else return Results.Unauthorized();
            });

            backend.MapGet("/version", () =>
            {
                return VersionCode.ToString();
            });

            backend.MapGet("/api/audio/GetAudioFormat", async (string token) =>
            {
                if (!TokenController.Auth(token)) return Results.Unauthorized();
                await AudioCloneHelper.Boot();
                HttpClient c = new();
                c.BaseAddress = new Uri($"http://127.0.0.1:{AudioCloneHelper.Port}");
                var r = await c.GetAsync($"/api/audio/GetAudioFormat?token={AudioCloneHelper.Token}");
                return Results.Text(await r.Content.ReadAsStringAsync(), "application/json", null);

            });
           

            var audioGroup = backend.MapGroup("/api/audio");
            var deviceGroup = backend.MapGroup("/api/device");
            var tokenGroup = backend.MapGroup("/api/token");
            DeviceController.Init(deviceGroup);
            TokenController.Init(tokenGroup);
            


            Thread t = new(() =>
            {
                backend.Run(uri);
            });
            t.Start();
            return;
        }

        class MyEventListener : EventListener
        {
            protected override void OnEventSourceCreated(EventSource eventSource) //从mslearn那里复制黏贴的
            {
                if (eventSource.Name == "Microsoft-Extensions-Logging")
                {                  
                    var args = new Dictionary<string, string>() { { "FilterSpecs", "App*:Information;*" } };
                    EnableEvents(eventSource, EventLevel.Error, LoggingEventSource.Keywords.FormattedMessage, args);
                }
            }
            protected override void OnEventWritten(EventWrittenEventArgs eventData)
            {
                // Look for the formatted message event, which has the following argument layout (as defined in the LoggingEventSource):
                // FormattedMessage(LogLevel Level, int FactoryID, string LoggerName, string EventId, string FormattedMessage);
                if (eventData.EventName == "FormattedMessage")
                    Log(eventData.Payload[4] as string, $"IntegratedBackend-{(LogLevel)eventData.Payload[0]}-{(eventData.Payload[2] ?? "Unknown source") as string}" );
                    //Console.WriteLine($"Logger {}: {}");
            }
        }


    }
}
