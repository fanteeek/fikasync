import sys
import os
import time
import subprocess
import requests
from pathlib import Path
from packaging import version
from utils.logger import Logger

logger = Logger()

class AutoUpdater:
    def __init__(self, config):
        self.config = config
        self.current_version = config.APP_VERSION
        self.repo_name = config.UPDATE_REPO_NAME
        
        # Определяем путь к файлу (exe или скрипт)
        self.is_frozen = getattr(sys, 'frozen', False)
        if self.is_frozen:
            self.app_path = Path(sys.executable)
        else:
            self.app_path = Path(sys.argv[0])

    def cleanup_old_updates(self):
        old_file = self.app_path.with_name(self.app_path.name + ".old")
        if old_file.exists():
            try:
                time.sleep(1)
                os.remove(old_file)
                logger.log('UPDATE', 'Удален временный файл старой версии', 'ok')
            except Exception as e:
                logger.log('UPDATE', f'Не удалось удалить .old файл: {e}', 'warn')

    def check_and_update(self) -> bool:
        if not self.is_frozen:
            return False

        self.cleanup_old_updates()
        
        logger.log('UPDATE', f'Проверка версии... (Текущая: {self.current_version})')
        
        try:
            url = f"https://api.github.com/repos/{self.repo_name}/releases/latest"
            
            response = requests.get(url, timeout=5)
            
            if response.status_code != 200:
                logger.log('UPDATE', f'Ошибка получения данных о релизе: {response.status_code}', 'warn')
                return False
                
            data = response.json()
            latest_tag = data.get('tag_name', '0.0.0').lstrip('v') # Удаляем 'v' если есть
            
            v_current = version.parse(self.current_version)
            v_latest = version.parse(latest_tag)
            
            if v_latest <= v_current:
                logger.log('UPDATE', 'Версия актуальна', 'ok')
                return False

            logger.log('UPDATE', f'Найдена новая версия: {latest_tag}!', 'ok')
            
            # Ищем .exe файл в ассетах релиза
            download_url = None
            for asset in data.get('assets', []):
                if asset['name'].endswith('.exe'):
                    download_url = asset['browser_download_url']
                    break
            
            if not download_url:
                logger.log('UPDATE', 'В релизе нет .exe файла', 'error')
                return False
            
            # Запускаем процесс обновления
            return self._perform_update(download_url)
            
        except requests.RequestException:
            logger.log('UPDATE', 'Нет связи с GitHub для проверки обновлений', 'warn')
            return False
        except Exception as e:
            logger.log('UPDATE', f'Ошибка обновления: {e}', 'error')
            return False

    def _perform_update(self, url: str) -> bool:
        logger.log('UPDATE', 'Скачиваю обновление...')
        
        temp_new_file = self.app_path.with_name("update_tmp.exe")
        
        try:
            response = requests.get(url, stream=True)
            response.raise_for_status()
            
            with open(temp_new_file, 'wb') as f:
                for chunk in response.iter_content(chunk_size=8192):
                    f.write(chunk)
            
            logger.log('UPDATE', 'Файл скачан. Устанавливаю...', 'ok')
            
            old_file = self.app_path.with_name(self.app_path.name + ".old")
            
            if old_file.exists():
                os.remove(old_file)
                
            os.rename(self.app_path, old_file)
            os.rename(temp_new_file, self.app_path)
            
            logger.log('UPDATE', 'Перезапуск приложения...', 'ok')
            
            subprocess.Popen([str(self.app_path)] + sys.argv[1:], close_fds=True)
            
            sys.exit(0)
            return True
            
        except Exception as e:
            logger.log('UPDATE', f'Сбой при установке обновления: {e}', 'error')
            if temp_new_file.exists():
                try: os.remove(temp_new_file)
                except: pass
            return False