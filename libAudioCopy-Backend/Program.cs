/*
*	 File: Program.cs
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
using libAudioCopy;
using libAudioCopy.Audio;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.Collections;
using System.Collections.Concurrent;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;

internal class Program
{
    private static void Main(string[] args)
    {
        Thread.Sleep(2500);//等待日志采集启动

        Console.WriteLine($"Current Directory: {Directory.GetCurrentDirectory()}");

        //foreach (DictionaryEntry de in Environment.GetEnvironmentVariables())
        //    if((de.Key.ToString() ?? "").StartsWith("ASPNET") ||(de.Key.ToString() ?? "").StartsWith("AudioCopy")) Console.WriteLine($"{de.Key} : {de.Value}");


        var builder = WebApplication.CreateBuilder(args);
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting", LogLevel.None);
        builder.Logging.AddFilter("Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker", LogLevel.None);
        builder.Logging.AddFilter("Microsoft.AspNetCore.Routing.EndpointMiddleware", LogLevel.None);
        builder.Logging.AddFilter("Microsoft.AspNetCore.Mvc.Infrastructure.ObjectResultExecutor", LogLevel.None);
        builder.Logging.AddFilter("Microsoft.AspNetCore.Mvc.StatusCodeResult", LogLevel.Information);



        PrintLocalNetworkAddresses();


        builder.Services.AddSingleton<AudioProvider>();
        builder.Services.AddSingleton<TokenService>();
        builder.Services.AddSingleton<ConcurrentDictionary<string, string>>(); //懒
        builder.Services.AddSingleton<ConcurrentBag<DeviceController.MediaInfo>>(); //懒


        builder.Services.AddControllers();

        builder.Services.Configure<KestrelServerOptions>(opts => opts.AllowSynchronousIO = true);
        builder.Services.Configure<IISServerOptions>(opts => opts.AllowSynchronousIO = true);
        if (builder.Environment.IsDevelopment())
        {
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
        }

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler(errorApp =>
            {
                errorApp.Run(async context =>
                {
                    var contextFeature = context.Features.Get<IExceptionHandlerFeature>();
                    if (contextFeature != null)
                    {
                        var ex = contextFeature.Error;
                        var errorInfo = new
                        {
                            Type = ex.GetType().FullName,
                            Message = ex.Message,
                            StackTrace = ex.StackTrace,
                            InnerException = ex.InnerException?.Message
                        };
                        string json = JsonSerializer.Serialize(errorInfo, new JsonSerializerOptions { WriteIndented = false });
                        Console.Error.WriteLine("ERROR!" + json);
                        context.Response.StatusCode = 500;
                        context.Response.ContentType = "text/plain";
                        await context.Response.WriteAsync($"服务器发生了{contextFeature.Error.GetType().Name}异常: {contextFeature.Error.Message}");
                    }
                });
            });
        }
        else
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.MapControllers();
        try
        {
            app.Run();

        }
        catch (Exception ex)
        {
            var errorInfo = new
            {
                Type = ex.GetType().FullName,
                Message = ex.Message,
                StackTrace = ex.StackTrace,
                InnerException = ex.InnerException?.Message
            };
            string json = JsonSerializer.Serialize(errorInfo, new JsonSerializerOptions { WriteIndented = false});
            Console.Error.WriteLine("ERROR!"+json);
        }
    }

    public class BackendExceptionObject
    {
        public string Type { get; set; }
        public string Message { get; set; }
        public string StackTrace { get; set; }
        public string InnerException { get; set; }
    }

        private static bool IsLocalNetwork(string ipAddress)
    {
        return ipAddress.StartsWith("192.168.") || ipAddress.StartsWith("10.") ||
               (ipAddress.StartsWith("172.") && int.TryParse(ipAddress.Split('.')[1], out int secondOctet) && secondOctet >= 16 && secondOctet <= 31);
    }
    private static void PrintLocalNetworkAddresses()
    {
        var interfaces = NetworkInterface.GetAllNetworkInterfaces();

        foreach (var networkInterface in interfaces)
        {
            if (networkInterface.OperationalStatus == OperationalStatus.Up &&
                networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                !networkInterface.Description.Contains("Vmware", StringComparison.OrdinalIgnoreCase) &&
                !networkInterface.Description.Contains("VirtualBox", StringComparison.OrdinalIgnoreCase))
            {
                var ipProperties = networkInterface.GetIPProperties();
                foreach (var ipAddress in ipProperties.UnicastAddresses)
                {
                    if (ipAddress.Address.AddressFamily == AddressFamily.InterNetwork &&
                        IsLocalNetwork(ipAddress.Address.ToString()))
                    {
                        Console.WriteLine($"Interface: {networkInterface.Name}, IP:{ipAddress.Address}");
                    }
                }
            }
        }
    }
}
