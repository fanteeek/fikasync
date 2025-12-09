using System.Reflection;
using System.Text;
using DotNetEnv;
using Spectre.Console;

namespace FikaSync;

public class Config
{
    public string GithubToken { get; private set; } = string.Empty;
    public string RepoUrl { get; private set; } = string.Empty;
    public string AppVersion { get; private set;} = "0.0.0";
    
    public string BaseDir { get; private set; }
    public string GameProfilesPath { get; private set; }
    public string SptServerPath { get; private set; }
    public string SptLauncherPath { get; private set; }
    
    public Config()
    {
        // Version
        var v = Assembly.GetEntryAssembly()?.GetName().Version;
        if (v != null) AppVersion = $"{v.Major}.{v.Minor}.{v.Build}";

        // BaseDir
        BaseDir = AppContext.BaseDirectory;

        GameProfilesPath = Path.Combine(BaseDir, "SPT", "user", "profiles");
        SptServerPath = Path.Combine(BaseDir, "SPT", "SPT.Server.exe");
        SptLauncherPath = Path.Combine(BaseDir, "SPT", "SPT.Launcher.exe");

        LoadEnvironment();
    }

    private void LoadEnvironment()
    {
        string envPath = Path.Combine(BaseDir, ".env");
        
        if (File.Exists(envPath))
        {
            Env.Load(envPath);
        }

        GithubToken = Environment.GetEnvironmentVariable("GITHUB_PAT") ?? ""; // ?? "" значит "если null, то верни пустую строку"
        RepoUrl = Environment.GetEnvironmentVariable("REPO_URL") ?? "";
    }

    public void EnsureConfiguration()
    {
        bool configChanged = false;

        // check token
        if (string.IsNullOrWhiteSpace(GithubToken))
        {
            Logger.Info("[yellow]![/] GitHub Token not found.");

            GithubToken = AnsiConsole.Prompt(
                new TextPrompt<string>("Enter your [green]GitHub PAT[/]:")
                    .Secret()
                    .Validate(token => 
                        token.Length > 10 
                            ? ValidationResult.Success() 
                            : ValidationResult.Error("[white on red]×[/] The token is too short.!")));
            
            UpdateEnvFile("GITHUB_PAT", GithubToken);
            configChanged = true;
        }

        // check Url
        if (string.IsNullOrWhiteSpace(RepoUrl))
        {
            Logger.Info("[yellow]![/] Repository URL not found.");
            
            RepoUrl = AnsiConsole.Prompt(
                new TextPrompt<string>("Enter [green]HTTPS URL[/] repositories:")
                    .Validate(url => 
                        url.StartsWith("https://github.com/") 
                            ? ValidationResult.Success() 
                            : ValidationResult.Error("[white on red]×[/] The link must begin with https://github.com/")));

            UpdateEnvFile("REPO_URL", RepoUrl);
            configChanged = true;
        }

        if (configChanged)
        {
            Logger.Info("[green]√[/] Settings are saved in the .env file.");
            AnsiConsole.WriteLine();
        }
    }

    private void UpdateEnvFile(string key, string value)
    {
        try
        {
            string envPath = Path.Combine(BaseDir, ".env");
            var lines = new List<string>();

            if (File.Exists(envPath))
            {
                lines = File.ReadAllLines(envPath).ToList();
            }

            int index = lines.FindIndex(l => l.StartsWith($"{key}="));
            string newLine = $"{key}={value}";

            if (index != -1)
                lines[index] = newLine;
            else
                lines.Add(newLine);

            File.WriteAllLines(envPath, lines, Encoding.UTF8);
            Environment.SetEnvironmentVariable(key, value);
        }
        catch (Exception ex)
        {
            Logger.Error($"[white on red]×[/] Error writing .env: {ex.Message}");
        }
    }

    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(GithubToken) && 
               !string.IsNullOrWhiteSpace(RepoUrl);
    }
}