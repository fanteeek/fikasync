import requests
import base64
from typing import Optional, Tuple
from pathlib import Path
from datetime import datetime
from utils.logger import Logger

logger = Logger()

class GitHubClient:
    def __init__(self, token: str):
        self.token = token
        self.base_url = 'https://api.github.com'
        self.headers = {
            'Authorization': f'token {token}',
            'Accept': 'application/vnd.github.v3+json'
        }
    
    def test_token(self) -> bool:
        try:
            response = requests.get(
                f'{self.base_url}/user',
                headers=self.headers,
                timeout=10
            )
            
            if response.status_code == 200:
                user_data = response.json()
                logger.log('GITHUB', f'Авторизован как: {user_data.get('login', 'Unknown')}', 'ok')
                return True
            else:
                logger.log('GITHUB', f'Ошибка авторизации: {response.status_code}', 'error')
                return False
                
        except Exception as e:
            logger.log('GITHUB', f'Ошибка подключения: {e}', 'error')
            return False
    
    def extract_repo_info(self, github_url: str) -> Tuple[str, str]:
        parts = github_url.rstrip('/').split('/')
        
        if len(parts) >= 2:
            owner = parts[-2]
            repo = parts[-1].replace('.git', '')
            return owner, repo
        
        raise ValueError(f'Некорректный GitHub URL: {github_url}')
    
    def get_file_commit_time(self, owner: str, repo: str, file_path: str) -> Optional[float]:
        try:
            url = f'{self.base_url}/repos/{owner}/{repo}/commits'
            params = {'path': file_path, 'per_page': 1}
            
            response = requests.get(
                url, 
                headers=self.headers, 
                params=params, 
                timeout=10
            )
            
            if response.status_code == 200:
                commits = response.json()
                if commits and isinstance(commits, list):
                    commit_date = commits[0]['commit']['committer']['date']
                    dt = datetime.fromisoformat(commit_date.replace('Z', '+00:00'))
                    return dt.timestamp()
            
            return None
            
        except Exception as e:
            logger.log('GITHUB', f'Ошибка получения времени файла {file_path}: {e}', 'warn')
            return None
    
    def upload_file(self, owner: str, repo: str, file_path: str, local_file: Path) -> bool:
        try:
            # Получаем текущий SHA файла
            get_url = f'{self.base_url}/repos/{owner}/{repo}/contents/{file_path}'
            response = requests.get(get_url, headers=self.headers, timeout=10)
            
            sha = None
            if response.status_code == 200:
                sha = response.json()['sha']
            
            # Читаем и кодируем содержимое
            with open(local_file, 'rb') as f:
                content = f.read()
            encoded_content = base64.b64encode(content).decode('utf-8')
            
            # Подготавливаем данные
            data = {
                'message': f'Update {file_path}',
                'content': encoded_content
            }
            if sha:
                data['sha'] = sha
            
            # Загружаем
            put_url = f'{self.base_url}/repos/{owner}/{repo}/contents/{file_path}'
            response = requests.put(put_url, headers=self.headers, json=data, timeout=30)
            
            if response.status_code in [200, 201]:
                logger.log('GITHUB', f'Файл {file_path} загружен', 'ok')
                return True
            else:
                logger.log('GITHUB', f'Ошибка загрузки: {response.status_code}', 'error')
                return False
                
        except Exception as e:
            logger.log('GITHUB', f'Ошибка загрузки файла: {e}', 'error')
            return False