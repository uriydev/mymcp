using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using mymcp.Core;
using mymcp.Dynamic;
using Nice3point.Revit.Toolkit.External;
using Nice3point.Revit.Toolkit;

namespace mymcp.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class StartupCommand : ExternalCommand
    {
        private Process _groqProcess;
        private TcpListener _tcpListener;
        private bool _isListening;
        private DynamicCodeExecutor _dynamicCodeExecutor;

        public override void Execute()
        {
            try
            {
                // Инициализация динамического исполнителя кода
                _dynamicCodeExecutor = new DynamicCodeExecutor(UiApplication);
                
                // Запуск MCP сервера
                StartMcpServer();
                
                // Запуск TCP сервера для обработки команд от MCP
                StartTcpServer();
                
                // Запуск Groq клиента
                StartGroqClient();
                
                TaskDialog.Show("MCP", "MCP сервер запущен успешно!");
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка", $"Не удалось запустить MCP сервер: {ex.Message}");
                Logger.Error($"Ошибка запуска MCP: {ex}");
            }
        }

        private void StartMcpServer()
        {
            // Настройка обработчика команд
            MCPConnection.Instance.Initialize(UiApplication, HandleCommandAsync);
            MCPConnection.Instance.Start();
            
            Logger.Info("MCP сервер запущен");
        }

        private void StartTcpServer()
        {
            try
            {
                // Запуск TCP сервера для обработки команд от MCP
                _tcpListener = new TcpListener(IPAddress.Loopback, 8080);
                _tcpListener.Start();
                _isListening = true;
                
                // Запуск асинхронного слушателя
                Task.Run(ListenForConnectionsAsync);
                
                Logger.Info("TCP сервер запущен на порту 8080");
            }
            catch (Exception ex)
            {
                Logger.Error($"Ошибка запуска TCP сервера: {ex}");
                throw;
            }
        }

        private void StartGroqClient()
        {
            try
            {
                // Завершаем предыдущий процесс если он существует
                if (_groqProcess != null && !_groqProcess.HasExited)
                {
                    try
                    {
                        _groqProcess.Kill();
                        _groqProcess.WaitForExit(5000);
                    }
                    catch { }
                    finally
                    {
                        _groqProcess?.Dispose();
                    }
                }

                // Путь к исполняемому файлу GroqStarter
                string groqStarterPath = @"C:\_code\Repos\mymcp\mymcp\GroqStarter\bin\Debug\net9.0\GroqStarter.exe";

                if (!File.Exists(groqStarterPath))
                {
                    Logger.Error($"Не найден файл GroqStarter.exe по пути: {groqStarterPath}");
                    return;
                }

                // Запуск процесса GroqStarter
                _groqProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = groqStarterPath,
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        WorkingDirectory = Path.GetDirectoryName(groqStarterPath)
                    },
                    EnableRaisingEvents = true
                };

                _groqProcess.Exited += (sender, args) =>
                {
                    Logger.Error($"Процесс GroqStarter завершился неожиданно. Exit code: {_groqProcess?.ExitCode}");
                };

                _groqProcess.ErrorDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        Logger.Error($"GroqStarter error: {args.Data}");
                    }
                };

                _groqProcess.Start();
                _groqProcess.BeginErrorReadLine();
                Logger.Info("Groq клиент запущен");
            }
            catch (Exception ex)
            {
                Logger.Error($"Ошибка запуска Groq клиента: {ex}");
                throw;
            }
        }

        private async Task ListenForConnectionsAsync()
        {
            while (_isListening)
            {
                try
                {
                    var client = await _tcpListener.AcceptTcpClientAsync();
                    _ = HandleClientAsync(client);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Ошибка при прослушивании соединений: {ex}");
                    await Task.Delay(1000);
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    // Чтение запроса
                    var buffer = new byte[4096];
                    var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    var requestJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    // Десериализация запроса
                    var request = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(requestJson);
                    string method = request.method;
                    dynamic parameters = request.@params;

                    // Обработка запроса
                    object responseData;
                    switch (method)
                    {
                        case "health_check":
                            responseData = new
                            {
                                success = true,
                                status = new
                                {
                                    documentName = Document?.Title ?? "No active document",
                                    timestamp = DateTime.Now,
                                    version = UiApplication.Application.VersionName,
                                    dynamicCommands = new
                                    {
                                        enabled = true,
                                        cachedCommands = 0,
                                        validCommands = 0
                                    }
                                }
                            };
                            break;

                        case "execute_groq_command":
                            responseData = await ExecuteGroqCommandAsync(parameters.description.ToString(), (bool)parameters.safety);
                            break;

                        default:
                            responseData = new
                            {
                                success = false,
                                error = $"Unknown method: {method}"
                            };
                            break;
                    }

                    // Отправка ответа
                    var responseJson = Newtonsoft.Json.JsonConvert.SerializeObject(responseData);
                    var responseBytes = Encoding.UTF8.GetBytes(responseJson);
                    await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Ошибка при обработке клиента: {ex}");
            }
        }

        private async Task<object> ExecuteGroqCommandAsync(string commandDescription, bool safetyMode)
        {
            try
            {
                // Отправка запроса в Groq для генерации кода
                string generatedCode = await SendCommandToGroqAsync(commandDescription);
                if (string.IsNullOrEmpty(generatedCode))
                {
                    return new
                    {
                        success = false,
                        error = "Не удалось получить код от Groq API"
                    };
                }

                // Выполнение сгенерированного кода в Revit
                var startTime = DateTime.Now;
                var result = await _dynamicCodeExecutor.ExecuteScriptAsync(generatedCode);
                var executionTime = (DateTime.Now - startTime).TotalMilliseconds;

                return new
                {
                    success = true,
                    result = result?.ToString() ?? "Команда выполнена",
                    executionTime,
                    message = "Код успешно выполнен",
                    code = generatedCode
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    success = false,
                    error = $"Ошибка выполнения команды: {ex.Message}"
                };
            }
        }

        private async Task<string> SendCommandToGroqAsync(string commandDescription)
        {
            // Проверяем и перезапускаем процесс если необходимо
            if (_groqProcess == null || _groqProcess.HasExited)
            {
                StartGroqClient();
                // Даем время процессу запуститься
                await Task.Delay(2000);
            }

            try
            {
                // Проверяем что процесс все еще жив
                if (_groqProcess.HasExited)
                {
                    Logger.Error("Процесс GroqStarter завершился перед отправкой команды");
                    return null;
                }

                // Отправка команды в процесс GroqStarter
                Logger.Info($"Отправка команды в Groq: {commandDescription}");
                await _groqProcess.StandardInput.WriteLineAsync(commandDescription);
                await _groqProcess.StandardInput.FlushAsync();
                
                // Чтение результата с таймаутом до маркера конца
                var timeoutTask = Task.Delay(30000); // 30 секунд таймаут
                var resultBuilder = new System.Text.StringBuilder();
                
                while (true)
                {
                    var readTask = _groqProcess.StandardOutput.ReadLineAsync();
                    var completedTask = await Task.WhenAny(readTask, timeoutTask);
                    
                    if (completedTask == timeoutTask)
                    {
                        Logger.Error("Таймаут при чтении ответа от GroqStarter");
                        return null;
                    }
                    
                    string line = await readTask;
                    if (line == null)
                        break;
                        
                    if (line == "===END_OF_RESPONSE===")
                        break;
                        
                    if (resultBuilder.Length > 0)
                        resultBuilder.AppendLine();
                    resultBuilder.Append(line);
                }
                
                string result = resultBuilder.ToString();
                Logger.Info($"Получен ответ от Groq: {result}");
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error($"Ошибка при отправке команды в Groq: {ex}");
                
                // Попытка перезапуска процесса при ошибке
                try
                {
                    if (_groqProcess != null && !_groqProcess.HasExited)
                    {
                        _groqProcess.Kill();
                    }
                }
                catch { }
                
                _groqProcess = null;
                return null;
            }
        }

        private async Task<string> HandleCommandAsync(string command)
        {
            try
            {
                // Обработка команды от MCP
                var result = await ExecuteGroqCommandAsync(command, true);
                return Newtonsoft.Json.JsonConvert.SerializeObject(result);
            }
            catch (Exception ex)
            {
                return $"Ошибка: {ex.Message}";
            }
        }
    }
}