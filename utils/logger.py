import sys
from datetime import datetime

class Logger:
    _instance = None  
    
    def __new__(cls):
        if cls._instance is None:
            cls._instance = super().__new__(cls)
            cls._instance._initialized = False
        return cls._instance
    
    def __init__(self):
        if not self._initialized:
            self._initialized = True
            self._status_symbols = {
                '': '',
                'ok': '√',
                'error': '×',
                'warn': '△'
            }
            self.debug_enabled = False
    
    def enable_debug(self):
        self.debug_enabled = True
        
    def disable_debug(self):
        self.debug_enabled = False
    
    def log(self, prefix: str, message: str, status: str = '') -> None:
        # Пропускаем DEBUG сообщения если отладка выключена
        if prefix == 'DEBUG' and not self.debug_enabled:
            return
        
        timestamp = datetime.now().strftime('%H:%M:%S')
        prefix_padded = f'[{prefix}]'.ljust(10)
        status_symbol = self._status_symbols.get(status, '')
        
        if status:
            print(f'{timestamp} {prefix_padded} {status_symbol} {message}')
        else:
            print(f'{timestamp} {prefix_padded} {message}')
    
    def error_and_exit(self, prefix: str, message: str) -> None:
        self.log(prefix, message, 'error')
        input('\nНажмите Enter для выхода...')
        sys.exit(1)