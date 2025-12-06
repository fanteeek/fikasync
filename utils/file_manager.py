# file_manager.py
import os
import shutil
import subprocess
import time
from pathlib import Path
from typing import List, Optional
from datetime import datetime
from utils.logger import Logger

logger = Logger()

class FileManager:
    def __init__(self):
        pass
    
    def force_delete_folder(self, folder_path: Path) -> bool:
        if not folder_path.exists():
            return True
        
        max_attempts = 3
        for attempt in range(max_attempts):
            try:
                shutil.rmtree(folder_path, ignore_errors=True)
                if not folder_path.exists():
                    return True
                
                if attempt == 1:
                    subprocess.run(
                        ['cmd', '/c', 'rmdir', '/s', '/q', str(folder_path)],
                        capture_output=True, 
                        timeout=3
                    )
                
                time.sleep(0.5)
                
            except Exception:
                if attempt == max_attempts - 1:
                    return False
        
        return not folder_path.exists()
    
    def check_game_profiles_path(self, profiles_path: Path) -> bool:
        logger.log('DEBUG', f'Проверяю папку: {profiles_path}')
        
        if profiles_path.exists():
            json_count = len(list(profiles_path.glob('*.json')))
            logger.log('PATH', f'Папка SPT Profiles найдена ({json_count} профилей)', 'ok')
            return True
        
        logger.log('PATH', f'Папка не найдена: {profiles_path}', 'warn')
        logger.log('PATH', 'Будет создана при необходимости')
        return True
    
    def create_backup(self, local_file: Path, backup_base_dir: Path) -> Optional[Path]:
        try:
            backup_dir = backup_base_dir / datetime.now().strftime('%Y%m%d_%H%M%S')
            backup_dir.mkdir(parents=True, exist_ok=True)
            
            backup_file = backup_dir / local_file.name
            shutil.copy2(local_file, backup_file)
            
            return backup_file
        except Exception as e:
            logger.log('BACKUP', f'Ошибка создания бэкапа {local_file.name}: {e}', 'warn')
            return None
    
    def cleanup_temp_files(self, temp_folders: List[Path]) -> None:
        for folder in temp_folders:
            if folder.exists():
                if self.force_delete_folder(folder):
                    logger.log('DEBUG', f'Удалено: {folder.name}', 'ok')