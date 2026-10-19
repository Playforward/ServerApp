using Serilog;
using Serilog.Events;
using ServerShared.Controllers;
using Shared;

namespace ServerApp;

public static class ArgProcess
{
    public static void Process(string[] args)
    {
        if (args.Contains("example"))
        {
            CreateExample();
            Environment.Exit(0);
        }
        if (args.Contains("clean"))
        {
            MainLogger.Close();
            // Deleting all files and the database too.
            foreach (var logfile in Directory.GetFiles(Environment.CurrentDirectory, "*.log", SearchOption.AllDirectories))
                File.Delete(logfile);
            Directory.Delete("Database", true);
            MainLogger.CreateNew();
        }
        if (args.Contains("debug"))
        {
            // Verbose expose MWS to us. we using DEBUG.
            MainLogger.LevelSwitch.MinimumLevel = LogEventLevel.Debug;
            MainLogger.ConsoleLevelSwitch.MinimumLevel = LogEventLevel.Debug;
            MainLogger.FileLevelSwitch.MinimumLevel = LogEventLevel.Debug;
        }
        if (args.Contains("verbose"))
        {
            MainLogger.LevelSwitch.MinimumLevel = LogEventLevel.Verbose;
            MainLogger.ConsoleLevelSwitch.MinimumLevel = LogEventLevel.Verbose;
            MainLogger.FileLevelSwitch.MinimumLevel = LogEventLevel.Verbose;
        }

        if (!Directory.Exists("Cert") && Program.ServerAppSettings.Servers.Any(x => x.UseCerts))
        {
            Log.Error("You have not created a 'Cert' folder to include your certificates, but your settings have to user cert. Please make a 'Cert' directory and install any certificate!");
            Environment.Exit(1);
        }
    }

    private static void CreateExample()
    {
        Program.ServerAppSettings.Servers.Add(new()
        {
            Name = "MainWeb",
            Port = 80,
        });
        Program.ServerAppSettings.Servers.Add(new()
        {
            Name = "MainWebSSL",
            Port = 443,
            UseCerts = true
        });
        Program.ServerAppSettings.Servers.Add(new()
        {
            Name = "TestUDP",
            Port = 8989,
            UseUDP = true
        });
        Program.ServerAppSettings.CertDetails.Add(new()
        {
            Name = "ServerEmusPFX",
            Password = "ServerEmus"
        });
        Program.ServerAppSettings.CertDetails.Add(new()
        {
            Name = "UbisoftCert",
            Password = "ServerEmus"
        });
        JsonController.Save(Program.ServerAppSettings, "ServerAppSettings.json");
    }
}
