using System.Reflection;
using Spectre.Console;

namespace FikaSync;

public class Updater
{
    private readonly GitHubClient _client;
    private readonly Config _config;
    
    private const string UpdateRepo = "fanteeek/fika-profiles-sync";

    public Updater(GitHubClient client, Config config)
    {
        _client = client;
        _config = config;
    }

    public async Task CheckForUpdates()
    {
        try
        {
            if (!Version.TryParse(_config.AppVersion, out Version? currentVersion))
                currentVersion = new Version(0,0,0);
            
            string url = $"/repos/{UpdateRepo}/releases/latest";
            var releaseInfo = await _client.GetLatestReleaseInfo(UpdateRepo);
            if (releaseInfo == null) return; 

            string tagName = releaseInfo.Value.TagName.TrimStart('v');
            string htmlUrl = releaseInfo.Value.HtmlUrl;

            if (Version.TryParse(tagName, out Version? latestVersion))
            {
                if (latestVersion > currentVersion)
                {
                    var panel = new Panel(
                        $"[yellow]New version available:[/] [green]v{latestVersion}[/]\n" +
                        $"Your version: [gray]v{currentVersion}[/]\n\n" +
                        $"Download: [blue underline]{htmlUrl}[/]"
                    );
                    panel.Header = new PanelHeader("[bold red]UPDATE AVAILABLE[/]");
                    panel.Border = BoxBorder.Double;
                    panel.Padding = new Padding(2, 1, 2, 1);
                    AnsiConsole.WriteLine();
                    AnsiConsole.Write(panel);
                    AnsiConsole.WriteLine();
                }
                else
                {
                    Logger.Info($"[gray]The program version is up to date. (v{currentVersion})[/]");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Unable to check for updates: {ex.Message}");
        }
    }
}