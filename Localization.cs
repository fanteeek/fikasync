using System.Globalization;
using System.Linq.Expressions;

namespace FikaSync;

public static class Loc
{
    private static Dictionary<string, string> _currentStrings;

    // Словари для разных языков
    private static readonly Dictionary<string, string> _en = new()
    {
        // Program & General
        {"App_Started", "Application started. Args: {0}"},
        {"Debug_Enabled", "Debug mode enabled via arguments"},
        {"Config_Loading", "Loading configuration..."},
        {"Setup_Incomplete", "Setup not complete. Exit."},
        {"Conn_GitHub", "Connecting to GitHub..."},
        {"Repo_Target", "Target repository: [blue]{0}/{1}[/]" },
        {"Offline_Mode", "[yellow]![/] Offline mode (GitHub not reachable)."},
        {"Start_Game_Question", "Start the game?"},
        {"Start_Game_NoSync", "Start the game without synchronization?"},
        {"Launch_Canceled", "[gray]Launch canceled.[/]"},
        {"Press_Enter", "Press [blue]Enter[/] to exit."},
        
        // Config
        {"Token_NotFound", "[yellow]![/] GitHub Token not found."},
        {"Token_Prompt", "Enter your [green]GitHub PAT[/]:"},
        {"Token_Invalid", "[white on red]×[/] The token is too short!"},
        {"Url_NotFound", "[yellow]![/] Repository URL not found."},
        {"Url_Prompt", "Enter [green]HTTPS URL[/] repository:"},
        {"Url_Invalid", "[white on red]×[/] The link must begin with https://github.com/"},
        {"Config_Saved", "[green]√[/] Settings are saved in the .env file."},
        {"Env_Error", "[white on red]×[/] Error writing .env: {0}"},

        // Updater
        {"Update_Available", "[bold red]UPDATE AVAILABLE[/]"},
        {"Update_Body", "[yellow]New version available:[/] [green]v{0}[/]\nYour version: [gray]v{1}[/]\n\nDownload: [blue underline]{2}[/]"},
        {"Update_Latest", "[gray]The program version is up to date. (v{0})[/]"},

        // ProfileSync (Startup)
        {"Sync_Downloading", "[gray]Downloading cloud profiles...[/]"},
        {"Sync_NoProfiles", "[yellow]![/] No profiles found in the cloud (Repository is empty)."},
        {"Sync_Found", "[bold]Profiles in cloud:[/] {0}"},
        {"Sync_Updated_Count", "[green]Updated {0} profiles from cloud.[/]"},
        
        // ProfileSync (Table)
        {"Table_File", "File"},
        {"Table_Status", "Status"},
        {"Table_Action", "Action"},
        {"Status_Synced", "[green]Synced[/]"},
        {"Status_LocalNewer", "[blue]Local Newer[/]"},
        {"Status_NewLocal", "[green]New Local[/]"},
        {"Status_Update", "[yellow]Update[/]"},
        {"Action_Pass", "[gray]-[/]"},
        {"Action_WillUpload", "[yellow]Will Upload Later[/]"},
        {"Action_Downloaded", "[green]Downloaded[/]"},
        
        // ProfileSync (Shutdown)
        {"Sync_Title", "[yellow]Synchronization[/]"},
        {"Sync_Checking", "[gray]Checking for changes to upload...[/]"},
        {"Sync_Report_Title", "Synchronization Report"},
        {"Sync_Profile_Title", "Profile"}, 
        {"Sync_Reason_Title", "Reasone"}, 
        {"Sync_Result_Title", "Result"},
        {"Sync_NoLocal", "[gray]No local profiles found.[/]"},
        {"Reason_NewProgress", "[green]New Progress[/]"},
        {"Reason_Pending", "[blue]Pending Sync[/]"},
        {"Result_Conflict", "[red]Conflict[/]"},
        {"Result_RemoteNewer", "Remote is newer now"},
        {"Result_Sent", "[green]Sent[/]"},
        {"Result_Error", "[red]Error: {0}[/]"},
        {"Sync_AllDone", "[gray]Everything is synchronized.[/]"},
        {"Verify_Remote", "[gray]Verifying remote version: {0}...[/]"},

        // GameLauncher
        {"Server_NotFound", "Server file not found: {0}"},
        {"Config_Found", "[gray]Configuration found:[/] {0} -> [blue]{1}:{2}[/]"},
        {"Config_Default", "[gray]Configs not found, using default:[/]{0}:{1}"},
        {"Game_Starting", "[yellow]Starting the game[/]"},
        {"Server_Starting", "[gray]Starting SPT Server...[/]"},
        {"Server_Process_Fail", "Failed to start the server process!"},
        {"Server_Waiting", "Waiting for the server to start up..."},
        {"Server_Loading", "Loading server... {0}s"},
        {"Server_Exited", "The server shut down unexpectedly!"},
        {"Server_Timeout", "[yellow]![/] Server wait timeout.[/]"},
        {"Server_Success", "[green]√[/] The server has successfully booted up [green]{0}:{1}[/]"},
        {"Launcher_Opening", "[gray]Opening Launcher...[/]"},
        {"Launcher_NotFound", "Launcher not found!"},
        {"Game_Started_Title", "The game has started"},
        {"Game_Close_Instruction", "Press [bold red]ENTER[/] in this window to close the server and synchronize the profile."},
        {"Server_Stopping", "[gray]I'm shutting down the server...[/]"},
        {"Server_Stopped", "[green]√[/] The server has been shut down."},
        
        // GitHub
        {"Auth_Success", "[green]√[/] Authorized as: [bold]{0}[/]"},
        {"File_Sent", "[green]√[/] File sent: {0}"}
    };

