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
            self._initialized = True
            self._config_loaded = False
            self._env_file: Optional[Path] = None
            self._load_or_setup_config()
    
    def _load_or_setup_config(self):
        # Находим или создаем .env файл
        self._env_file = self._get_or_create_env_file()
        
        # Загружаем конфигурацию
        self._load_config()
        
        # Проверяем и настраиваем недостающие параметры
        self._setup_missing_config()
        
        # Проверяем валидность конфигурации
        self._validate_config()
        
        self._config_loaded = True
    
    def _get_or_create_env_file(self) -> Path:
        # Ищем существующий .env
        env_path = find_dotenv()
        if env_path:
            env_file = Path(env_path)
            logger.log('CONFIG', f'Найден .env файл: {env_file}', 'ok')
            return env_file
        
        # Создаем новый
        script_dir = Path(__file__).parent.absolute()
        env_file = script_dir / '.env'
        
        if not env_file.exists():
            env_file.touch()
            logger.log('CONFIG', f'Создан новый .env файл: {env_file}', 'ok')
            
            # Показываем инструкцию
            logger.log('CONFIG', '=' * 50, '')
            logger.log('CONFIG', 'Создан новый конфигурационный файл.', '')
            logger.log('CONFIG', f'Отредактируйте его: {env_file}', '')
            logger.log('CONFIG', 'Или следуйте инструкциям ниже.', '')
            logger.log('CONFIG', '=' * 50, '')
        
        return env_file
    
    def _load_config(self):
        load_dotenv(self._env_file)
        
        # Читаем переменные
        self.GITHUB_PAT = os.getenv('GITHUB_PAT')
        self.REPO_HTTPS_URL = os.getenv('REPO_URL')
        
        # Пути
        self.SCRIPT_DIR = Path(__file__).parent.absolute()
        self.GAME_PROFILES_PATH = Path.cwd() / 'SPT' / 'user' / 'profiles'
        self.LOCAL_REPO_PATH = self.SCRIPT_DIR / 'profiles_repo'
        self.SPT_SERVER_PATH = Path.cwd() / 'SPT' / 'SPT.Server.exe'
        self.SPT_LAUNCHER_PATH = Path.cwd() / 'SPT' / 'SPT.Launcher.exe'
    
    def _setup_missing_config(self):
        """Настраивает недостающие параметры."""
        # GitHub токен
        if not self.GITHUB_PAT:
            self.GITHUB_PAT = self._ask_github_token()
            if self.GITHUB_PAT:
                set_key(str(self._env_file), 'GITHUB_PAT', self.GITHUB_PAT)
                logger.log('CONFIG', 'Токен сохранен в .env', 'ok')
        
        # URL репозитория
        if not self.REPO_HTTPS_URL:
            self.REPO_HTTPS_URL = self._ask_repo_url()
            if self.REPO_HTTPS_URL:
                set_key(str(self._env_file), 'REPO_URL', self.REPO_HTTPS_URL)
                logger.log('CONFIG', 'URL репозитория сохранен', 'ok')
    
    def _ask_github_token(self) -> Optional[str]:
        """Запрашивает GitHub токен у пользователя."""
        logger.log('CONFIG', '=' * 50, '')
        logger.log('CONFIG', 'НАСТРОЙКА GITHUB ТОКЕНА', '')
        logger.log('CONFIG', '=' * 50, '')
        logger.log('CONFIG', 'Для синхронизации профилей нужен GitHub токен.', '')
        logger.log('CONFIG', 'Инструкция:', '')
        logger.log('CONFIG', '1. Откройте: https://github.com/settings/tokens', '')
        logger.log('CONFIG', '2. Нажмите "Generate new token (classic)"', '')
        logger.log('CONFIG', '3. Выберите срок (рекомендуется 30 дней)', '')
        logger.log('CONFIG', '4. Поставьте галочку "repo"', '')
        logger.log('CONFIG', '5. Скопируйте токен (выглядит как ghp_xxxxxxxxxx)', '')
        logger.log('CONFIG', '=' * 50, '')
        
        while True:
            token = input('Введите GitHub токен (или Enter для отмены): ').strip()
            
            if not token:
                logger.log('CONFIG', 'Настройка токена отменена', 'warn')
                return None
            
            # Базовая валидация
            if not self._validate_github_token(token):
                logger.log('CONFIG', 'Неверный формат токена', 'error')
                logger.log('CONFIG', 'Токен должен начинаться с ghp_, ghs_ или github_pat_', '')
                continue
            
            # Подтверждение
            confirm = input('Сохранить токен? (y/n): ').strip().lower()
            if confirm == 'y':
                return token
            else:
                logger.log('CONFIG', 'Введите токен заново', '')
    
    def _ask_repo_url(self) -> Optional[str]:
        """Запрашивает URL репозитория."""
        logger.log('CONFIG', '=' * 50, '')
        logger.log('CONFIG', 'НАСТРОЙКА РЕПОЗИТОРИЯ', '')
        logger.log('CONFIG', '=' * 50, '')
        logger.log('CONFIG', 'Введите URL репозитория где хранятся профили.', '')
        logger.log('CONFIG', 'Пример: https://github.com/fanteeek/spt-profiles-sync.git', '')
        logger.log('CONFIG', '=' * 50, '')
        
        while True:
            url = input('Введите URL репозитория (или Enter для отмены): ').strip()
            
            if not url:
                logger.log('CONFIG', 'Настройка репозитория отменена', 'warn')
                return None
            
            # Валидация URL
            if not self._validate_repo_url(url):
                logger.log('CONFIG', 'Неверный формат URL', 'error')
                logger.log('CONFIG', 'URL должен быть вида: https://github.com/username/repo.git', '')
                continue
            
            confirm = input(f'Использовать репозиторий: {url}? (y/n): ').strip().lower()
            if confirm == 'y':
                return url
    
    def _validate_github_token(self, token: str) -> bool:
        """Проверяет формат GitHub токена."""
        # GitHub токены обычно начинаются с:
        # ghp_ для классических токенов
        # ghs_ для токенов GitHub Actions
        # github_pat_ для fine-grained токенов
        patterns = [
            r'^ghp_[A-Za-z0-9_]{36,}$',
            r'^ghs_[A-Za-z0-9_]{36,}$',
            r'^github_pat_[A-Za-z0-9_]{22}_[A-Za-z0-9_]{59}$'
        ]
        
        return any(re.match(pattern, token) for pattern in patterns)
    
    def _validate_repo_url(self, url: str) -> bool:
        """Проверяет формат URL репозитория."""
        pattern = r'^https://github\.com/[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+(\.git)?$'
        return re.match(pattern, url) is not None
    
    def _validate_config(self):
        """Проверяет валидность всей конфигурации."""
        if not self.GITHUB_PAT:
            logger.error_and_exit('CONFIG', 'GitHub токен не настроен')
        
        if not self.REPO_HTTPS_URL:
            logger.error_and_exit('CONFIG', 'URL репозитория не настроен')
        
        if not self._validate_github_token(self.GITHUB_PAT):
            logger.log('CONFIG', 'Предупреждение: токен имеет необычный формат', 'warn')
        
        if not self._validate_repo_url(self.REPO_HTTPS_URL):
            logger.error_and_exit('CONFIG', 'Неверный формат URL репозитория')
    
    def reload(self):
        """Перезагружает конфигурацию."""
        self._config_loaded = False
        self._load_or_setup_config()