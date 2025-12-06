# github_downloader.py
import requests
import zipfile
from pathlib import Path
from typing import Optional, Tuple, List
from utils.logger import Logger

logger = Logger()

class GitHubDownloader:
    def __init__(self, github_client):
        self.github_client = github_client

    def download_repo_as_zip(self, owner: str, repo: str, output_dir: Path) -> bool:
        try:
            import shutil
            
            # URL для скачивания ZIP
            zip_url = f'https://api.github.com/repos/{owner}/{repo}/zipball'
            logger.log('GITHUB', f'Пытаюсь скачать репозиторий {owner}/{repo}...')
            
            # Скачиваем
            response = requests.get(
                zip_url, 
                headers=self.github_client.headers, 
                stream=True, 
                timeout=30
            )
            
            if response.status_code != 200:
                logger.log('GITHUB', f'Ошибка скачивания: {response.status_code}', 'error')
                return False
            
            # Создаем папку
            output_dir.mkdir(parents=True, exist_ok=True)
            temp_zip = output_dir / 'temp_repo.zip'
            
            # Скачиваем с прогрессом
            total_size = int(response.headers.get('content-length', 0))
            downloaded = 0
            
            with open(temp_zip, 'wb') as f:
                for chunk in response.iter_content(chunk_size=8192):
                    if chunk:
                        f.write(chunk)
                        downloaded += len(chunk)
                        
                        if total_size > 0:
                            percent = (downloaded / total_size) * 100
                            if int(percent) % 10 == 0:
                                logger.log('GITHUB', f'Прогресс: {percent:.1f}%', '')
            
            logger.log('DEBUG', 'Распаковываю архив...', '')
            with zipfile.ZipFile(temp_zip, 'r') as zip_ref:
                zip_ref.extractall(output_dir)
            
            # Удаляем временный ZIP
            temp_zip.unlink()
            
            # Переименовываем папку
            extracted_folders = list(output_dir.glob(f'{owner}-{repo}-*'))
            if extracted_folders:
                main_folder = extracted_folders[0]
                final_path = output_dir / 'repo'
                if final_path.exists():
                    shutil.rmtree(final_path)
                main_folder.rename(final_path)
            
            logger.log('DEBUG', 'Репозиторий скачан и распакован', 'ok')
            return True
            
        except requests.exceptions.Timeout:
            logger.log('GITHUB', 'Таймаут при скачивании', 'error')
            return False
        except zipfile.BadZipFile:
            logger.log('GITHUB', 'Поврежденный ZIP архив', 'error')
            return False
        except Exception as e:
            logger.log('GITHUB', f'Ошибка скачивания: {e}', 'error')
            return False
    
    def download_and_extract_profiles(self, repo_url: str, local_repo_path: Path) -> Tuple[Optional[Path], List[Path]]:        
        try:
            import shutil
            from utils.file_manager import FileManager
            
            # Извлекаем информацию о репозитории
            owner, repo = self.github_client.extract_repo_info(repo_url)
            
            # Очищаем старую папку
            file_manager = FileManager()
            if local_repo_path.exists():
                if file_manager.force_delete_folder(local_repo_path):
                    logger.log('SYNC', 'Старая папка удалена', 'ok')
            
            # Скачиваем репозиторий
            if not self.download_repo_as_zip(owner, repo, local_repo_path):
                return None, []
            
            # Ищем профили
            search_paths = [
                local_repo_path / 'repo' / 'profiles',
                local_repo_path / 'repo',
                local_repo_path / 'repo' / 'SPT' / 'user' / 'profiles',
            ]
            
            json_files = []
            for path in search_paths:
                if path.exists():
                    found_files = list(path.glob('*.json'))
                    if found_files:
                        json_files.extend(found_files)
            
            if not json_files:
                logger.log('SYNC', 'JSON файлы не найдены в репозитории', 'error')
                return None, []
            
            logger.log('SYNC', f'Всего найдено {len(json_files)} файлов профилей', 'ok')
            
            # Создаём чистую папку для профилей
            clean_profiles_path = local_repo_path.parent / 'github_profiles'
            
            if clean_profiles_path.exists():
                file_manager.force_delete_folder(clean_profiles_path)
            
            clean_profiles_path.mkdir(exist_ok=True)
            
            # Копируем профили
            copied_files = []
            for json_file in json_files:
                try:
                    dest_file = clean_profiles_path / json_file.name
                    shutil.copy2(json_file, dest_file)
                    copied_files.append(dest_file)
                except Exception as e:
                    logger.log('SYNC', f'Ошибка копирования {json_file.name}: {e}', 'warn')
            
            if not copied_files:
                logger.log('SYNC', 'Не удалось скопировать файлы', 'error')
                return None, []
            
            return clean_profiles_path, copied_files
            
        except Exception as e:
            logger.log('SYNC', f'Ошибка загрузки: {e}', 'error')
            return None, []