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
                    
                    Logger.Debug(Loc.Tr("Config_Found", Path.GetFileName(path), _targetIp, _targetPort));
                    return;
                }
                catch {}
            }
        }

        Logger.Debug(Loc.Tr("Config_Default", _targetIp, _targetPort));
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
            Logger.Error(Loc.Tr("Server_NotFound", _config.SptServerPath));
            return false;
        }

        DetectServerConfig();

        AnsiConsole.Write(new Rule(Loc.Tr("Game_Starting")));
        Logger.Info(Loc.Tr("Server_Starting"));

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
            Logger.Error(Loc.Tr("Server_Process_Fail"));
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
            .StartAsync(Loc.Tr("Server_Waiting"), async ctx =>
            {
                for (int i = 0; i < 60; i++) 
                {
                    if (serverProcess.HasExited) return;

                    if (await IsPortOpen(_targetIp, _targetPort))
                    {
                        serverReady = true;
                        return;
                    }

                    ctx.Status(Loc.Tr("Server_Loading", i));
                    await Task.Delay(1000);
                }
            });


        if (serverProcess.HasExited)
        {
            Logger.Error(Loc.Tr("Server_Exited"));
            return false;
        }

        if (!serverReady)
        {
            Logger.Info(Loc.Tr("Server_Timeout"));
        }
        else
        {
            Logger.Info(Loc.Tr("Server_Success", _targetIp, _targetPort));
        }

        // Launcher Start
        if (File.Exists(_config.SptLauncherPath))
        {
            Logger.Info(Loc.Tr("Launcher_Opening"));
            Process.Start(new ProcessStartInfo
            {
                FileName = _config.SptLauncherPath,
                WorkingDirectory = Path.GetDirectoryName(_config.SptLauncherPath),
                UseShellExecute = true
            });
        }
        else
        {
            Logger.Error(Loc.Tr("Launcher_NotFound"));
        }

        AnsiConsole.WriteLine();

        string textPanel = Loc.Tr("Game_Close_Instruction");
        var panel = new Panel(textPanel);
        panel.Border = BoxBorder.Rounded;
        panel.Header = new PanelHeader(Loc.Tr("Game_Started_Title"));
        AnsiConsole.Write(panel);
        Logger.Debug(textPanel);
        
        Console.ReadLine();

        Logger.Info(Loc.Tr("Server_Stopping"));
        try
        {
            if (!serverProcess.HasExited)
            {
                serverProcess.Kill();
                serverProcess.WaitForExit(); 
                Logger.Info(Loc.Tr("Server_Stopped"));
            }
        }
        catch {}

        return true;
    }
}