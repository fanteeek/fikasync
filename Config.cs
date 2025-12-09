using System.ComponentModel;
using System.Reflection;
using DotNetEnv;

namespace FikaSync;

public class Config
{
    public string GithubToken { get; private set; } = string.Empty;
    public string RepoUrl { get; private set; } = string.Empty;
    
    public string BaseDir { get; private set; }
    public string GameProfilesPath { get; private set; }
    public string SptServerPath { get; private set; }
    public string SptLauncherPath { get; private set; }
    public string AppVersion { get; private set;} = "0.0.0";
    
    public Config()
    {
        // Version
        var v = Assembly.GetEntryAssembly()?.GetName().Version;
        if (v != null)
            AppVersion = $"{v.Major}.{v.Minor}.{v.Build}";

        // BaseDir
        var assembly = Assembly.GetEntryAssembly();
        if (assembly == null) { BaseDir = Directory.GetCurrentDirectory(); }
        else { BaseDir = Path.GetDirectoryName(assembly.Location) ?? Directory.GetCurrentDirectory(); }

        LoadEnvironment();

        GameProfilesPath = Path.Combine(BaseDir, "SPT", "user", "profiles");
        SptServerPath = Path.Combine(BaseDir, "SPT", "SPT.Server.exe");
        SptLauncherPath = Path.Combine(BaseDir, "SPT", "SPT.Launcher.exe");
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

    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(GithubToken) && 
               !string.IsNullOrWhiteSpace(RepoUrl);
    }
}