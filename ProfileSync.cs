using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Spectre.Console;

namespace FikaSync;

public struct ProfileState
{
    public string Hash;
    public long Timestamp;
}

public class ProfileSync
{
    private readonly Config _config;

    public ProfileSync(Config config)
    {
        _config = config;
    }

    private long GetProfileTimestamp(string jsonContent)
    {
        try
        {
            if (string.IsNullOrEmpty(jsonContent)) return 0;
            var node = JsonNode.Parse(jsonContent);
            return node?["characters"]?["pmc"]?["Hideout"]?["sptUpdateLastRunTimestamp"]?.GetValue<long>() ?? 0;
        }
        catch { return 0; }
    }

    public Dictionary<string, ProfileState> GetProfilesSnapshot()
    {
        var snapshot = new Dictionary<string, ProfileState>();
        if (Directory.Exists(_config.GameProfilesPath))
        {
            foreach(var file in Directory.GetFiles(_config.GameProfilesPath, "*.json"))
            {
                string content = File.ReadAllText(file);
                snapshot[Path.GetFileName(file)] = new ProfileState
                {
                    Hash = GetFileHash(file),
                    Timestamp = GetProfileTimestamp(content)
                };
            }
        }

        return snapshot;
    }

    public async Task UploadChanges(string owner, string repo, Dictionary<string, ProfileState> initialSnapshot, GitHubClient client, List<string> pendingUploads)
    {

        var table = new Table();
        table.Title("Uploading changes").AddColumn("Profile").AddColumn("Status").AddColumn("Result").Border(TableBorder.Rounded);

        var currentSnapshot = GetProfilesSnapshot();
        int uploadCount = 0;
        bool hasChanges = false;

        foreach (var file in currentSnapshot)
        {
            string fileName = file.Key;
            var currentState = file.Value;
            bool needsUpload = false;
            string statusTag = "";
            bool changedDuringSession = false;

            if (initialSnapshot.ContainsKey(fileName))
            {
                var initialState = initialSnapshot[fileName];
                if (initialState.Hash != currentState.Hash && currentState.Timestamp >= initialState.Timestamp)
                    changedDuringSession = true;
            }
            else changedDuringSession = true;

            if (changedDuringSession)
            {
                statusTag = "[green]New Progress[/]";
                needsUpload = true;
            }else if (pendingUploads.Contains(fileName))
            {
                Logger.Info($"[gray]Verifying status for pending file: {fileName}...[/]");

                string repoPath = $"profiles/{fileName}";
                byte[]? remoteBytes = await client.DownloadFileContent(owner, repo, repoPath);

                long remoteTs = 0;
                if (remoteBytes != null)
                {
                    string remoteJson = Encoding.UTF8.GetString(remoteBytes);
                    remoteTs = GetProfileTimestamp(remoteJson);
                }

                if (currentState.Timestamp > remoteTs)
                {
                    statusTag = "[blue]Sync Pending[/]";
                    needsUpload = true;
                    Logger.Debug($"Safe to upload pending {fileName}. Local: {currentState.Timestamp} > Remote: {remoteTs}");
                }
                else
                {
                    table.AddRow(fileName.EscapeMarkup(), "[red]Conflict[/]", "[gray]Skipped (Remote became newer)[/]");
                    Logger.Debug($"Skipping pending upload for {fileName}. Remote was updated during session!");
                    needsUpload = false;
                }

            }

            if (needsUpload)
            {
                hasChanges = true;
                string fullPath = Path.Combine(_config.GameProfilesPath, fileName);
                string repoPath = $"profiles/{fileName}"; 
                byte[] content = await File.ReadAllBytesAsync(fullPath);
                
                if(await client.UploadFile(owner, repo, repoPath, content))
                {
                    table.AddRow(fileName.EscapeMarkup(), statusTag, "[green]Sent[/]");
                    uploadCount++;
                }
                else
                {
                    table.AddRow(fileName.EscapeMarkup(), statusTag, "[red]Error[/]");
                }
            }
        }

        if (hasChanges) AnsiConsole.Write(table);
        else Logger.Info("[gray]No changes to upload.[/]");
            
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

    public List<string> SyncProfiles(string extractedFolder, List<string> downloadedFiles)
    {
        var table = new Table();
        table.AddColumn("File").AddColumn("Status").AddColumn("Action");
        
        var pendingUploads = new List<string>();
        int updatedCount = 0;

        if (!Directory.Exists(_config.GameProfilesPath)) Directory.CreateDirectory(_config.GameProfilesPath);

        foreach (var downloadedFile in downloadedFiles)
        {
            string fileName = Path.GetFileName(downloadedFile);
            string localFile = Path.Combine(_config.GameProfilesPath, fileName);
            
            string remoteContent = File.ReadAllText(downloadedFile);
            long remoteTs = GetProfileTimestamp(remoteContent);

            long localTs = 0;
            if (File.Exists(localFile))
            {
                string localContent = File.ReadAllText(localFile);
                localTs = GetProfileTimestamp(localContent);
            }

            if (GetFileHash(localFile) == GetFileHash(downloadedFile))
            {
                table.AddRow(fileName, "[green]Relevant[/]", "[gray]Pass[/]");
                continue;
            }
            
            Logger.Debug($"File: {fileName} | LocalTS: {localTs} | RemoteTS: {remoteTs}");

            if (localTs > remoteTs && localTs > 0)
            {
                pendingUploads.Add(fileName);
                table.AddRow(fileName, "[blue]Local newer[/]", "[yellow]Pending Upload[/]");
                continue;
            }

            try
            {
                if (File.Exists(localFile)) CreateBackup(localFile);

                File.Copy(downloadedFile, localFile, true);
                var originalTime = File.GetLastWriteTime(downloadedFile);
                File.SetLastWriteTime(localFile, originalTime);

                table.AddRow(fileName, "[yellow]Updated[/]", "[blue]Downloaded[/]");
                updatedCount++;
            }
            catch (Exception ex)
            {
                table.AddRow(fileName, "[red]Error[/]", ex.Message);
            }
        }

        AnsiConsole.Write(table);

        if (updatedCount > 0) Logger.Info($"[green]Profiles successfully updated: {updatedCount}[/]");
        else Logger.Info("[gray]All profiles are current/newer, no updates.[/]");

        return pendingUploads;
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
            Logger.Error($"Failed to create backup for {Path.GetFileName(filePath)}");
        }
    }
}