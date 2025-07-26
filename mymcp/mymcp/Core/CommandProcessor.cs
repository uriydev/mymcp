using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace mymcp.Core;

/// <summary>
/// Процессор команд для обработки MCP запросов
/// </summary>
public class CommandProcessor
{
    private readonly UIApplication _uiApp;

    public CommandProcessor(UIApplication uiApp)
    {
        _uiApp = uiApp;
    }

    /// <summary>
    /// Обрабатывает команду из JSON
    /// </summary>
    public object ProcessCommand(string commandJson)
    {
        try
        {
            var commandData = JsonConvert.DeserializeObject<JObject>(commandJson);
            var commandName = commandData["method"]?.ToString();
            var parameters = commandData["params"] as JObject;

            Logger.Info($"Processing command: {commandName}");

            return commandName switch
            {
                "health_check" => ProcessHealthCheck(),
                _ => new { success = false, error = $"Unknown command: {commandName}" }
            };
        }
        catch (Exception ex)
        {
            Logger.Error("Error processing command", ex);
            return new { success = false, error = ex.Message };
        }
    }
    
    /// <summary>
    /// Проверка состояния системы
    /// </summary>
    private object ProcessHealthCheck()
    {
        try
        {
            var doc = _uiApp.ActiveUIDocument?.Document;
            
            return new
            {
                success = true,
                status = new
                {
                    serverRunning = true,
                    revitConnected = doc != null,
                    documentName = doc?.Title ?? "No document",
                    timestamp = DateTime.Now,
                    version = "2.0.0", // Обновленная версия с динамическими командами
                    dynamicCommands = new
                    {
                        enabled = true,
                        cachedCommands = 0,
                        validCommands = 0
                    }
                }
            };
        }
        catch (Exception ex)
        {
            Logger.Error("Error in health check", ex);
            return new { success = false, error = ex.Message };
        }
    }
} 