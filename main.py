import sys
import os
from utils.config import Config
from utils.logger import Logger
from gitpy.github_client import GitHubClient
from gitpy.github_downloader import GitHubDownloader
from utils.file_manager import FileManager
from utils.profile_sync import ProfileSync
from utils.game_launcher import GameLauncher

def check_debug_enabled() -> bool:
        # 1. Аргументы командной строки
        if '--debug' in sys.argv or '-d' in sys.argv:
            return True
        
        return False

def setup_application() -> 'SPTGameSync':
    # Включаем дебаг если нужно (делаем ДО создания объектов)
    if check_debug_enabled():
        logger = Logger()
        logger.enable_debug()
        logger.log('SYSTEM', 'Отладочный режим включен', 'ok')
        
    # Создаем основное приложение
    return SPTGameSync()

class SPTGameSync:

    def __init__(self):
        self.logger = Logger()
        self.config = Config()
        self.github_client = GitHubClient(self.config.GITHUB_PAT)
        self.github_downloader = GitHubDownloader(self.github_client)
        self.file_manager = FileManager()
        self.profile_sync = ProfileSync(self.config, self.github_client, self.file_manager)
        self.game_launcher = GameLauncher(self.config)
    
    def run(self) -> bool:
        # Проверка токена
        if not self.github_client.test_token():
            self.logger.error_and_exit('AUTH', 'Неверный GitHub токен')
        
        # Извлекаем информацию о репозитории
        try:
            owner, repo = self.github_client.extract_repo_info(self.config.REPO_HTTPS_URL)
        except ValueError as e:
            self.logger.error_and_exit('CONFIG', f'Ошибка URL: {e}')
        
        # Проверка путей
        self.file_manager.check_game_profiles_path(self.config.GAME_PROFILES_PATH)
        
        # Очистка старых файлов
        self.file_manager.cleanup_temp_files([
            self.config.SCRIPT_DIR / 'github_profiles',
            self.config.SCRIPT_DIR / 'profiles_repo',
        ])
        
        # Загрузка профилей
        github_profiles_path, github_files = self.github_downloader.download_and_extract_profiles(
            self.config.REPO_HTTPS_URL, 
            self.config.LOCAL_REPO_PATH
        )
        
        if not github_files:
            self.logger.error_and_exit('SYNC', 'Не удалось загрузить профили')
        
        # Сравнение профилей
        files_to_update, files_to_skip, git_times = self.profile_sync.compare_profiles(
            owner, repo, github_files
        )
        
        # Обновление файлов
        updated_count, error_count = self.profile_sync.update_profiles(
            github_profiles_path, files_to_update, git_times
        )
        
        # Очистка
        self.file_manager.cleanup_temp_files([
            self.config.SCRIPT_DIR / 'github_profiles',
            self.config.SCRIPT_DIR / 'profiles_repo',
        ])
        
        # Статистика
        self.logger.log('SYSTEM', f'Загружено: {len(github_files)} файлов')
        self.logger.log('SYSTEM', f'Обновлено: {updated_count}, Ошибок: {error_count}')
        
        # Запуск игры
        if error_count == 0:
            self.logger.log('SYSTEM', 'Хотите запустить игру? (y/n): ')
            choice = input().strip().lower()
            
            if choice == 'y':
                game_success = self.game_launcher.launch_and_monitor()
                
                if game_success:
                    self.logger.log('SYSTEM', 'Игра завершена, синхронизирую изменения...', 'ok')
                    
                    # Синхронизация изменений
                    # sync_success = self.profile_sync.sync_changes_after_game(owner, repo)
                    
                    # if sync_success:
                    #     self.logger.log('SYSTEM', 'Все изменения синхронизированы с GitHub!', 'ok')
                    # else:
                    #     self.logger.log('SYSTEM', 'Не удалось синхронизировать изменения', 'warn')
                    
                    # return sync_success
                else:
                    self.logger.log('SYSTEM', 'Игра не была запущена', 'warn')
                    return False
            else:
                self.logger.log('SYSTEM', 'Запуск игры отменён', 'warn')
                return True
        else:
            return False
    
    def cleanup(self):
        '''Очистка ресурсов.'''
        pass

def main() -> None:
    app = None
    try:
        app = setup_application()
        
        success = app.run()
        
        if success:
            app.logger.log('SYSTEM', 'Скрипт успешно завершён', 'ok')
        
    except KeyboardInterrupt:
        if app:
            app.logger.log('SYSTEM', 'Прервано пользователем', 'warn')
    except Exception as e:
        if app:
            app.logger.log('SYSTEM', f'Критическая ошибка: {e}', 'error')
    finally:
        if app:
            app.cleanup()

if __name__ == '__main__':
    main()