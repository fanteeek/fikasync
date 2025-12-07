import sys
import os
import re
from pathlib import Path
from typing import Optional
from dotenv import load_dotenv, set_key, find_dotenv
from utils.logger import Logger

logger = Logger()

class Config:
    _instance = None
    
    def __new__(cls):
        if cls._instance is None:
            cls._instance = super().__new__(cls)
        return cls._instance
    
    def __init__(self):
        if not hasattr(self, '_initialized'):
            if getattr(sys, 'frozen', False):
                # Если приложение запущено как EXE
                self.BASE_DIR = Path(sys.executable).parent
            else:
                # Если запущено как скрипт (поднимаемся на 2 уровня вверх из utils/config.py)
                self.BASE_DIR = Path(__file__).parent.parent.absolute()
            
            self.APP_VERSION = '0.0.1'
            self.UPDATE_REPO_NAME = 'fanteeek/fika-profiles-sync'
            self._initialized = True
            self._config_loaded = False
            self._env_file: Optional[Path] = None
            self._load_or_setup_config()
    
    def _load_or_setup_config(self):
        self._env_file = self._get_or_create_env_file()
        self._load_config()
        self._setup_missing_config()
        self._validate_config()
        self._config_loaded = True
    
    def _get_or_create_env_file(self) -> Path:
        # Сначала ищем .env строго рядом с исполняемым файлом
        env_file = self.BASE_DIR / '.env'
        
        if env_file.exists():
            logger.log('CONFIG', f'Найден .env файл: {env_file}', 'ok')
            return env_file           

        # Создаем новый, если не нашли
        if not env_file.exists():
            try:
                env_file.touch()
                logger.log('CONFIG', f'Создан новый .env файл: {env_file}', 'ok')
            except PermissionError:
                raise PermissionError(f'Нет прав на создание файла в {self.BASE_DIR}')
            
            logger.log('CONFIG', 'Создан новый конфигурационный файл.', 'ok')
        return env_file
    
    def _load_config(self):
        load_dotenv(self._env_file)
        self.GITHUB_PAT = os.getenv('GITHUB_PAT')
        self.REPO_HTTPS_URL = os.getenv('REPO_URL')
        self.SCRIPT_DIR = self.BASE_DIR
        self.GAME_PROFILES_PATH = self.BASE_DIR / 'SPT' / 'user' / 'profiles'
        self.LOCAL_REPO_PATH = self.SCRIPT_DIR / 'profiles_repo'
        self.SPT_SERVER_PATH = self.BASE_DIR / 'SPT' / 'SPT.Server.exe'
        self.SPT_LAUNCHER_PATH = self.BASE_DIR / 'SPT' / 'SPT.Launcher.exe'
    
    def _setup_missing_config(self):
        if not self.GITHUB_PAT:
            self.GITHUB_PAT = self._ask_github_token()
            if self.GITHUB_PAT:
                set_key(str(self._env_file), 'GITHUB_PAT', self.GITHUB_PAT)
                logger.log('CONFIG', 'Токен сохранен в .env', 'ok')
                
        if not self.REPO_HTTPS_URL:
            self.REPO_HTTPS_URL = self._ask_repo_url()
            if self.REPO_HTTPS_URL:
                set_key(str(self._env_file), 'REPO_URL', self.REPO_HTTPS_URL)
                logger.log('CONFIG', 'URL репозитория сохранен', 'ok')
    
    def _ask_github_token(self) -> Optional[str]:
        logger.log('CONFIG','Введите GitHub токен:')
        return input("Token: ").strip()

    def _ask_repo_url(self) -> Optional[str]:
        logger.log('CONFIG','Введите URL репозитория:')
        return input("URL: ").strip()
    
    def _validate_github_token(self, token: str) -> bool:
        patterns = [
            r'^ghp_[A-Za-z0-9_]{36,}$',
            r'^ghs_[A-Za-z0-9_]{36,}$',
            r'^github_pat_[A-Za-z0-9_]{22}_[A-Za-z0-9_]{59}$'
        ]
        
        return any(re.match(pattern, token) for pattern in patterns)
    
    def _validate_repo_url(self, url: str) -> bool:
        pattern = r'^https://github\.com/[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+(\.git)?$'
        return re.match(pattern, url) is not None
    
    def _validate_config(self):
        if not self.GITHUB_PAT:
            raise ValueError('GitHub токен не настроен')
        if not self.REPO_HTTPS_URL:
            raise ValueError('URL репозитрия не настроен')

    def reload(self):
        self._config_loaded = False
        self._load_or_setup_config()