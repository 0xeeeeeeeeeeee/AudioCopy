using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AudioCopyUI.Backend
{
    internal class Backend
    {
        private static WebApplication backend;

        public static Action<Action>? Dispatcher { get; private set; } = null;   

        public const string VersionCode = "2";
        public static void Init(Action<Action> dispatcher, string uri = "http://+:23456")
        {
            Dispatcher = dispatcher;

            var builder = WebApplication.CreateBuilder();
            if (bool.Parse(SettingUtility.GetOrAddSettings("EnableDevelopmentMode", "False")))
            {
                builder.Services.AddEndpointsApiExplorer();
                builder.Services.AddSwaggerGen(options =>
                {
                    options.SwaggerDoc("v1", new OpenApiInfo
                    {
                        Title = "AudioCopy API",
                        Version = "v1"
                    });
                });
            }
            backend = builder.Build();
            if (bool.Parse(SettingUtility.GetOrAddSettings("EnableDevelopmentMode", "False")))
            {
                backend.UseSwagger();
                backend.UseSwaggerUI(options =>
                {
                    options.SwaggerEndpoint("/swagger/v1/swagger.json", "AudioCopy API V2");
                    options.RoutePrefix = "swagger"; // 访问路径为 /swagger
                });
            }
            backend.MapGet("/index", () => 
            {
                return 
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
"""""";
            });

            backend.MapGet("/version", () =>
            {
                return VersionCode;
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
    }
}
