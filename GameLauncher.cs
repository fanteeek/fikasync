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

                    Logger.Debug($"[gray]Найден конфиг:[/] {Path.GetFileName(path)} -> [blue]{_targetIp}:{_targetPort}[/]");
                    return;
                }
                catch {}
            }
        }

        Logger.Debug($"[gray]Конфиги не найдены, использую стандарт:[/]{_targetIp}:{_targetPort}");
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
            Logger.Error($"[white on red]×[/] Не найден файл сервера: {_config.SptServerPath}");
            return false;
        }

        DetectServerConfig();

        AnsiConsole.Write(new Rule("[yellow]Запуск игры[/]"));
        Logger.Info("[gray]Запускаю SPT Server...[/]");

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
            Logger.Error("[white on red]×[/] Не удалось запустить процесс сервера!");
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
            .StartAsync($"Ожидаем поднятия сервера...", async ctx =>
            {
                for (int i = 0; i < 60; i++) 
                {
                    if (serverProcess.HasExited) return;

                    if (await IsPortOpen(_targetIp, _targetPort))
                    {
                        serverReady = true;
                        return;
                    }

                    ctx.Status($"Загрузка сервера... {i}с");
                    await Task.Delay(1000);
                }
            });


        if (serverProcess.HasExited)
        {
            Logger.Error("[white on red]×[/] Сервер закрылся неожиданно!");
            return false;
        }

        if (!serverReady)
        {
            Logger.Info("[yellow]![/] Таймаут ожидания порта. Пробуем запустить лаунчер...[/]");
        }
        else
        {
            Logger.Info($"[green]√[/] Сервер успешно загрузился [green]{_targetIp}:{_targetPort}[/]");
        }

        // Launcher Start
        if (File.Exists(_config.SptLauncherPath))
        {
            Logger.Info("[gray]Открываю Лаунчер...[/]");
            Process.Start(new ProcessStartInfo
            {
                FileName = _config.SptLauncherPath,
                WorkingDirectory = Path.GetDirectoryName(_config.SptLauncherPath),
                UseShellExecute = true
            });
        }
        else
        {
            Logger.Error("[white on red]×[/] Лаунчер не был найден!");
        }

        AnsiConsole.WriteLine();

        string textPanel = "Нажмите [bold red]ENTER[/] в этом окне,\nчтобы закрыть сервер и синхронизировать профиль.";
        var panel = new Panel(textPanel);
        panel.Border = BoxBorder.Rounded;
        panel.Header = new PanelHeader("Игра запущена");
        AnsiConsole.Write(panel);
        Logger.Debug(textPanel);
        
        Console.ReadLine();

        Logger.Info("[gray]Закрываю сервер...[/]");
        try
        {
            if (!serverProcess.HasExited)
            {
                serverProcess.Kill();
                serverProcess.WaitForExit(); 
                Logger.Info("[green]√[/] Сервер остановлен.");
            }
        }
        catch {}

        return true;
    }
}