import os
import shutil
import hashlib
from pathlib import Path
from typing import List, Tuple, Dict, Optional
from utils.logger import Logger

logger = Logger()

class ProfileSync:
    def __init__(self, config, github_client, file_manager):
        self.config = config
        self.github_client = github_client
        self.file_manager = file_manager
    
    def get_profiles_snapshot(self) -> Dict[str, str]:
        snapshot = {}
        if self.config.GAME_PROFILES_PATH.exists():
            for profile in self.config.GAME_PROFILES_PATH.glob('*.json'):
                file_hash = self._calculate_file_hash(profile)
                if file_hash:
                    snapshot[profile.name] = file_hash
        return snapshot
    
    def _calculate_file_hash(self, file_path: Path) -> Optional[str]:
        if not file_path.exists():
            return None
        
        sha256_hash = hashlib.sha256()
        try:
            with open(file_path, 'rb') as f:
                for byte_block in iter(lambda: f.read(4096), b''):
                    sha256_hash.update(byte_block)
            return sha256_hash.hexdigest()
        except Exception as e:
            logger.log('DEBUG', f'Ошибка хеширования {file_path.name}: {e}', 'warn')
            return None
    
    def compare_profiles(self, owner: str, repo: str, github_files: List[Path]) -> Tuple[List[Path], List[Path], Dict[str, float]]:
        logger.log('SYNC', 'Сравниваю профили...')
        
        files_to_update = []
        files_to_skip = []
        git_times = {}
        
        # Создаём папку для профилей если её нет
        self.config.GAME_PROFILES_PATH.mkdir(parents=True, exist_ok=True)
        
        for github_file in github_files:
            local_file = self.config.GAME_PROFILES_PATH / github_file.name
            file_name = github_file.name
            
            if not local_file.exists():
                logger.log('DEBUG', f'Новый файл: {file_name}', 'ok')
                files_to_update.append(github_file)
                t = self.github_client.get_file_commit_time(owner, repo, f'profiles/{file_name}')
                if t: git_times[file_name] = t
                continue
            
            remote_hash = self._calculate_file_hash(github_file)
            local_hash = self._calculate_file_hash(local_file)
            
            if remote_hash and local_file and remote_hash == local_hash:
                logger.log('DEBUG', f'Идентичен (Hash): {file_name}')
                files_to_skip.append(github_file)
                continue
            
            logger.log('DEBUG', f'Файл изменился: {file_name}. Проверяю время...', 'warn')
            
            git_mtime = self.github_client.get_file_commit_time(owner, repo, f'profiles/{file_name}')
            
            if git_mtime is not None:
                git_times[file_name] = git_mtime
            else:
                git_mtime = github_file.stat().st_mtime
            
            local_mtime = local_file.stat().st_mtime
            
            if git_mtime > (local_mtime + 2.0):
                files_to_update.append(github_file)
                logger.log('DEBUG', f'GitHub файл новее: {file_name}', 'ok')
            else:
                files_to_skip.append(github_file)
                logger.log('DEBUG', f'Локальный новее/актуален: {file_name}', 'ok')
               
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
                        logger.log('DEBUG', f'Бэкап: {backup_file.name}', '')
                
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
    
    def sync_changes_after_game(self, owner: str, repo: str, initial_snapshot: Dict[str, str]) -> bool:
        logger.log('SYNC', 'Проверяю локальные изменения для отправки...')
        
        try:
            local_profiles = list(self.config.GAME_PROFILES_PATH.glob('*.json'))
            
            if not local_profiles:
                logger.log('SYNC', 'Нет локальных профилей', 'warn')
                return True
            
            uploaded_count = 0
            skipped_count = 0
            error_count = 0
            
            for profile in local_profiles:
                file_name = profile.name
                remote_path = f'profiles/{file_name}'
                
                current_hash = self._calculate_file_hash(profile)
                
                if file_name in initial_snapshot:
                    if initial_snapshot[file_name] == current_hash:
                        logger.log('DEBUG', f'Контент не изменился (игнорирую время): {file_name}')
                        skipped_count += 1
                        continue
                    
                git_mtime = self.github_client.get_file_commit_time(owner, repo, remote_path)
                should_update = False
                
                if git_mtime is None:
                    logger.log('DEBUG', f'Новый файл для отправки: {file_name}', 'ok')
                    should_update = True
                else:
                    logger.log('DEBUG', f'Профиль был изменен в игре: {file_name}', 'ok')
                    should_update = True

                if should_update:
                    logger.log('SYNC', f'Отправляю {file_name} на GitHub...')
                    if self.github_client.upload_file(owner, repo, remote_path, profile):
                        uploaded_count += 1
                    else:
                        error_count += 1
                        
            if uploaded_count > 0:
                logger.log('SYNC', f'Успешно отправлено файлов: {uploaded_count}', 'ok')
            elif error_count == 0:
                logger.log('SYNC', 'Нет новых изменений для отправки', 'ok')
                
            if error_count > 0:
                logger.log('SYNC', f'Ошибок при отправке: {error_count}', 'error')
                return False
            
            return True
        
        except Exception as e:
            logger.log('SYNC', f'Ошибка процесса отправки: {e}', 'error')
            return False
            