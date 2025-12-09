using Spectre.Console;
using System.Reflection;
using FikaSync;

class Program
{
    static async Task Main(string[] args)
    {

        if (args.Contains("-d") || args.Contains("--debug"))
             Logger.Enable();

        var config = new Config();

        // Just Visual ///////////////////////////////////////////////////////////////
        AnsiConsole.Write(new FigletText("Fika Sync").LeftJustified().Color(Color.Teal));
        string versionSuffix = Logger.IsDebugEnabled ? " [red](DEBUG MODE)[/]" : "";
        AnsiConsole.Write(new Rule($"[green]v{config.AppVersion} C#[/]{versionSuffix}"));

        // Config ///////////////////////////////////////////////////////////////
        AnsiConsole.MarkupLine("[gray]Загрузка конфигурации...[/]");

        Logger.Log($"Рабочая папка: [blue]{config.BaseDir}[/]");
        Logger.Log($"Путь к профилям: [blue]{config.GameProfilesPath}[/]");
        Logger.Log($"Путь к профилям: [blue]{config.GithubToken}[/]");
        Logger.Log($"Путь к профилям: [blue]{config.RepoUrl}[/]");

        if (!config.IsValid())
        {
            AnsiConsole.MarkupLine("[red]×[/] Ошибка: Проверьте .env файл!");
            Console.ReadLine();
            return;
        }


        // GitHub ///////////////////////////////////////////////////////////////
        AnsiConsole.MarkupLine("[gray]Подключение к GitHub...[/]");

        var github = new GitHubClient(config.GithubToken);

        var updater = new Updater(github, config);
        await updater.CheckForUpdates();

        bool isAuthSuccess = await github.TestToken();
        bool shouldLaunch = false;

        if (isAuthSuccess)
        {
            try
            {
                var(owner, repo) = github.ExtractRepoInfo(config.RepoUrl);
                AnsiConsole.MarkupLine($"Целевой репозиторий: [blue]{owner}/{repo}[/]");

                string tempZipPath = Path.Combine(config.BaseDir, "temp", "repo.zip");
                string extractPath = Path.Combine(config.BaseDir, "temp", "extracted");

                string? extractedContentDir = null;
                List<string>? foundProfiles = null;
                
                AnsiConsole.MarkupLine("[gray]Скачивание архива...[/]");

                await AnsiConsole.Status()
                    .StartAsync("Загрузка данных...", async ctx =>
                    {
                        bool downloaded = await github.DownloadRepository(owner, repo, tempZipPath);
                        if (!downloaded) throw new Exception("Не удалось скачать файл");

                        ctx.Status("Распаковка...");
                        extractedContentDir = FileManager.ExtractZip(tempZipPath, extractPath);

                        if (extractedContentDir != null)
                            foundProfiles = FileManager.FindProfiles(extractedContentDir);
                    });

                if (extractedContentDir != null && foundProfiles != null && foundProfiles.Count > 0)
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine($"[bold]Найдено профилей в облаке:[/] {foundProfiles.Count}");

                    var syncer = new ProfileSync(config);
                    syncer.SyncProfiles(extractedContentDir, foundProfiles);

                    FileManager.ForceDeleteDirectory(Path.Combine(config.BaseDir, "temp"));
                } else AnsiConsole.MarkupLine("[yellow]![/] Профили в репозитории не найдены.");
                shouldLaunch = true;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Ошибка синхронизации:[/]{ex.Message}");
                shouldLaunch = AnsiConsole.Confirm("Запустить игру без синхронизации?", defaultValue: true);
                Logger.Log($"Exception: {ex}");
            }
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]![/] Синхронизация пропущена (нет связи с GitHub).");
            shouldLaunch = AnsiConsole.Confirm("Запустить игру без синхронизации?", defaultValue: true);
        }

        if (shouldLaunch)
        {
            var syncer = new ProfileSync(config);
            var initialSnapshot = syncer.GetProfilesSnapshot();

            var launcher = new GameLauncher(config);
            var gamePlayedSuccessfully = await launcher.LaunchAndMonitor();

            if (isAuthSuccess && !string.IsNullOrEmpty(config.RepoUrl) && gamePlayedSuccessfully)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.Write(new Rule("[yellow]Синхронизация[/]"));
                AnsiConsole.MarkupLine("[gray]Проверка локальных изменений...[/]");

                try 
                 {
                     var (owner, repo) = github.ExtractRepoInfo(config.RepoUrl);
                     await syncer.UploadChanges(owner, repo, initialSnapshot, github);
                 }
                 catch (Exception ex)
                 {
                    AnsiConsole.MarkupLine($"[red]Ошибка при отправке: {ex}[/]");
                 }
            }
        }
        else
            AnsiConsole.MarkupLine("[gray]Запуск отменен или сервер был крашнут.[/]");

        Console.WriteLine();
        AnsiConsole.MarkupLine("Нажмите [blue]Enter[/] чтобы выйти.");
        Console.ReadLine();
    }
}