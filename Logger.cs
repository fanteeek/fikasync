using Spectre.Console;

namespace FikaSync;

public static class Logger
{
    public static bool IsDebugEnabled { get; private set; } = false;

    public static void Enable()
    {
        IsDebugEnabled = true;
    }

    public static void Log(string message)
    {
        if (!IsDebugEnabled) return;

        AnsiConsole.MarkupLine($"[grey][[DEBUG]] {message}[/]");
    }
    
}