using System;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace AudioClone
{
    internal class Program
    {
        static int Main(string[] args)
        {
            var rootCommand = new RootCommand("AudioClone 命令行工具");

            var helpCommand = new Command("help", "显示帮助信息");
            helpCommand.SetHandler((InvocationContext context) =>
            {
                context.Console.Out.WriteLine("AudioClone 命令行帮助:");
                context.Console.Out.WriteLine("  help         显示帮助信息");
                context.Console.Out.WriteLine("  version      显示版本信息");
                // 可在此添加更多命令说明
            });

            var versionCommand = new Command("version", "显示版本信息");
            versionCommand.SetHandler((InvocationContext context) =>
            {
                context.Console.Out.WriteLine("AudioClone 版本 1.0.0");
            });

            rootCommand.AddCommand(helpCommand);
            rootCommand.AddCommand(versionCommand);

            // 默认行为
            rootCommand.SetHandler((InvocationContext context) =>
            {
                context.Console.Out.WriteLine("用法: AudioClone <命令> [参数]");
                context.Console.Out.WriteLine("可用命令:");
                context.Console.Out.WriteLine("  help         显示帮助信息");
                context.Console.Out.WriteLine("  version      显示版本信息");
            });

            return rootCommand.Invoke(args);
        }
    }
}
