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
        table.Title("Выгрузка изменений (Upload)");
        table.AddColumn("Профиль");
        table.AddColumn("Статус");
        table.AddColumn("Результат");
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
                    statusTag = "[yellow]Изменен[/]";
                    needsUpload = true;
                }
            }
            else
            {
                statusTag = "[green]Новый[/]";
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
                    table.AddRow(fileName, statusTag, "[green]Отправлен[/]");
                    uploadCount++;
                }
                else
                {
                    table.AddRow(fileName, statusTag, "[red]Ошибка[/]");
                }
            }
        }

        if (hasChanges)
        {
            AnsiConsole.Write(table);
            if (uploadCount > 0)
            {
                Logger.Info($"[green]Успешно синхронизировано профилей: {uploadCount}[/]");
            }
        }
        else
        {
            Logger.Info("[gray]Локальных изменений нет. Всё синхронизировано.[/]");
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
        table.AddColumn("Файл");
        table.AddColumn("Статус");
        table.AddColumn("Действие");

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
                table.AddRow(fileName, "[green]Актуален[/]", "[gray]Пропуск[/]");
                continue;
            }

            if (File.Exists(localFile))
            {
                DateTime localTime = File.GetLastWriteTimeUtc(localFile);
                DateTime remoteTime = File.GetLastWriteTimeUtc(downloadedFile);

                if (remoteTime <= localTime.AddSeconds(2))
                {
                    table.AddRow(fileName, "[blue]Локальный новее[/]", "[yellow]Будет отправлен[/]");
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

                table.AddRow(fileName, "[yellow]Обновлен[/]", "[blue]Загружен с GitHub[/]");
                updatedCount++;
            }
            catch (Exception ex)
            {
                table.AddRow(fileName, "[red]Ошибка[/]", ex.Message);
            }
        }

        AnsiConsole.Write(table);

        if (updatedCount > 0)
        {
            Logger.Info($"[green]Успешно обновлено профилей: {updatedCount}[/]");
        }
        else
        {
            Logger.Info("[gray]Все профили актуальны/новее, обновлений нет.[/]");
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
            Logger.Error($"[white on red]×[/] Не удалось создать бэкап для {Path.GetFileName(filePath)}");
        }
    }
}