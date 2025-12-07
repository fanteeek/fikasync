import sys
import argparse
import traceback
from utils.config import Config
from utils.logger import Logger
from gitpy.github_client import GitHubClient
from gitpy.github_downloader import GitHubDownloader
from utils.file_manager import FileManager
from utils.profile_sync import ProfileSync
from utils.game_launcher import GameLauncher

def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description='Утилита для синхронизации профилей Fika/SPT с GitHub'
    )
    
    parser.add_argument(
        '-d', '--debug',
        action='store_true',
        help='Включить подробный режим отладки'
    )
    
    return parser.parse_args()

def setup_application(args) -> 'SPTGameSync':
    # Включаем дебаг если нужно (делаем ДО создания объектов)
    if args.debug:
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
            return False
        
        # Извлекаем информацию о репозитории
        try:
            owner, repo = self.github_client.extract_repo_info(self.config.REPO_HTTPS_URL)
        except ValueError as e:
            self.logger.error_and_exit('CONFIG', f'Ошибка URL: {e}')
            return False
        
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
        
        updated_count = 0
        error_count = 0
        should_launch_game = False
        
        if github_files:
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
            
            if error_count == 0:
                self.logger.log('SYSTEM', 'Синхронизация успешна. Автоматический запуск игры...', 'ok')
                should_launch_game = True
            else:
                self.logger.log('SYSTEM', 'Были ошибки при обновлении. Запуск отменен для безопасности.', 'error')
                return False
        
        else:
            self.logger.log('GITHUB', 'Не удалось загрузить профили с репозитория (или их нет). Синхронизация пропущена.', 'warn')
            
            # Спрашиваем пользователя
            self.logger.log('SYSTEM', 'Хотите запустить игру без обновления? (y/n): ')
            choice = input().strip().lower()
            
            if choice == 'y':
                should_launch_game = True
            else:
                self.logger.log('SYSTEM', 'Запуск игры отменён пользователем', 'warn')
                return True
            
        # Запуск игры
        if should_launch_game:
            
            self.logger.log('SYSTEM', 'Создаю снимок состояния профилей...')
            initial_snapshot = self.profile_sync.get_profiles_snapshot()
            
            game_success = self.game_launcher.launch_and_monitor()
            
            if game_success:
                self.logger.log('SYSTEM', 'Игра завершена, проверяю изменения...', 'ok')
                
                # Синхронизация изменений (Local -> GitHub)
                sync_success = self.profile_sync.sync_changes_after_game(owner, repo, initial_snapshot)
                
                if sync_success:
                    self.logger.log('SYSTEM', 'Синхронизация завершена успешно!', 'ok')
                else:
                    self.logger.log('SYSTEM', 'Были ошибки при отправке данных', 'warn')
                
                return True
            else:
                self.logger.log('SYSTEM', 'Игра не была запущена корректно', 'warn')
                return False
        
        return False

def main() -> None:
    args = parse_arguments()
    
    app = None
    try:
        app = setup_application(args)
        success = app.run()
        
        if success:
            app.logger.log('SYSTEM', 'Скрипт успешно завершён', 'ok')
        
    except KeyboardInterrupt:
        if app:
            app.logger.log('SYSTEM', 'Прервано пользователем', 'warn')
    except (ValueError, PermissionError) as e:
        app.logger.log('SYSTEM', f'Ошибка: {e}', 'error')
    except Exception as e:
        app.logger.log('SYSTEM', f'Критическая ошибка: {e}', 'error')
        traceback.print_exc()
    finally:
        print("\nНажмите Enter, чтобы выйти...")
        input()

if __name__ == '__main__':
    main()