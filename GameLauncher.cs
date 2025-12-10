using System.Diagnostics;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using Spectre.Console;

namespace FikaSync;

public class GameLauncher
{

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private readonly Config _config;

    private string _targetIp = "127.0.0.1";
    private int _targetPort = 6969;

    public GameLauncher(Config config)
    {
        _config = config;
    }

    private void DetectServerConfig()
    {
        string serverDir = Path.GetDirectoryName(_config.SptServerPath) ?? _config.BaseDir;

        var possiblePaths = new List<string>
        {
            Path.Combine(serverDir, "user", "mods", "fika-server", "assets", "configs", "fika.jsonc"),
            Path.Combine(serverDir, "SPT_data", "Server", "configs", "http.json")
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                try
                {
                    string content = File.ReadAllText(path);

                    var portMatch = Regex.Match(content, @"""port""\s*:\s*(\d+)");
                    if (portMatch.Success && int.TryParse(portMatch.Groups[1].Value, out int foundPort))
                        _targetPort = foundPort;

                    var ipMatch = Regex.Match(content, @"""ip""\s*:\s\s*""([^""]+)""");
                    if (ipMatch.Success)
                    {
                        string rawIp = ipMatch.Groups[1].Value;
                        _targetIp = (rawIp == "0.0.0.0") ? "127.0.0.1" : rawIp;
                    }

                    Logger.Debug($"[gray]Configuration found:[/] {Path.GetFileName(path)} -> [blue]{_targetIp}:{_targetPort}[/]");
                    return;
                }
                catch {}
            }
        }

        Logger.Debug($"[gray]Configs not found, using default:[/]{_targetIp}:{_targetPort}");
    }

    private async Task<bool> IsPortOpen(string host, int port)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host, port);
            var completedTask = await Task.WhenAny(connectTask, Task.Delay(500));
            
            if (completedTask == connectTask)
            {
                return client.Connected;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> LaunchAndMonitor()
    {
        // Start Server
        if (!File.Exists(_config.SptServerPath))
        {
            Logger.Error($"Server file not found: {_config.SptServerPath}");
            return false;
        }

        DetectServerConfig();

        AnsiConsole.Write(new Rule("[yellow]Starting the game[/]"));
        Logger.Info("[gray]Starting SPT Server...[/]");

        var serverInfo = new ProcessStartInfo
        {
            FileName = _config.SptServerPath,
            WorkingDirectory = Path.GetDirectoryName(_config.SptServerPath),
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Minimized
        };

        Process? serverProcess = Process.Start(serverInfo);

        if (serverProcess == null)
        {
            Logger.Error("Failed to start the server process!");
            return false;
        }

        await Task.Delay(500);

        try
        {
            var handle = GetConsoleWindow();
            SetForegroundWindow(handle);
        }catch{}

        bool serverReady = false;
        // Wait Server
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync($"Waiting for the server to start up...", async ctx =>
            {
                for (int i = 0; i < 60; i++) 
                {
                    if (serverProcess.HasExited) return;

                    if (await IsPortOpen(_targetIp, _targetPort))
                    {
                        serverReady = true;
                        return;
                    }

                    ctx.Status($"Loading server... {i}s");
                    await Task.Delay(1000);
                }
            });


        if (serverProcess.HasExited)
        {
            Logger.Error("The server shut down unexpectedly!");
            return false;
        }

        if (!serverReady)
        {
            Logger.Info("[yellow]![/] Server wait timeout.[/]");
        }
        else
        {
            Logger.Info($"[green]√[/] The server has successfully booted up [green]{_targetIp}:{_targetPort}[/]");
        }

        // Launcher Start
        if (File.Exists(_config.SptLauncherPath))
        {
            Logger.Info("[gray]Opening Launcher...[/]");
            Process.Start(new ProcessStartInfo
            {
                FileName = _config.SptLauncherPath,
                WorkingDirectory = Path.GetDirectoryName(_config.SptLauncherPath),
                UseShellExecute = true
            });
        }
        else
        {
            Logger.Error("Launcher not found!");
        }

        AnsiConsole.WriteLine();

        string textPanel = "Press [bold red]ENTER[/] in this window to close the server and synchronize the profile.";
        var panel = new Panel(textPanel);
        panel.Border = BoxBorder.Rounded;
        panel.Header = new PanelHeader("The game has started");
        AnsiConsole.Write(panel);
        Logger.Debug(textPanel);
        
        Console.ReadLine();

        Logger.Info("[gray]I'm shutting down the server...[/]");
        try
        {
            if (!serverProcess.HasExited)
            {
                serverProcess.Kill();
                serverProcess.WaitForExit(); 
                Logger.Info("[green]√[/] The server has been shut down.");
            }
        }
        catch {}

        return true;
    }
}