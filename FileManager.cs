using System.IO.Compression;
using Spectre.Console;

namespace FikaSync;

public static class FileManager
{
    public static void ForceDeleteDirectory(string path)
    {
        if (!Directory.Exists(path)) return;

        try
        {
            Directory.Delete(path, true);
        }
        catch (Exception ex)
        {
            Logger.Error($"[white on red]×[/] Не удалось удалить папку {path}: {ex.Message}");
        }
    }

    public static string? ExtractZip(string zipPath, string extractTo)
    {
        try
        {
            ForceDeleteDirectory(extractTo);
            ZipFile.ExtractToDirectory(zipPath, extractTo);
            var extractedDirs = Directory.GetDirectories(extractTo);
            if (extractedDirs.Length > 0)
                return extractedDirs[0];
            return extractTo;
        }
        catch (Exception ex)
        {
            Logger.Error($"[white on red]×[/] Ошибка распаковки: {ex.Message}");
            return null;
        }
    }

    public static List<string> FindProfiles(string rootPath)
    {
        var profiles = new List<string>();
        var allFiles = Directory.GetFiles(rootPath, "*.json", SearchOption.AllDirectories);
        foreach (var file in allFiles)
        {
            profiles.Add(file);
        }

        return profiles;
    }
}