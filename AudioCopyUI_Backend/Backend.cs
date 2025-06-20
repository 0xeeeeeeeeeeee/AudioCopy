using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventSource;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
using static AudioCopyUI_MiddleWare.BackendHelper;


namespace AudioCopyUI.Backend
{
    public class Backend
    {
        public static WebApplication backend;

        public static Action<Action>? Dispatcher { get; private set; } = null;


        public const double VersionCode = 2.1;

        public static bool Running { get; private set; }

        [RequiresUnreferencedCode("something here :)")]
        public static void Init(string uri = "http://+:23456", bool STA = false)
        {
            BackendAPIVersion = VersionCode;

            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            //builder.Logging.AddConsole();
            builder.Logging.AddSimpleConsole();
            builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting", LogLevel.None);
            builder.Logging.AddFilter("Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker", LogLevel.None);
            builder.Logging.AddFilter("Microsoft.AspNetCore.Routing.EndpointMiddleware", LogLevel.None);
            builder.Logging.AddFilter("Microsoft.AspNetCore.Mvc.Infrastructure.ObjectResultExecutor", LogLevel.None);
            builder.Logging.AddFilter("Microsoft.AspNetCore.Mvc.StatusCodeResult", LogLevel.Error);
            builder.Logging.AddFilter("Microsoft.AspNetCore.Http.Result", LogLevel.Error);

#if DEBUG
            builder.Logging.AddDebug();
#endif

            if (bool.Parse(GetOrAddSettings("EnableSwagger", "False")))
            {
                builder.Services.AddEndpointsApiExplorer();
                builder.Services.AddSwaggerGen(options =>
                {
                    options.SwaggerDoc("v1", new OpenApiInfo
                    {
                        Title = "AudioCopy Integrated Backend API",
                        Version = "v" + VersionCode.ToString()
                    });
                });
            }
            backend = builder.Build();
            backend.UseExceptionHandler(errorApp =>
            {
                errorApp.Run(async context =>
                {
                    var contextFeature = context.Features.Get<IExceptionHandlerFeature>();
                    if (contextFeature != null)
                    {
                        await context.Response.WriteAsync($"A {contextFeature.Error.GetType().Name} exception happens: {contextFeature.Error.Message}");
                        LogEx(contextFeature.Error, "process request", "IntegratedBackend");
                    }
                });
            });
            if (bool.Parse(GetOrAddSettings("EnableSwagger", "False")))
            {
                backend.UseSwagger();
                backend.UseSwaggerUI(options =>
                {
                    options.SwaggerEndpoint("/swagger/v1/swagger.json", "AudioCopy Integrated Backend API");
                    options.RoutePrefix = "swagger";
                    options.DocumentTitle = $"AudioCopy Swagger @ {AudioCopyVersion}";
                });
            }
            backend.MapGet("/index", async (v) => 
            {
                v.Response.ContentType = "text/html";
                await v.Response.WriteAsync(
                
$""""""
<!DOCTYPE html><html><head><meta charset='utf-8'/>
    <title>AudioCopy Integrated Backend</title>
</head>
<body>
    <a href="https://github.com/0xeeeeeeeeeeee/AudioCopy">
        <img src="api/device/GetAlbumPhoto" width="100" height="100">       
    </a>
    <br>
    AudioCopy <code>v{AudioCopyVersion}</code> @ {Environment.MachineName}
    <br>
    AudioCopy Integrated Backend <code>v{VersionCode}</code>
</body>
</html>
"""""");
            });
            backend.MapGet("/Detect", (string token = "") =>
            {
                if (string.IsNullOrWhiteSpace(token)) return Results.Text("Ready");
                else if (TokenController.Auth(token)) return Results.Text("OK");
                else return Results.Unauthorized();
            });

            backend.MapGet("/version", () =>
            {
                return VersionCode.ToString();
            });

            //backend.MapGet("/crash", () =>
            //{
            //    throw new NotSupportedException("fun thing hah :)");
            //});         

            var deviceGroup = backend.MapGroup("/api/device");
            var tokenGroup = backend.MapGroup("/api/token");
            DeviceController.Init(deviceGroup);
            TokenController.Init(tokenGroup);

            if (STA)
            {
                Running = true;
                backend.Run(uri);
                Running = false;
                return;
            }

            Thread t = new(() =>
            {
                Running = true;
                backend.Run(uri);
                Running = false;
            });
            t.Start();
            return;
        }

       


    }
}
