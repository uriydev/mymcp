using System;
using System.Threading.Tasks;
using Autodesk.Revit.UI;

namespace mymcp.Core
{
    public class MCPConnection
    {
        private static MCPConnection _instance;
        public static MCPConnection Instance => _instance ??= new MCPConnection();

        public bool IsRunning { get; private set; }
        private Func<string, Task<string>> _commandHandler;

        private MCPConnection() { }

        public void Initialize(UIApplication uiApplication, Func<string, Task<string>> commandHandler = null)
        {
            // Сохраняем обработчик команд, который может быть null
            _commandHandler = commandHandler;

            // Здесь может быть логика инициализации MCP сервера
            Logger.Info("MCP Connection initialized");
        }

        public void Start()
        {
            if (IsRunning)
                return;

            // Логика запуска MCP сервера
            IsRunning = true;
            Logger.Info("MCP Connection started");
        }

        public void Stop()
        {
            if (!IsRunning)
                return;

            // Логика остановки MCP сервера
            IsRunning = false;
            Logger.Info("MCP Connection stopped");
        }

        /// <summary>
        /// Обработка входящей команды с возможностью динамического выполнения
        /// </summary>
        public async Task<string> HandleCommand(string command)
        {
            if (_commandHandler != null)
            {
                return await _commandHandler(command);
            }

            // Базовая обработка команд, если нет специального обработчика
            return $"Получена команда: {command}";
        }
    }
} 