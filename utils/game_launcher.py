# game_launcher.py
import subprocess
import time
from pathlib import Path
from utils.logger import Logger

logger = Logger()

class GameLauncher:
    '''Запуск и мониторинг игры.'''
    
    def __init__(self, config):
        self.config = config
    
    def is_process_running(self, process_name: str) -> bool:
        '''Проверяет, запущен ли процесс.'''
        try:
            result = subprocess.run(
                ['tasklist', '/FI', f'IMAGENAME eq {process_name}'],
                capture_output=True,
                text=True,
                creationflags=subprocess.CREATE_NO_WINDOW
            )
            return process_name.lower() in result.stdout.lower()
        except:
            return False
    
    def launch_and_monitor(self) -> bool:
        '''Запускает игру и отслеживает завершение.'''
        logger.log('GAME', 'Запуск игры')
        
        # Проверяем существование файлов
        if not self.config.SPT_SERVER_PATH.exists():
            logger.log('GAME', f'Файл сервера не найден: {self.config.SPT_SERVER_PATH}', 'error')
            return False
        
        if not self.config.SPT_LAUNCHER_PATH.exists():
            logger.log('GAME', f'Файл лаунчера не найден: {self.config.SPT_LAUNCHER_PATH}', 'error')
            return False
        
        logger.log('GAME', f'Сервер: {self.config.SPT_SERVER_PATH.name}')
        logger.log('GAME', f'Лаунчер: {self.config.SPT_LAUNCHER_PATH.name}')
        
        try:
            # Запускаем сервер
            server_process = subprocess.Popen(
                [str(self.config.SPT_SERVER_PATH)],
                cwd=self.config.SPT_SERVER_PATH.parent
            )
            
            time.sleep(15)
            
            # Проверяем что сервер работает
            if not self.is_process_running('SPT.Server.exe'):
                logger.log('GAME', 'Сервер не запустился', 'error')
                return False
            
            logger.log('GAME', 'Сервер запущен', 'ok')
            
            # Запускаем лаунчер
            subprocess.Popen(
                [str(self.config.SPT_LAUNCHER_PATH)],
                cwd=self.config.SPT_LAUNCHER_PATH.parent
            )
            
            logger.log('GAME', 'Лаунчер запущен', 'ok')
            logger.log('GAME', 'Играйте! Закройте игру для продолжения...')
            
            # Ждём завершения сервера
            while self.is_process_running('SPT.Server.exe'):
                time.sleep(5)
            
            logger.log('GAME', 'Сервер завершён', 'ok')
            return True
            
        except Exception as e:
            logger.log('GAME', f'Ошибка: {e}', 'error')
            return False