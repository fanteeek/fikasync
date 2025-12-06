import os
import shutil
from pathlib import Path
from typing import List, Tuple, Dict
from utils.logger import Logger

logger = Logger()

class ProfileSync:
    def __init__(self, config, github_client, file_manager):
        self.config = config
        self.github_client = github_client
        self.file_manager = file_manager
    
    def compare_profiles(self, owner: str, repo: str, github_files: List[Path]) -> Tuple[List[Path], List[Path], Dict[str, float]]:
        logger.log('SYNC', 'Сравниваю профили...')
        
        files_to_update = []
        files_to_skip = []
        git_times = {}
        
        # Создаём папку для профилей если её нет
        self.config.GAME_PROFILES_PATH.mkdir(parents=True, exist_ok=True)
        
        for github_file in github_files:
            local_file = self.config.GAME_PROFILES_PATH / github_file.name
            
            # Получаем время последнего коммита
            git_mtime = self.github_client.get_file_commit_time(
                owner, repo, f'profiles/{github_file.name}'
            )
            
            if git_mtime is not None:
                git_times[github_file.name] = git_mtime
            
            if git_mtime is None:
                git_mtime = github_file.stat().st_mtime
                logger.log('DEBUG', f'Использую время файла для {github_file.name}', '')
            
            # Если локального файла нет — нужно обновить
            if not local_file.exists():
                files_to_update.append(github_file)
                logger.log('DEBUG', f'Новый файл: {github_file.name}', 'ok')
                continue
            
            # Сравниваем время
            local_mtime = local_file.stat().st_mtime
            
            logger.log('DEBUG', f'local: {local_mtime}')
            logger.log('DEBUG', f'local: {git_mtime}')
            
            if git_mtime > local_mtime:
                files_to_update.append(github_file)
                logger.log('DEBUG', f'Новее: {github_file.name}', 'ok')
            else:
                files_to_skip.append(github_file)
                logger.log('DEBUG', f'Актуален: {github_file.name}', '')
        
        return files_to_update, files_to_skip, git_times
    
    def update_profiles(self, github_profiles_path: Path, files_to_update: List[Path], git_times: Dict[str, float]) -> Tuple[int, int]:
        if not files_to_update:
            logger.log('SYNC', 'Нет файлов для обновления')
            return 0, 0
        
        logger.log('DEBUG', f'Начинаю обновление {len(files_to_update)} файлов...')
        
        updated_count = 0
        error_count = 0
        
        for github_file in files_to_update:
            local_file = self.config.GAME_PROFILES_PATH / github_file.name
            
            try:
                # Бэкап
                if local_file.exists():
                    backup_file = self.file_manager.create_backup(
                        local_file, 
                        self.config.SCRIPT_DIR / 'backups'
                    )
                    if backup_file:
                        logger.log('BACKUP', f'Бэкап: {backup_file.name}', '')
                
                # Копируем
                shutil.copy2(github_file, local_file)
                
                # Восстанавливаем время Git
                if github_file.name in git_times:
                    git_timestamp = git_times[github_file.name]
                    os.utime(local_file, (git_timestamp, git_timestamp))
                
                updated_count += 1
                logger.log('DEBUG', f'Обновлён: {github_file.name}', 'ok')
                    
            except PermissionError:
                error_count += 1
                logger.log('DEBUG', f'Нет прав для записи: {local_file.name}', 'error')
            except Exception as e:
                error_count += 1
                logger.log('DEBUG', f'Ошибка обновления {github_file.name}: {e}', 'error')
        
        return updated_count, error_count
    
    def sync_changes_after_game(self, owner: str, repo: str) -> bool:
        logger.log('SYNC', 'Синхронизирую изменения с GitHub...')
        
        try:
            local_profiles = list(self.config.GAME_PROFILES_PATH.glob('*.json'))
            
            if not local_profiles:
                logger.log('SYNC', 'Нет профилей для синхронизации', 'warn')
                return True
            
            success_count = 0
            total_count = len(local_profiles)
            
            for profile in local_profiles:
                remote_path = f'profiles/{profile.name}'
                if self.github_client.upload_file(owner, repo, remote_path, profile):
                    success_count += 1
            
            if success_count == total_count:
                logger.log('SYNC', f'Все {success_count} файлов синхронизированы', 'ok')
                return True
            elif success_count > 0:
                logger.log('SYNC', f'Синхронизировано {success_count}/{total_count} файлов', 'warn')
                return True
            else:
                logger.log('SYNC', 'Не удалось синхронизировать файлы', 'error')
                return False
                
        except Exception as e:
            logger.log('SYNC', f'Ошибка синхронизации: {e}', 'error')
            return False