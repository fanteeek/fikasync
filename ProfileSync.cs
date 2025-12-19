using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Spectre.Console;

namespace FikaSync;

public class ProfileSync
{
    private readonly Config _config;
    private readonly GitHubClient _client;
    
    private readonly HashSet<string> _pendingUploads = new();
    
    private Dictionary<string, long> _sessionStartTimestamps = new();

    private readonly HashSet<string> _ignoredPatterns = new(StringComparer.OrdinalIgnoreCase);

    public ProfileSync(Config config, GitHubClient client)
    {
        _config = config;
        _client = client;
        LoadIgnoreList();
    }

    private void LoadIgnoreList()
    {
        string ignorePath = Path.Combine(_config.BaseDir, ".fikaignore");

        if (!File.Exists(ignorePath))
        {
            try
            {
                File.WriteAllText(ignorePath,
                    "# FikaSync Ignore List\n" +
                    "# Write the file names you want to ignore here (one per line).\n" +
                    "# Lines starting with # are comments.\n" +
                    "# Example:\n" +
                    "# Tested_Profile.json\n");
            } catch {}
        }

        if (File.Exists(ignorePath))
        {
            try
            {
                var lines = File.ReadAllLines(ignorePath);
                foreach (var line in lines)
                {
                    var clean = line.Trim();
                    if (!string.IsNullOrEmpty(clean) && !clean.StartsWith("#"))
                    {
                        _ignoredPatterns.Add(clean);
                    }
                }
                if (_ignoredPatterns.Count > 0)
                    Logger.Debug(Loc.Tr("Ignore_Loaded", _ignoredPatterns.Count));
            }
            catch {}
        }
    }

    public async Task PerformStartupSync(string owner, string repo)
    {
        string tempZip = Path.Combine(_config.BaseDir, "temp", "repo.zip");
        string extractPath = Path.Combine(_config.BaseDir, "temp", "extracted");
        string downloadUrl = $"/repos/{owner}/{repo}/zipball";

        try
        {
            bool downloaded = false;

            await AnsiConsole.Status()
                .StartAsync(Loc.Tr("Sync_Downloading"), async ctx =>
                {
                    downloaded = await _client.DownloadRepository(owner, repo, tempZip);
                });

            if (!downloaded) throw new Exception(Loc.Tr("Result_Error"));

            string? contentDir = FileManager.ExtractZip(tempZip, extractPath);

            var remoteFiles = contentDir != null ? FileManager.FindProfiles(contentDir) : new List<string>();

            remoteFiles.RemoveAll(f => _ignoredPatterns.Contains(Path.GetFileName(f)));

            if (remoteFiles.Count == 0)
                Logger.Info(Loc.Tr("Sync_NoProfiles"));
            else
                Logger.Info(Loc.Tr("Sync_Found", remoteFiles.Count));

            ProcessDownloadedFiles(remoteFiles);
        }
        finally
        {
            FileManager.ForceDeleteDirectory(Path.Combine(_config.BaseDir, "temp"));
        }
    }

    public void CaptureSessionStartSnapshot()
    {
        _sessionStartTimestamps.Clear();
        if (!Directory.Exists(_config.GameProfilesPath)) return;

        foreach (var file in Directory.GetFiles(_config.GameProfilesPath, "*.json"))
        {   
            string fileName = Path.GetFileName(file);
            if (_ignoredPatterns.Contains(Path.GetFileName(fileName)))
            {
                Logger.Debug(Loc.Tr("Ignore_File", fileName));
                continue;
            }
            string content = File.ReadAllText(file);
            _sessionStartTimestamps[fileName] = GetTimestamp(content);
        }
    }

