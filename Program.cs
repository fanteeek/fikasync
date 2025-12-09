using Spectre.Console;
using FikaSync;

class Program
{
    static async Task Main(string[] args)
    {

        Logger.Debug($"Application started. Args: {string.Join(" ", args)}");

        if (args.Contains("-d") || args.Contains("--debug"))
             Logger.Enable();
             Logger.Debug("Debug mode enabled via arguments");

        var config = new Config();

        // Visual ///////////////////////////////////////////////////////////////
        string versionSuffix = Logger.IsDebugEnabled ? " [red](DEBUG MODE)[/]" : "";
        Logger.Info($"[white on teal] FikaSync v{config.AppVersion}{versionSuffix} \n[/]");
        // Config ///////////////////////////////////////////////////////////////
        Logger.Info("[gray]Загрузка конфигурации...[/]");

        Logger.Debug($"Рабочая папка: [blue]{config.BaseDir}[/]");
        Logger.Debug($"Путь к профилям: [blue]{config.GameProfilesPath}[/]");
        Logger.Debug($"GitHub Token: [blue]{(string.IsNullOrEmpty(config.GithubToken) ? "[red]Не найдено[/]" : $"[blue]{config.GithubToken}[/]")}[/]");
        Logger.Debug($"GitHub URL: [blue]{(string.IsNullOrEmpty(config.RepoUrl) ? "[red]Не найдено[/]" : $"[blue]{config.GithubToken}[/]")}[/]");

        config.EnsureConfiguration();

        if (!config.IsValid())
        {
            Logger.Error("[white on red]×[/] Настройка не завершена. Выход.");
            Console.ReadLine();
            return;
        }


        // GitHub ///////////////////////////////////////////////////////////////
        Logger.Info("[gray]Подключение к GitHub...[/]");

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
                Logger.Info($"Целевой репозиторий: [blue]{owner}/{repo}[/]");

                string tempZipPath = Path.Combine(config.BaseDir, "temp", "repo.zip");
                string extractPath = Path.Combine(config.BaseDir, "temp", "extracted");

                string? extractedContentDir = null;
                List<string>? foundProfiles = null;
                
                Logger.Info("[gray]Скачивание архива...[/]");

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
                    Logger.Info($"[bold]Найдено профилей в облаке:[/] {foundProfiles.Count}");

                    var syncer = new ProfileSync(config);
                    syncer.SyncProfiles(extractedContentDir, foundProfiles);

                    FileManager.ForceDeleteDirectory(Path.Combine(config.BaseDir, "temp"));
                } else Logger.Info("[yellow]![/] Профили в репозитории не найдены.");
                shouldLaunch = true;
            }
            catch (Exception ex)
            {
                Logger.Error($"[white on red]×[/] Ошибка синхронизации: {ex.Message}");
                shouldLaunch = AnsiConsole.Confirm("Запустить игру без синхронизации?", defaultValue: true);
            }
        }
        else
        {
            Logger.Info("[yellow]![/] Синхронизация пропущена (нет связи с GitHub).");
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
                Logger.Info("[gray]Проверка локальных изменений...[/]");

                try 
                 {
                     var (owner, repo) = github.ExtractRepoInfo(config.RepoUrl);
                     await syncer.UploadChanges(owner, repo, initialSnapshot, github);
                 }
                 catch (Exception ex)
                 {
                    Logger.Error($"[white on red]×[/] Ошибка при отправке: {ex}");
                 }
            }
        }
        else
            Logger.Info("[gray]Запуск отменен или сервер был крашнут.[/]");

        Console.WriteLine();
        Logger.Info("Нажмите [blue]Enter[/] чтобы выйти.");
        Console.ReadLine();
    }
}