    private static readonly Dictionary<string, string> _ru = new()
    {
        {"App_Started", "Приложение запущено. Аргументы: {0}"},
        {"Debug_Enabled", "Режим отладки включен через аргументы"},
        {"Config_Loading", "Загрузка конфигурации..."},
        {"Setup_Incomplete", "Настройка не завершена. Выход."},
        {"Conn_GitHub", "Подключение к GitHub..."},
        {"Repo_Target", "Целевой репозиторий: [blue]{0}/{1}[/]" },
        {"Offline_Mode", "[yellow]![/] Офлайн режим (нет доступа к GitHub)."},
        {"Start_Game_Question", "Запустить игру?"},
        {"Start_Game_NoSync", "Запустить игру без синхронизации?"},
        {"Launch_Canceled", "[gray]Запуск отменен.[/]"},
        {"Press_Enter", "Нажмите [blue]Enter[/], чтобы выйти."},
        
        {"Token_NotFound", "[yellow]![/] GitHub Token не найден."},
        {"Token_Prompt", "Введите ваш [green]GitHub PAT[/]:"},
        {"Token_Invalid", "[white on red]×[/] Токен слишком короткий!"},
        {"Url_NotFound", "[yellow]![/] URL репозитория не найден."},
        {"Url_Prompt", "Введите [green]HTTPS URL[/] репозитория:"},
        {"Url_Invalid", "[white on red]×[/] Ссылка должна начинаться с https://github.com/"},
        {"Config_Saved", "[green]√[/] Настройки сохранены в .env файл."},
        {"Env_Error", "[white on red]×[/] Ошибка записи .env: {0}"},

        {"Update_Available", "[bold red]ДОСТУПНО ОБНОВЛЕНИЕ[/]"},
        {"Update_Body", "[yellow]Новая версия:[/] [green]v{0}[/]\nВаша версия: [gray]v{1}[/]\n\nСкачать: [blue underline]{2}[/]"},
        {"Update_Latest", "[gray]У вас последняя версия программы. (v{0})[/]"},

        {"Sync_Title", "[yellow]Сихнронизация[/]"},
        {"Sync_Downloading", "[gray]Загрузка профилей из облака...[/]"},    
        {"Sync_NoProfiles", "[yellow]![/] В облаке нет профилей (Репозиторий пуст)."},
        {"Sync_Found", "[bold]Профилей в облаке:[/] {0}"},
        {"Sync_Updated_Count", "[green]Обновлено {0} профилей из облака.[/]"},
        
        {"Table_File", "Файл"},
        {"Table_Status", "Статус"},
        {"Table_Action", "Действие"},
        {"Status_Synced", "[green]Актуал[/]"},
        {"Status_LocalNewer", "[blue]Локальный новее[/]"},
        {"Status_NewLocal", "[green]Новый локальный[/]"},
        {"Status_Update", "[yellow]Обновить[/]"},
        {"Action_WillUpload", "[yellow]Будет отправлен[/]"},
        {"Action_Downloaded", "[green]Загружен[/]"},
        
        {"Sync_Checking", "[gray]Проверка изменений для отправки...[/]"},
        {"Sync_Report_Title", "Отчет синхронизации"},
        {"Sync_Profile_Title", "Профиль"}, 
        {"Sync_Reason_Title", "Причина"},
        {"Sync_Result_Title", "Результат"},
        {"Sync_NoLocal", "[gray]Локальные профили не найдены.[/]"},
        {"Reason_NewProgress", "[green]Новый прогресс[/]"},
        {"Reason_Pending", "[blue]Отложенная синхра[/]"},
        {"Result_Conflict", "[red]Конфликт[/]"},
        {"Result_RemoteNewer", "В облаке новее"},
        {"Result_Sent", "[green]Отправлен[/]"},
        {"Result_Error", "[red]Ошибка: {0}[/]"},
        {"Sync_AllDone", "[gray]Всё синхронизировано.[/]"},
        {"Verify_Remote", "[gray]Проверка версии на сервере: {0}...[/]"},

        {"Server_NotFound", "Файл сервера не найден: {0}"},
        {"Config_Found", "[gray]Конфиг найден:[/] {0} -> [blue]{1}:{2}[/]"},
        {"Config_Default", "[gray]Конфиг не найден, используем:[/]{0}:{1}"},
        {"Game_Starting", "[yellow]Запуск игры[/]"},
        {"Server_Starting", "[gray]Запуск сервера SPT...[/]"},
        {"Server_Process_Fail", "Не удалось запустить процесс сервера!"},
        {"Server_Waiting", "Ожидание запуска сервера..."},
        {"Server_Loading", "Загрузка сервера... {0}c"},
        {"Server_Exited", "Сервер неожиданно завершил работу!"},
        {"Server_Timeout", "[yellow]![/] Время ожидания сервера истекло.[/]"},
        {"Server_Success", "[green]√[/] Сервер успешно запущен [green]{0}:{1}[/]"},
        {"Launcher_Opening", "[gray]Открываем Лаунчер...[/]"},
        {"Launcher_NotFound", "Лаунчер не найден!"},
        {"Game_Started_Title", "Игра запущена"},
        {"Game_Close_Instruction", "Нажмите [bold red]ENTER[/] в этом окне, чтобы закрыть сервер и синхронизировать профиль."},
        {"Server_Stopping", "[gray]Выключаю сервер...[/]"},
        {"Server_Stopped", "[green]√[/] Сервер выключен."},
        
        {"Auth_Success", "[green]√[/] Авторизован как: [bold]{0}[/]"},
        {"File_Sent", "[green]√[/] Файл отправлен: {0}"}
    };