    public async Task PerformShutdownSync(string owner, string repo)
    {
        Logger.Info(Loc.Tr("Sync_Checking"));
        
        var table = new Table();
        table.Title(Loc.Tr("Sync_Report_Title")).AddColumn(Loc.Tr("Sync_Profile_Title")).AddColumn(Loc.Tr("Sync_Reason_Title")).AddColumn(Loc.Tr("Sync_Result_Title")).Border(TableBorder.Rounded);

        if (!Directory.Exists(_config.GameProfilesPath))
        {
            Logger.Info(Loc.Tr("Sync_NoLocal"));
            return;
        }

        var localFiles = Directory.GetFiles(_config.GameProfilesPath, "*.json");
        bool hasActivity = false;
        int sentCount = 0;

        foreach (var file in localFiles)
        {
            string fileName = Path.GetFileName(file);

            if (_ignoredPatterns.Contains(fileName))
            {
                Logger.Debug(Loc.Tr("Ignore_File", fileName));
                continue;
            }

            string content = File.ReadAllText(file);
            long currentTs = GetTimestamp(content);

            bool shouldUpload = false;
            string reason = "";

            long startTs = _sessionStartTimestamps.ContainsKey(fileName) ? _sessionStartTimestamps[fileName] : 0;
            
            if (currentTs > startTs)
            {
                shouldUpload = true;
                reason = Loc.Tr("Reason_NewProgress");
            }
            else if (_pendingUploads.Contains(fileName))
            {
                if (await IsSafeToUploadPending(owner, repo, fileName, currentTs))
                {
                    shouldUpload = true;
                    reason = Loc.Tr("Reason_Pending");
                }
                else
                {
                    table.AddRow(fileName, Loc.Tr("Result_Conflict"), Loc.Tr("Result_RemoteNewer"));
                    continue;
                }
            }

            if (shouldUpload)
            {
                hasActivity = true;
                byte[] bytes = await File.ReadAllBytesAsync(file);
                string repoPath = $"profiles/{fileName}";

                if (await _client.UploadFile(owner, repo, repoPath, bytes))
                {
                    table.AddRow(fileName, reason, Loc.Tr("Result_Sent"));
                    sentCount++;
                }
                else
                {
                    table.AddRow(fileName, reason, Loc.Tr("Result_Error"));
                }
            }
        }

        if (hasActivity) AnsiConsole.Write(table);
        else Logger.Info(Loc.Tr("Sync_AllDone"));
    }

    private void ProcessDownloadedFiles(List<string> remoteFiles)
    {
        var table = new Table().AddColumn(Loc.Tr("Table_File")).AddColumn(Loc.Tr("Table_Status")).AddColumn(Loc.Tr("Table_Action"));
        int updated = 0;

        if (!Directory.Exists(_config.GameProfilesPath)) Directory.CreateDirectory(_config.GameProfilesPath);

        var processedFiles = new HashSet<string>();

        foreach (var remotePath in remoteFiles)
        {
            string fileName = Path.GetFileName(remotePath);
            processedFiles.Add(fileName);
            string localPath = Path.Combine(_config.GameProfilesPath, fileName);

            string remoteHash = GetFileHash(remotePath);
            string localHash = File.Exists(localPath) ? GetFileHash(localPath) : "";

            string remoteContent = File.ReadAllText(remotePath);
            long remoteTs = GetTimestamp(remoteContent);

            long localTs = 0;
            if (File.Exists(localPath))
            {
                string localContent = File.ReadAllText(localPath);
                localTs = GetTimestamp(localContent);
            }
            
            Logger.Debug($"Локальный хеш: {localHash}");
            Logger.Debug($"Удаленный хеш: {remoteHash}");
            Logger.Debug($"Локальное время: {localTs}");
            Logger.Debug($"Удаленное время: {remoteTs}");
            Logger.Debug($"localTs > remoteTs: {localTs > remoteTs}");

            if (localHash == remoteHash || localTs == remoteTs)
            {
                table.AddRow(fileName, Loc.Tr("Status_Synced"), Loc.Tr("Action_Pass"));
                Logger.Debug($"File: {fileName}  |  Status:  {Loc.Tr("Status_Synced")}  |  Action: {Loc.Tr("Action_Pass")}");
                continue;
            }

            if (localTs > remoteTs)
            {
                _pendingUploads.Add(fileName);
                table.AddRow(fileName, Loc.Tr("Status_LocalNewer"), Loc.Tr("Action_WillUpload"));
                Logger.Debug($"File: {fileName}  |  Status:  {Loc.Tr("Status_LocalNewer")}  |  Action: {Loc.Tr("Action_WillUpload")}");
            }
            else
            {
                ApplyUpdate(remotePath, localPath);
                table.AddRow(fileName, Loc.Tr("Status_Update"), Loc.Tr("Action_Downloaded"));
                Logger.Debug($"File: {fileName}  |  Status:  {Loc.Tr("Status_Update")}  |  Action: {Loc.Tr("Action_Downloaded")}");
                updated++;
            }
        }

        var localFiles = Directory.GetFiles(_config.GameProfilesPath, "*.json");
        foreach(var file in localFiles)
        {
            string name = Path.GetFileName(file);

            if (_ignoredPatterns.Contains(name))
            {
                Logger.Debug(Loc.Tr("Ignore_File", name));
                continue;
            }

            if (!processedFiles.Contains(name))
            {
                _pendingUploads.Add(name);
                table.AddRow(name, Loc.Tr("Status_NewLocal"), Loc.Tr("Action_WillUpload"));
                Logger.Debug($"File: {name}  |  Status:  {Loc.Tr("Status_NewLocal")}  |  Action: {Loc.Tr("Action_WillUpload")}");
            }
        }
        AnsiConsole.WriteLine();
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        if (updated > 0) Logger.Info(Loc.Tr("Sync_Updated_Count", updated));
    }

