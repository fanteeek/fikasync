using Spectre.Console;

namespace FikaSync;

public static class Logger
{
    public static bool IsDebugEnabled { get; private set; } = false;
    private static string _logFilePath = string.Empty;

    static Logger()
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string logDir = Path.Combine(baseDir, "logs");

            if (!Directory.Exists(logDir))
                Directory.CreateDirectory(logDir);

            _logFilePath = Path.Combine(logDir, "fikasync.log");

            File.WriteAllText(_logFilePath, $"--- Log Started: {DateTime.Now} ---\n");
        }
        catch
        {}
    }

    public static void Enable()
    {
        IsDebugEnabled = true;
    }

    public static void Info(string message)
    {
        AnsiConsole.MarkupLine(message);
        LogToFile("INFO", message);
    } 

    public static void Debug(string message)
    {
        LogToFile("DEBUG", message);

        if (IsDebugEnabled)
        {
            AnsiConsole.MarkupLine($"[grey][[DEBUG]] {message}[/]");
        }
    }

    public static void Error(string message)
    {
        AnsiConsole.MarkupLine($"[red]{message}[/]");
        LogToFile("ERROR", message);
    }

    private static void LogToFile(string level, string message)
    {
        try
        {
            if (string.IsNullOrEmpty(_logFilePath)) return;

            string cleanMessage = message;
            try 
            {
                cleanMessage = Markup.Remove(message); 
            }
            catch {}

            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string line = $"[{timestamp}] [{level}] {cleanMessage}\n";

            File.AppendAllText(_logFilePath, line);
        }
        catch
        {}
    }
}