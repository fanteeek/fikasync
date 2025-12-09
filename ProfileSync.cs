using System.Security.Cryptography;
using Spectre.Console;

namespace FikaSync;

public class ProfileSync
{
    private readonly Config _config;

    public ProfileSync(Config config)
    {
        _config = config;
    }

    public Dictionary<string, string> GetProfilesSnapshot()
    {
        var snapshot = new Dictionary<string, string>();

        if (Directory.Exists(_config.GameProfilesPath))
        {
            var files = Directory.GetFiles(_config.GameProfilesPath, "*.json");
            foreach(var file in files)
            {
                string hash = GetFileHash(file);
                snapshot[Path.GetFileName(file)] = hash;
            }
        }

        return snapshot;
    }

    public async Task UploadChanges(string owner, string repo, Dictionary<string, string> initialSnapshot, GitHubClient client)
    {
        var currentSnapshot = GetProfilesSnapshot();

        var table = new Table();
        table.Title("Uploading changes");
        table.AddColumn("Profile");
        table.AddColumn("Status");
        table.AddColumn("Result");
        table.Border(TableBorder.Rounded);

        int uploadCount = 0;
        bool hasChanges = false;

        foreach (var file in currentSnapshot)
        {
            string fileName = file.Key;
            string currentHash = file.Value;
            string fullPath = Path.Combine(_config.GameProfilesPath, fileName);

            bool needsUpload = false;
            string statusTag = "";

            if (initialSnapshot.ContainsKey(fileName))
            {
                if (initialSnapshot[fileName] != currentHash)
                {
                    statusTag = "[yellow]Changed[/]";
                    needsUpload = true;
                }
            }
            else
            {
                statusTag = "[green]New[/]";
                needsUpload = true;
            }

            if (needsUpload)
            {
                hasChanges = true;
                byte[] content = await File.ReadAllBytesAsync(fullPath);
                string repoPath = $"profiles/{fileName}"; 
                
                bool success = await client.UploadFile(owner, repo, repoPath, content);
                if (success)
                {
                    table.AddRow(fileName, statusTag, "[green]Sent[/]");
                    uploadCount++;
                }
                else
                {
                    table.AddRow(fileName, statusTag, "[red]Error[/]");
                }
            }
        }

        if (hasChanges)
        {
            AnsiConsole.Write(table);
            if (uploadCount > 0)
            {
                Logger.Info($"[green]Successfully synchronized profiles: {uploadCount}[/]");
            }
        }
        else
        {
            Logger.Info("[gray]There are no local changes. Everything is synchronized.[/]");
        }
    }

    private string GetFileHash(string filePath)
    {
        if (!File.Exists(filePath)) return string.Empty;

        using (var sha256 = SHA256.Create())
        {
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hashBytes = sha256.ComputeHash(stream);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }
    }

    public void SyncProfiles(string extractedFolder, List<string> downloadedFiles)
    {
        var table = new Table();
        table.AddColumn("File");
        table.AddColumn("Status");
        table.AddColumn("Action");

        int updatedCount = 0;

        if (!Directory.Exists(_config.GameProfilesPath))
        {
            Directory.CreateDirectory(_config.GameProfilesPath);
        }

        foreach (var downloadedFile in downloadedFiles)
        {
            string fileName = Path.GetFileName(downloadedFile);
            string localFile = Path.Combine(_config.GameProfilesPath, fileName);

            string remoteHash = GetFileHash(downloadedFile);
            string localHash = GetFileHash(localFile);

            if (localHash == remoteHash)
            {
                table.AddRow(fileName, "[green]Relevant[/]", "[gray]Pass[/]");
                continue;
            }

            if (File.Exists(localFile))
            {
                DateTime localTime = File.GetLastWriteTimeUtc(localFile);
                DateTime remoteTime = File.GetLastWriteTimeUtc(downloadedFile);

                if (remoteTime <= localTime.AddSeconds(2))
                {
                    table.AddRow(fileName, "[blue]Local newer[/]", "[yellow]Will be sent[/]");
                    continue;
                }
            }

            try
            {
                if (File.Exists(localFile))
                {
                    CreateBackup(localFile);
                }

                File.Copy(downloadedFile, localFile, true);
                
                var originalTime = File.GetLastWriteTime(downloadedFile);
                File.SetLastWriteTime(localFile, originalTime);

                table.AddRow(fileName, "[yellow]Updated[/]", "[blue]Downloaded from GitHub[/]");
                updatedCount++;
            }
            catch (Exception ex)
            {
                table.AddRow(fileName, "[red]Error[/]", ex.Message);
            }
        }

        AnsiConsole.Write(table);

        if (updatedCount > 0)
        {
            Logger.Info($"[green]Profiles successfully updated: {updatedCount}[/]");
        }
        else
        {
            Logger.Info("[gray]All profiles are current/newer, no updates.[/]");
        }
    }

    private void CreateBackup(string filePath)
    {
        try
        {
            string fileName = Path.GetFileName(filePath);
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string backupDir = Path.Combine(_config.BaseDir, "backups", timestamp);

            if (!Directory.Exists(backupDir))
                Directory.CreateDirectory(backupDir);

            string destFile = Path.Combine(backupDir, fileName);
            File.Copy(filePath, destFile);
        }
        catch
        {
            Logger.Error($"[white on red]×[/] Failed to create backup for {Path.GetFileName(filePath)}");
        }
    }
}