    private void ApplyUpdate(string source, string dest)
    {
        try
        {
            if (File.Exists(dest)) CreateBackup(dest);
            File.Copy(source, dest, true);
            
            var dt = File.GetLastWriteTime(source);
            File.SetLastWriteTime(dest, dt);
        }
        catch (Exception ex)
        {
            Logger.Error(Loc.Tr("Result_Error", ex.Message));
        }
    }

    private async Task<bool> IsSafeToUploadPending(string owner, string repo, string fileName, long localTs)
    {
        AnsiConsole.MarkupLine(Loc.Tr("Verify_Remote"), fileName);
        
        byte[]? remoteBytes = await _client.DownloadFileContent(owner, repo, $"profiles/{fileName}");
        
        if (remoteBytes == null) return true;

        try
        {
            string remoteJson = Encoding.UTF8.GetString(remoteBytes);
            long remoteTs = GetTimestamp(remoteJson);
            
            Logger.Debug($"Verify {fileName}: LocalTS={localTs}, RemoteTS={remoteTs}");

            return localTs > remoteTs;
        }
        catch
        {
            return false; 
        }
    }

    private long GetTimestamp(string jsonContent)
    {
        try
        {
            if (string.IsNullOrEmpty(jsonContent)) return 0;
            var node = JsonNode.Parse(jsonContent);
            return node?["characters"]?["pmc"]?["Hideout"]?["sptUpdateLastRunTimestamp"]?.GetValue<long>() ?? 0;
        }
        catch { return 0; }
    }

    private string GetFileHash(string filePath)
    {
        if (!File.Exists(filePath)) return string.Empty;
        try
        {
            string content = File.ReadAllText(filePath);
            string normalized = content.Replace("\r\n", "\n").Replace("\r", "\n");
            using var sha = SHA256.Create();
            byte[] bytes = Encoding.UTF8.GetBytes(normalized);
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        catch
        {
            return string.Empty;
        }
    }

    private void CreateBackup(string filePath)
    {
        try
        {
            string fileName = Path.GetFileName(filePath);
            string ts = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string backupRoot = Path.Combine(_config.BaseDir, "backups", fileName);
            string backupTS = Path.Combine(backupRoot, ts);
            Directory.CreateDirectory(backupTS);
            File.Copy(filePath, Path.Combine(backupTS, fileName));
            Logger.Info(Loc.Tr("Sync_Backup", fileName));
            CleanOldBackups(backupRoot);
        }
        catch
        {
            Logger.Error(Loc.Tr("Sync_Backup_Failed"));
        }
    }

    private void CleanOldBackups(string backupRoot)
    {
        if (!Directory.Exists(backupRoot)) return;

        var backupDirs = Directory.GetDirectories(backupRoot)
            .Select(dir => new DirectoryInfo(dir))
            .Where(dir => DateTime.TryParseExact(
                dir.Name,
                "yyyy-MM-dd_HH-mm-ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _))
            .OrderByDescending(dir => dir.Name)
            .ToList();

        if (backupDirs.Count > 5)
        {
            var oldBackups = backupDirs.Skip(5);
            foreach (var oldDir in oldBackups)
            {
                try
                {
                    Directory.Delete(oldDir.FullName, recursive: true);
                    Logger.Debug(Loc.Tr("Sync_Backup_DeletedOld", oldDir.Name));
                }
                catch (Exception ex)
                {
                    Logger.Error(Loc.Tr("Sync_Backup_Failed_DeletedOld",oldDir.Name, ex.Message));
                }
            }
        }
    }
}