    private static readonly Dictionary<string, string> _uk = new()
    {
        {"App_Started", "Додаток запущено. Аргументи: {0}"},
        {"Debug_Enabled", "Режим налагодження увімкнено через аргументи"},
        {"Config_Loading", "Завантаження конфігурації..."},
        {"Setup_Incomplete", "Налаштування не завершено. Вихід."},
        {"Conn_GitHub", "Підключення до GitHub..."},
        {"Repo_Target", "Цільовий репозиторій: [blue]{0}/{1}[/]" },
        {"Offline_Mode", "[yellow]![/] Офлайн режим (немає доступу до GitHub)."},
        {"Start_Game_Question", "Запустити гру?"},
        {"Start_Game_NoSync", "Запустити гру без синхронізації?"},
        {"Launch_Canceled", "[gray]Запуск скасовано.[/]"},
        {"Press_Enter", "Натисніть [blue]Enter[/] для виходу."},
        
        {"Token_NotFound", "[yellow]![/] GitHub Token не знайдено."},
        {"Token_Prompt", "Введіть ваш [green]GitHub PAT[/]:"},
        {"Token_Invalid", "[white on red]×[/] Токен занадто короткий!"},
        {"Url_NotFound", "[yellow]![/] URL репозиторію не знайдено."},
        {"Url_Prompt", "Введіть [green]HTTPS URL[/] репозиторію:"},
        {"Url_Invalid", "[white on red]×[/] Посилання має починатися з https://github.com/"},
        {"Config_Saved", "[green]√[/] Налаштування збережено в .env файл."},
        {"Env_Error", "[white on red]×[/] Помилка запису .env: {0}"},

        {"Update_Available", "[bold red]ДОСТУПНЕ ОНОВЛЕННЯ[/]"},
        {"Update_Body", "[yellow]Нова версія:[/] [green]v{0}[/]\nВаша версія: [gray]v{1}[/]\n\nЗавантажити: [blue underline]{2}[/]"},
        {"Update_Latest", "[gray]У вас остання версія програми. (v{0})[/]"},

        {"Sync_Title", "[yellow]Синхронізація[/]"},
        {"Sync_Downloading", "[gray]Завантаження профілів з облака...[/]"},
        {"Sync_NoProfiles", "[yellow]![/] У облоці немає профілів (Репозиторій порожній)."},
        {"Sync_Found", "[bold]Профілів у облака:[/] {0}"},
        {"Sync_Updated_Count", "[green]Оновлено {0} профілів з облака.[/]"},
        
        {"Table_File", "Файл"},
        {"Table_Status", "Статус"},
        {"Table_Action", "Дія"},
        {"Status_Synced", "[green]Актуал[/]"},
        {"Status_LocalNewer", "[blue]Локальний новіший[/]"},
        {"Status_NewLocal", "[green]Новий локальний[/]"},
        {"Status_Update", "[yellow]Оновити[/]"},
        {"Action_WillUpload", "[yellow]Буде надіслано[/]"},
        {"Action_Downloaded", "[green]Завантажено[/]"},
        
        {"Sync_Checking", "[gray]Перевірка змін для відправки...[/]"},
        {"Sync_Report_Title", "Звіт синхронізації"},
        {"Sync_Profile_Title", "Профіль"}, 
        {"Sync_Reason_Title", "Причина"},
        {"Sync_Result_Title", "Результат"}, 
        {"Sync_NoLocal", "[gray]Локальні профілі не знайдені.[/]"},
        {"Reason_NewProgress", "[green]Новий прогрес[/]"},
        {"Reason_Pending", "[blue]Відкладена синхр[/]"},
        {"Result_Conflict", "[red]Конфлікт[/]"},
        {"Result_RemoteNewer", "У хмарі новіше"},
        {"Result_Sent", "[green]Надіслано[/]"},
        {"Result_Error", "[red]Помилка: {0}[/]"},
        {"Sync_AllDone", "[gray]Все синхронізовано.[/]"},
        {"Verify_Remote", "[gray]Перевірка версії на сервері: {0}...[/]"},

        {"Server_NotFound", "Файл сервера не знайдено: {0}"},
        {"Config_Found", "[gray]Конфіг знайдено:[/] {0} -> [blue]{1}:{2}[/]"},
        {"Config_Default", "[gray]Конфіг не знайдено, використовуємо:[/]{0}:{1}"},
        {"Game_Starting", "[yellow]Запуск гри[/]"},
        {"Server_Starting", "[gray]Запуск сервера SPT...[/]"},
        {"Server_Process_Fail", "Не вдалося запустити процес сервера!"},
        {"Server_Waiting", "Очікування запуску сервера..."},
        {"Server_Loading", "Завантаження сервера... {0}c"},
        {"Server_Exited", "Сервер несподівано завершив роботу!"},
        {"Server_Timeout", "[yellow]![/] Час очікування сервера вичерпано.[/]"},
        {"Server_Success", "[green]√[/] Сервер успішно запущено [green]{0}:{1}[/]"},
        {"Launcher_Opening", "[gray]Відкриваємо Лаунчер...[/]"},
        {"Launcher_NotFound", "Лаунчер не знайдено!"},
        {"Game_Started_Title", "Гра запущена"},
        {"Game_Close_Instruction", "Натисніть [bold red]ENTER[/] у цьому вікні, щоб закрити сервер та синхронізувати профіль."},
        {"Server_Stopping", "[gray]Вимикаю сервер...[/]"},
        {"Server_Stopped", "[green]√[/] Сервер вимкнено."},
        
        {"Auth_Success", "[green]√[/] Авторизовано як: [bold]{0}[/]"},
        {"File_Sent", "[green]√[/] Файл надіслано: {0}"}
    };

    static Loc()
    {
        var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLower();

        _currentStrings = culture switch
        {
            "ru" or "be" => _ru, 
            "uk" => _uk,         
            _ => _en 
        };
    }

    public static string Tr(string key, params object[] args)
    {   
        string template = _currentStrings.GetValueOrDefault(key) ?? _en.GetValueOrDefault(key) ?? key;

        if (args.Length == 0) return template;
        try
        {
            return string.Format(template, args);
        }
        catch
        {
            return template;
        }
            
        
    }
}