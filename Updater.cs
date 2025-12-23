using System.Diagnostics;
using Spectre.Console;

namespace FikaSync;

public class Updater
{
    private readonly GitHubClient _client;
    private readonly Config _config;
    
    private const string UpdateRepo = "fanteeek/fikasync";

    public Updater(GitHubClient client, Config config)
    {
        _client = client;
        _config = config;
    }

    public async Task CheckForUpdates()
    {
        try
        {
            CleanupOldFiles();

            if (!Version.TryParse(_config.AppVersion, out Version? currentVersion))
                currentVersion = new Version(0,0,0);
            
            var releaseInfo = await _client.GetLatestReleaseInfo(UpdateRepo);
            if (releaseInfo == null) return; 

            string tagName = releaseInfo.Value.TagName.TrimStart('v');
            string downloadUrl = releaseInfo.Value.DownloadUrl;

            if (Version.TryParse(tagName, out Version? latestVersion))
            {
                if (latestVersion > currentVersion)
                {
                    Logger.Info(Loc.Tr("Update_Found", latestVersion));

                    if (AnsiConsole.Confirm(Loc.Tr("Update_Ask"), defaultValue: true))
                        await PerformUpdate(downloadUrl);
                }
                else
                {
                    Logger.Info(Loc.Tr("Update_Latest", currentVersion));
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(Loc.Tr("Update_Fail", ex.Message));
        }
    }

    private async Task PerformUpdate(string url)
    {
        string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? "";
        string appDir = AppDomain.CurrentDomain.BaseDirectory;
        string tempArchive = Path.Combine(_config.BaseDir, "update.7z");
        string tempExtractorDir = Path.Combine(_config.BaseDir, "update_temp");

        if (string.IsNullOrEmpty(currentExe)) return;

        void CleanUp()
        {
            try
            {
                if (File.Exists(tempArchive)) File.Delete(tempArchive);
                FileManager.ForceDeleteDirectory(tempExtractorDir);
            } catch {}
        }

        try
        {
            // download
            await AnsiConsole.Status()
                .StartAsync(Loc.Tr("Update_Downloading"), async ctx =>
                {
                    bool success = await _client.DownloadAsset(url, tempArchive);
                    if (!success) throw new Exception("Download failed");
                });
            
            // install
            Logger.Info(Loc.Tr("Update_Extracting"));

            string? sourceUpdateDir = await Task.Run(() => FileManager.Extract7z(tempArchive, tempExtractorDir));

            if (string.IsNullOrEmpty(sourceUpdateDir)) throw new Exception("Failed to extract update archive.");
            
            Logger.Info(Loc.Tr("Update_Install"));

            string oldExe = currentExe + ".old";
            if (File.Exists(oldExe)) File.Delete(oldExe);
            File.Move(currentExe, oldExe);
            FileManager.CopyDirectory(sourceUpdateDir, appDir);

            Logger.Info(Loc.Tr("Update_Success"));
            await Task.Delay(1500);
            AnsiConsole.Clear();

            CleanUp();
            Process.Start(currentExe);
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Logger.Error(Loc.Tr("Update_Fail", ex.Message));

            string oldExe = currentExe + ".old";
            if (File.Exists(oldExe) && !File.Exists(currentExe))
                try {File.Move(oldExe, currentExe); } catch {}
        }
    }

    private void CleanupOldFiles()
    {
        try
        {
            string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            string oldExe = currentExe + ".old";
            if (File.Exists(oldExe)) File.Delete(oldExe);
        }catch {}
    }
}