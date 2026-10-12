using Serilog;
using ServerShared.CommonModels;
using ServerShared.Controllers;
using ServerShared.Titles;
using Shared;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;

namespace ServerApp;

internal class Program
{
    public static Settings ServerAppSettings = JsonController.Read<Settings>("ServerAppSettings.json");
    static void Main(string[] args)
    {
        MainLogger.CreateNew();
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Log.Fatal(e.ExceptionObject as Exception, "Unhandled exception; the server is terminating");
            MainLogger.Close();
        };

        ArgProcess.Process(args);
        CertLoader.Load();
        List<TitleDescriptor> titles = TitleRegistry.Read(DefaultTitles.All);
        TitleCatalogClient.Apply(ServerAppSettings.TitleCatalog, titles);
        IReadOnlyList<string> titleProblems = TitleRegistry.Publish(titles);
        if (titleProblems.Count > 0)
        {
            Log.Fatal("Configs/{File} is invalid ({Count} problem(s) above); refusing to start",
                TitleRegistry.ConfigFileName, titleProblems.Count);
            MainLogger.Close();
            return;
        }
        foreach (TitleDescriptor title in TitleRegistry.All)
            Log.Information("Title {Title} registered (family: {Family}; ports: {Ports})", title,
                string.IsNullOrEmpty(title.ProtocolFamily) ? "none" : title.ProtocolFamily,
                title.Matchers.Ports.Count == 0 ? "none" : string.Join("/", title.Matchers.Ports));

        List<string> limitProblems = ServerAppSettings.Servers
            .SelectMany(server => server.UseUDP
                ? server.UdpLimits.Validate(server.Name)
                : server.HttpLimits.Validate(server.Name))
            .ToList();
        if (limitProblems.Count > 0)
        {
            foreach (string problem in limitProblems)
                Log.Fatal("ServerAppSettings.json: {Problem}", problem);
            MainLogger.Close();
            return;
        }

        Collection<ServerModel> servers = [];

        foreach (var server in ServerAppSettings.Servers)
        {
            Log.Information($"New Server {server.Name} registered!");
            servers.Add(new()
            {
                Name  = server.Name,
                Port = server.Port,
                IsSecure = server.UseCerts,
                IsUdp = server.UseUDP,
                HttpLimits = server.HttpLimits,
                UdpLimits = server.UdpLimits,
            });
        }

        ServerController.Start(servers);
        PluginController.LoadPlugins();
        WaitForShutdown();
        Log.Information("Stopping");
        PluginController.StopPlugins();
        ServerController.Stop();
        MainLogger.Close();
    }

    private static readonly string[] QuitCommands = ["q", "quit", "exit"];

    private static void WaitForShutdown()
    {
        using var stop = new ManualResetEventSlim();
        void Stop(PosixSignalContext context)
        {
            context.Cancel = true;
            Log.Information("Received {Signal}", context.Signal);
            stop.Set();
        }
        using PosixSignalRegistration terminate = PosixSignalRegistration.Create(PosixSignal.SIGTERM, Stop);
        using PosixSignalRegistration interrupt = PosixSignalRegistration.Create(PosixSignal.SIGINT, Stop);

        var console = new Thread(() => ReadConsole(stop)) { IsBackground = true, Name = "console" };
        console.Start();
        stop.Wait();
    }

    private static void ReadConsole(ManualResetEventSlim stop)
    {
        while (Console.ReadLine() is { } line)
        {
            if (QuitCommands.Contains(line))
            {
                stop.Set();
                return;
            }
            if (line.StartsWith('!'))
                CommandController.Run(line[1..]);
        }
    }
}
