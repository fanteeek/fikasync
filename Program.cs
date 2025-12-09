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
        Logger.Info("[gray]Loading configuration...[/]");

        Logger.Debug($"Working folder: [blue]{config.BaseDir}[/]");
        Logger.Debug($"Path to profiles: [blue]{config.GameProfilesPath}[/]");
        Logger.Debug($"GitHub Token: [blue]{(string.IsNullOrEmpty(config.GithubToken) ? "[red]Not found[/]" : $"[blue]{config.GithubToken}[/]")}[/]");
        Logger.Debug($"GitHub URL: [blue]{(string.IsNullOrEmpty(config.RepoUrl) ? "[red]Not found[/]" : $"[blue]{config.GithubToken}[/]")}[/]");

        config.EnsureConfiguration();

        if (!config.IsValid())
        {
            Logger.Error("[white on red]×[/] Setup not complete. Exit.");
            Console.ReadLine();
            return;
        }


        // GitHub ///////////////////////////////////////////////////////////////
        Logger.Info("[gray]Connecting to GitHub...[/]");

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
                Logger.Info($"Target repository: [blue]{owner}/{repo}[/]");

                string tempZipPath = Path.Combine(config.BaseDir, "temp", "repo.zip");
                string extractPath = Path.Combine(config.BaseDir, "temp", "extracted");

                string? extractedContentDir = null;
                List<string>? foundProfiles = null;
                
                Logger.Info("[gray]Downloading archive...[/]");

                await AnsiConsole.Status()
                    .StartAsync("Loading data...", async ctx =>
                    {
                        bool downloaded = await github.DownloadRepository(owner, repo, tempZipPath);
                        if (!downloaded) throw new Exception("Failed to download file");

                        ctx.Status("Unpacking...");
                        extractedContentDir = FileManager.ExtractZip(tempZipPath, extractPath);

                        if (extractedContentDir != null)
                            foundProfiles = FileManager.FindProfiles(extractedContentDir);
                    });

                if (extractedContentDir != null && foundProfiles != null && foundProfiles.Count > 0)
                {
                    AnsiConsole.WriteLine();
                    Logger.Info($"[bold]Profiles found in the cloud:[/] {foundProfiles.Count}");

                    var syncer = new ProfileSync(config);
                    syncer.SyncProfiles(extractedContentDir, foundProfiles);

                    FileManager.ForceDeleteDirectory(Path.Combine(config.BaseDir, "temp"));
                } else Logger.Info("[yellow]![/] No profiles found in the repository.");
                shouldLaunch = true;
            }
            catch (Exception ex)
            {
                Logger.Error($"[white on red]×[/] Synchronization error: {ex.Message}");
                shouldLaunch = AnsiConsole.Confirm("Start the game without synchronization?", defaultValue: true);
            }
        }
        else
        {
            Logger.Info("[yellow]![/] Synchronization skipped (no connection to GitHub).");
            shouldLaunch = AnsiConsole.Confirm("Start the game without synchronization?", defaultValue: true);
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
                AnsiConsole.Write(new Rule("[yellow]Synchronization[/]"));
                Logger.Info("[gray]Checking local changes...[/]");

                try 
                 {
                     var (owner, repo) = github.ExtractRepoInfo(config.RepoUrl);
                     await syncer.UploadChanges(owner, repo, initialSnapshot, github);
                 }
                 catch (Exception ex)
                 {
                    Logger.Error($"[white on red]×[/] Error sending: {ex}");
                 }
            }
        }
        else
            Logger.Info("[gray]The launch has been canceled or the server has crashed.[/]");

        Console.WriteLine();
        Logger.Info("Press [blue]Enter[/] to exit.");
        Console.ReadLine();
    }
}