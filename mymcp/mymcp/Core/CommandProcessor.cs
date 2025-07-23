using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using mymcp.Analysis;
using mymcp.Dynamic;
using System.Threading.Tasks;

namespace mymcp.Core;

/// <summary>
/// Процессор команд для обработки MCP запросов
/// </summary>
public class CommandProcessor
{
    private readonly UIApplication _uiApp;
    private readonly SpaceAnalyzer _spaceAnalyzer;
    private readonly RouteCalculator _routeCalculator;
    private readonly MEPPathfinder _mepPathfinder;
    private readonly DynamicCommandManager _dynamicCommandManager;

    public CommandProcessor(UIApplication uiApp)
    {
        _uiApp = uiApp;
        _spaceAnalyzer = new SpaceAnalyzer(uiApp);
        _routeCalculator = new RouteCalculator(_spaceAnalyzer);
        _mepPathfinder = new MEPPathfinder(_spaceAnalyzer, _routeCalculator, uiApp.ActiveUIDocument.Document);
        _dynamicCommandManager = new DynamicCommandManager();
        
        // Предварительно компилируем часто используемые команды
        _ = Task.Run(() => _dynamicCommandManager.PrecompileCommonCommands());
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
                // Оставляем только health_check
                "health_check" => ProcessHealthCheck(),
                // Динамические команды
                "execute_dynamic_command" => ProcessDynamicCommand(parameters),
                "test_fixed_dynamic_command" => ProcessTestFixedDynamicCommand(parameters),
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
    /// Обрабатывает динамическую команду
    /// </summary>
    private object ProcessDynamicCommand(JObject parameters)
    {
        try
        {
            var description = parameters["description"]?.ToString();
            var complexityLevel = parameters["complexity"]?.ToString() ?? "moderate";
            var safetyMode = parameters["safety"]?.Value<bool>() ?? true;
            var optimization = parameters["optimization"]?.ToString() ?? "balanced";

            Logger.Info($"[CmdProc] Processing dynamic command: {description}");
            Logger.Debug($"[CmdProc] Complexity: {complexityLevel}, Safety: {safetyMode}, Optimization: {optimization}");

            if (string.IsNullOrEmpty(description))
            {
                Logger.Error("[CmdProc] Command description is required but not provided");
                return new { success = false, error = "Command description is required" };
            }

            // Извлекаем дополнительные параметры
            var commandParameters = new Dictionary<string, object>();
            
            // Добавляем настройки выполнения
            commandParameters["complexity_level"] = complexityLevel;
            commandParameters["safety_mode"] = safetyMode;
            commandParameters["optimization_level"] = optimization;

            // Извлекаем параметры из JSON
            if (parameters["parameters"] is JObject paramObj)
            {
                foreach (var prop in paramObj.Properties())
                {
                    commandParameters[prop.Name] = prop.Value.ToObject<object>();
                    Logger.Debug($"[CmdProc] Parameter: {prop.Name} = {prop.Value}");
                }
            }

            Logger.Debug($"[CmdProc] Total parameters prepared: {commandParameters.Count}");

            // Выполняем динамическую команду
            Logger.Debug("[CmdProc] Executing dynamic command via DynamicCommandManager");
            var task = _dynamicCommandManager.ExecuteCommand(description, _uiApp, commandParameters);
            var result = task.GetAwaiter().GetResult(); // Синхронное выполнение для MCP

            if (result.Success)
            {
                Logger.Info($"[CmdProc] Dynamic command completed successfully in {result.ExecutionTime.TotalMilliseconds:F0}ms");
                return new
                {
                    success = true,
                    message = result.Message,
                    data = result.Data,
                    executionTime = result.ExecutionTime.TotalMilliseconds,
                    elementsCreated = result.ElementsCreated,
                    elementsModified = result.ElementsModified,
                    affectedElements = result.AffectedElements?.Select(id => id.IntegerValue).ToList(),
                    warnings = result.Warnings,
                    generatedLines = EstimateCodeLines(description),
                    details = GenerateExecutionDetails(result)
                };
            }
            else
            {
                Logger.Error($"[CmdProc] Dynamic command failed: {result.Message}");
                return new
                {
                    success = false,
                    error = result.Message,
                    executionTime = result.ExecutionTime.TotalMilliseconds,
                    warnings = result.Warnings
                };
            }
        }
        catch (Exception ex)
        {
            Logger.Error("[CmdProc] Critical error processing dynamic command", ex);
            return new { success = false, error = ex.Message };
        }
    }

    /// <summary>
    /// Обрабатывает тестовую динамическую команду
    /// </summary>
    private object ProcessTestFixedDynamicCommand(JObject parameters)
    {
        try
        {
            var operation = parameters["operation"]?.ToString() ?? "create wall";
            
            Logger.Info($"Testing fixed dynamic command with operation: {operation}");

            // Создаем простую тестовую команду для проверки системы
            var testDescription = $"Создай простую тестовую операцию: {operation}. Используй безопасную транзакцию и выведи сообщение о результате.";

            var commandParameters = new Dictionary<string, object>
            {
                ["operation"] = operation,
                ["complexity_level"] = "simple",
                ["safety_mode"] = true,
                ["optimization_level"] = "speed"
            };

            // Выполняем тестовую динамическую команду
            var task = _dynamicCommandManager.ExecuteCommand(testDescription, _uiApp, commandParameters);
            var result = task.GetAwaiter().GetResult();

            if (result.Success)
            {
                return new
                {
                    success = true,
                    message = $"✅ Test operation '{operation}' completed successfully",
                    data = result.Data,
                    executionTime = result.ExecutionTime.TotalMilliseconds,
                    elementsCreated = result.ElementsCreated,
                    elementsModified = result.ElementsModified,
                    warnings = result.Warnings,
                    testResult = "Dynamic command system is working correctly"
                };
            }
            else
            {
                return new
                {
                    success = false,
                    error = result.Message,
                    executionTime = result.ExecutionTime.TotalMilliseconds,
                    warnings = result.Warnings,
                    testResult = "Dynamic command system has issues"
                };
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Error in test fixed dynamic command", ex);
            return new { 
                success = false, 
                error = ex.Message,
                testResult = "Critical error in dynamic command system"
            };
        }
    }

    /// <summary>
    /// Генерирует детали выполнения команды
    /// </summary>
    private string GenerateExecutionDetails(DynamicCommandResult result)
    {
        var details = new List<string>();

        if (result.ElementsCreated > 0)
        {
            details.Add($"✅ Created {result.ElementsCreated} new elements");
        }

        if (result.ElementsModified > 0)
        {
            details.Add($"🔄 Modified {result.ElementsModified} existing elements");
        }

        if (result.Data.Any())
        {
            details.Add("📊 Analysis Results:");
            foreach (var kvp in result.Data)
            {
                details.Add($"   • {kvp.Key}: {kvp.Value}");
            }
        }

        if (result.Warnings.Any())
        {
            details.Add("⚠️ Warnings:");
            foreach (var warning in result.Warnings)
            {
                details.Add($"   • {warning}");
            }
        }

        return string.Join("\n", details);
    }

    /// <summary>
    /// Оценивает количество строк сгенерированного кода
    /// </summary>
    private int EstimateCodeLines(string description)
    {
        // Простая эвристика для оценки сложности команды
        var words = description.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
        var baseLines = 20; // Базовое количество строк
        var additionalLines = Math.Min(words * 2, 100); // Дополнительные строки на основе описания
        
        return baseLines + additionalLines;
    }

    /// <summary>
    /// Обрабатывает команду создания умного трубопровода
    /// </summary>
    private object ProcessSmartCreatePipe(JObject parameters)
    {
        try
        {
            var start = ParsePoint(parameters["start"]);
            var end = ParsePoint(parameters["end"]);
            var diameter = parameters["diameter"]?.Value<double>() ?? 0.5;

            if (start == null || end == null)
            {
                return new { success = false, error = "Invalid start or end point" };
            }

            // Находим маршрут для трубопровода
            var routeResult = _mepPathfinder.FindPipeRoute(start, end, diameter);

            if (!routeResult.Success)
            {
                return new { success = false, error = routeResult.Message };
            }

            // Возвращаем результат для дальнейшего создания в Revit
            return new
            {
                success = true,
                route = new
                {
                    path = routeResult.Path.Select(p => new { x = p.X, y = p.Y, z = p.Z }),
                    fittings = routeResult.Fittings.Select(f => new
                    {
                        position = new { x = f.Position.X, y = f.Position.Y, z = f.Position.Z },
                        type = f.Type,
                        size = f.Size
                    }),
                    totalLength = routeResult.TotalLength
                },
                message = routeResult.Message
            };
        }
        catch (Exception ex)
        {
            Logger.Error("Error in smart pipe creation", ex);
            return new { success = false, error = ex.Message };
        }
    }

    /// <summary>
    /// Обрабатывает команду анализа пространства
    /// </summary>
    private object ProcessAnalyzeSpace(JObject parameters)
    {
        try
        {
            var center = ParsePoint(parameters["center"]);
            var radius = parameters["radius"]?.Value<double>() ?? 5.0;

            if (center == null)
            {
                return new { success = false, error = "Invalid center point" };
            }

            var boundingBox = new BoundingBoxXYZ
            {
                Min = new XYZ(center.X - radius, center.Y - radius, center.Z - radius),
                Max = new XYZ(center.X + radius, center.Y + radius, center.Z + radius)
            };

            var elements = _spaceAnalyzer.GetElementsInBoundingBox(boundingBox);
            var isSpaceAvailable = _spaceAnalyzer.IsSpaceAvailable(center, radius);

            return new
            {
                success = true,
                analysis = new
                {
                    center = new { x = center.X, y = center.Y, z = center.Z },
                    radius = radius,
                    isSpaceAvailable = isSpaceAvailable,
                    elementCount = elements.Count,
                    elements = elements.Take(10).Select(e => new
                    {
                        id = e.Id.IntegerValue,
                        category = e.Category?.Name ?? "Unknown",
                        name = e.Name
                    })
                }
            };
        }
        catch (Exception ex)
        {
            Logger.Error("Error in space analysis", ex);
            return new { success = false, error = ex.Message };
        }
    }

    /// <summary>
    /// Обрабатывает команду поиска оптимального маршрута
    /// </summary>
    private object ProcessFindOptimalRoute(JObject parameters)
    {
        try
        {
            var start = ParsePoint(parameters["start"]);
            var end = ParsePoint(parameters["end"]);
            var clearance = parameters["clearance"]?.Value<double>() ?? 0.5;

            if (start == null || end == null)
            {
                return new { success = false, error = "Invalid start or end point" };
            }

            var path = _routeCalculator.FindOptimalPath(start, end, clearance);
            var alternatives = _routeCalculator.FindAlternativePaths(start, end, clearance, 3);

            return new
            {
                success = true,
                routing = new
                {
                    optimalPath = path.Select(p => new { x = p.X, y = p.Y, z = p.Z }),
                    alternativePaths = alternatives.Select(alt => 
                        alt.Select(p => new { x = p.X, y = p.Y, z = p.Z })),
                    clearance = clearance
                }
            };
        }
        catch (Exception ex)
        {
            Logger.Error("Error finding optimal route", ex);
            return new { success = false, error = ex.Message };
        }
    }

    /// <summary>
    /// Обрабатывает команду получения препятствий
    /// </summary>
    private object ProcessGetObstacles(JObject parameters)
    {
        try
        {
            var start = ParsePoint(parameters["start"]);
            var end = ParsePoint(parameters["end"]);
            var clearance = parameters["clearance"]?.Value<double>() ?? 0.5;

            if (start == null || end == null)
            {
                return new { success = false, error = "Invalid start or end point" };
            }

            var obstacles = _spaceAnalyzer.FindObstaclesOnPath(start, end, clearance);

            return new
            {
                success = true,
                obstacles = new
                {
                    count = obstacles.Count,
                    items = obstacles.Select(o => new
                    {
                        id = o.Id.IntegerValue,
                        category = o.Category?.Name ?? "Unknown",
                        name = o.Name,
                        boundingBox = GetBoundingBoxInfo(o.get_BoundingBox(null))
                    })
                }
            };
        }
        catch (Exception ex)
        {
            Logger.Error("Error getting obstacles", ex);
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
            var cacheStats = _dynamicCommandManager.GetCacheStatistics();
            
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
                        cachedCommands = cacheStats.TotalCachedCommands,
                        validCommands = cacheStats.ValidCommands
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

    /// <summary>
    /// Парсит точку из JSON
    /// </summary>
    private XYZ ParsePoint(JToken pointToken)
    {
        try
        {
            if (pointToken == null) return null;

            var x = pointToken["x"]?.Value<double>() ?? 0;
            var y = pointToken["y"]?.Value<double>() ?? 0;
            var z = pointToken["z"]?.Value<double>() ?? 0;

            return new XYZ(x, y, z);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Получает информацию о ограничивающем прямоугольнике
    /// </summary>
    private object GetBoundingBoxInfo(BoundingBoxXYZ boundingBox)
    {
        if (boundingBox == null)
            return null;

        return new
        {
            min = new { x = boundingBox.Min.X, y = boundingBox.Min.Y, z = boundingBox.Min.Z },
            max = new { x = boundingBox.Max.X, y = boundingBox.Max.Y, z = boundingBox.Max.Z }
        };
    }
} 