# 🔄 Fika Profile Sync

![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)
![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![Platform](https://img.shields.io/badge/Platform-Windows-0078D6?style=for-the-badge&logo=windows&logoColor=white)
![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)

**Fika Profile Sync** is an automated tool designed for the **Fika Project** (a multiplayer mod for **SPT Tarkov**). It allows you and your friends to seamlessly synchronize game profiles via a private GitHub repository.

## Features

*   **No Manual File Transfers:** Eliminates the need to manually send save files to the host or transfer them back to clients after playing solo. Everyone's progress is kept up-to-date on the server automatically.
*   **Smart Synchronization:** Automatically handles download and upload of profiles based on file modification time.
*   **Built with Spectre.Console:** Features a modern, beautiful, and interactive terminal interface.

## Prerequisites

1.  A **Private GitHub Repository** to store the profiles.
2.  A **GitHub Fine-grained Personal Access Token** with `Contents: Read and Write` permissions (scoped specifically to your repository).

## Getting Started

### 1. Installation
Download the latest `FikaSync.exe` from the [Releases](https://github.com/fanteeek/fika-profiles-sync/releases) page.
**Important:** Place the executable inside your **root game folder** (right next to `EscapeFromTarkov.exe`).

### 2. Configuration
Create a file named `.env` next to the executable (`.exe`). Open it with Notepad and add your settings:

```env
# Your GitHub Fine-grained Token
GITHUB_PAT=github_pat_...

# The HTTPS URL of your private repository
REPO_URL=https://github.com/YourUsername/Your-Repo-Name
```

## Usage

1.  Run `FikaSync.exe`.
2.  Wait for the tool to sync profiles and launch the server.
3.  Play the game!
4.  When you are done playing, close the game and **press [ENTER]** in the FikaSync console window to upload your progress to the repository.

## Building from Source

If you want to modify the code or build it yourself:

1.  Install **.NET 9.0 SDK**.
2.  Clone the repository:
    ```bash
    git clone https://github.com/fanteeek/fika-profiles-sync.git
    cd fika-profiles-sync
    ```
3.  Run via terminal:
    ```bash
    dotnet run
    ```
4.  Publish as a single EXE:
    ```bash
    dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
    ```

## Troubleshooting

*   **"Server port timeout"**: Ensure your `http.json` or `fika.jsonc` allows connection. The tool tries to parse the port automatically.
*   **"Unauthorized"**: Check your `.env` file and ensure your GitHub Token is valid and has `Contents: Read and Write` permissions.
*   **Debug Mode**: Run the tool with the `-d` flag to see detailed logs about paths and connection:
    ```powershell
    .\FikaSync.exe -d
    ```