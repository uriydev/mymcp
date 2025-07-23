using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using mymcp.Core;
using System.Linq;
using Nice3point.Revit.Toolkit.External.Handlers;

namespace mymcp.Dynamic;

/// <summary>
/// Менеджер динамических команд
/// </summary>
public class DynamicCommandManager
{
    private readonly AICodeGenerator _codeGenerator;
    private readonly DynamicCommandCompiler _compiler;
    private readonly CommandTemplateLibrary _templateLibrary;
    private readonly ConcurrentDictionary<string, CompiledCommandCache> _commandCache;
    private readonly ActionEventHandler _actionEventHandler;

    public DynamicCommandManager()
    {
        _templateLibrary = new CommandTemplateLibrary();
        _codeGenerator = new AICodeGenerator();
        _compiler = new DynamicCommandCompiler();
        _commandCache = new ConcurrentDictionary<string, CompiledCommandCache>();
        _actionEventHandler = new ActionEventHandler();
    }

    /// <summary>
    /// Выполняет динамическую команду на основе описания
    /// </summary>
    public async Task<DynamicCommandResult> ExecuteCommand(
        string naturalLanguageDescription, 
        UIApplication uiApp, 
        Dictionary<string, object> parameters = null)
    {
        var startTime = DateTime.Now;
        try
        {
            Logger.Info($"[DynCmd] Starting execution of dynamic command: {naturalLanguageDescription}");
            Logger.Debug($"[DynCmd] Parameters count: {parameters?.Count ?? 0}");

            // 1. Проверяем кэш
            var cacheKey = GenerateCacheKey(naturalLanguageDescription, parameters);
            Logger.Debug($"[DynCmd] Cache key generated: {cacheKey.Substring(0, Math.Min(16, cacheKey.Length))}...");
            
            if (_commandCache.TryGetValue(cacheKey, out var cachedCommand) && 
                cachedCommand.IsValid && 
                DateTime.Now - cachedCommand.CreatedAt < TimeSpan.FromHours(1))
            {
                Logger.Info("[DynCmd] Using cached compiled command");
                return await ExecuteCompiledCommand(cachedCommand.Command, uiApp, parameters ?? new Dictionary<string, object>());
            }
            Logger.Debug("[DynCmd] No valid cache found, proceeding with generation");

            // 2. Генерируем команду
            Logger.Debug("[DynCmd] Step 1: Generating command code");
            var generatedCommand = _codeGenerator.GenerateCommand(naturalLanguageDescription, parameters).Result;
            Logger.Info($"[DynCmd] Command generated successfully - Template: {generatedCommand.Template.Name}");

            // 3. Компилируем команду
            Logger.Debug("[DynCmd] Step 2: Compiling generated command");
            var compilationResult = _compiler.CompileCommand(generatedCommand).Result;
            
            if (!compilationResult.Success)
            {
                Logger.Error($"[DynCmd] Compilation failed: {string.Join(", ", compilationResult.Errors)}");
                return new DynamicCommandResult
                {
                    Success = false,
                    Message = $"Compilation failed: {string.Join(", ", compilationResult.Errors)}",
                    Error = new InvalidOperationException("Code compilation failed"),
                    ExecutionTime = DateTime.Now - startTime
                };
            }
            Logger.Info("[DynCmd] Command compiled successfully");

            // 4. Кэшируем команду
            Logger.Debug("[DynCmd] Step 3: Caching compiled command");
            _commandCache.TryAdd(cacheKey, new CompiledCommandCache
            {
                Command = compilationResult.CompiledCommand,
                CreatedAt = DateTime.Now,
                IsValid = true,
                GeneratedCode = compilationResult.GeneratedCode
            });
            Logger.Debug($"[DynCmd] Command cached. Total cached commands: {_commandCache.Count}");

            // 5. Выполняем команду
            Logger.Debug("[DynCmd] Step 4: Executing compiled command");
            var result = ExecuteCompiledCommand(compilationResult.CompiledCommand, uiApp, parameters ?? new Dictionary<string, object>()).Result;
            
            Logger.Info($"[DynCmd] Dynamic command execution completed - Success: {result.Success}, Time: {result.ExecutionTime.TotalMilliseconds:F0}ms");
            return result;
        }
        catch (Exception ex)
        {
            Logger.Error($"[DynCmd] Critical error executing dynamic command '{naturalLanguageDescription}'", ex);
            return new DynamicCommandResult
            {
                Success = false,
                Message = $"Execution failed: {ex.Message}",
                Error = ex,
                ExecutionTime = DateTime.Now - startTime
            };
        }
    }

    /// <summary>
    /// Выполняет скомпилированную команду
    /// </summary>
    private async Task<DynamicCommandResult> ExecuteCompiledCommand(
        IDynamicCommand command, 
        UIApplication uiApp, 
        Dictionary<string, object> parameters)
    {
        try
        {
            Logger.Info($"[ExecCmd] Executing compiled command: {command.Name}");
            Logger.Debug($"[ExecCmd] Security level: {command.SecurityLevel}");

            // Валидируем параметры
            var validationResult = command.ValidateParameters(parameters);
            if (!validationResult.IsValid)
            {
                Logger.Error($"[ExecCmd] Parameter validation failed: {string.Join(", ", validationResult.Errors)}");
                return new DynamicCommandResult
                {
                    Success = false,
                    Message = $"Parameter validation failed: {string.Join(", ", validationResult.Errors)}",
                    Warnings = validationResult.Warnings
                };
            }
            
            // Выполняем команду через ActionEventHandler для правильного контекста Revit
            Logger.Debug("[ExecCmd] Executing command via ActionEventHandler");
            
            DynamicCommandResult result = null;
            Exception executionException = null;
            var resetEvent = new System.Threading.ManualResetEvent(false);
            
            _actionEventHandler.Raise(application =>
            {
                try
                {
                    result = command.Execute(application, parameters);
                }
                catch (Exception ex)
                {
                    executionException = ex;
                }
                finally
                {
                    resetEvent.Set();
                }
            });

            // Ждем завершения выполнения
            resetEvent.WaitOne(TimeSpan.FromSeconds(30)); // Таймаут 30 секунд
            resetEvent.Dispose();
            
            if (executionException != null)
            {
                throw executionException;
            }
            
            Logger.Info($"[ExecCmd] Command executed - Success: {result?.Success}, Elements created: {result?.ElementsCreated}");
            
            return result ?? new DynamicCommandResult
            {
                Success = false,
                Message = "Command execution returned null result"
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"[ExecCmd] Error executing compiled command: {command.Name}", ex);
            return new DynamicCommandResult
            {
                Success = false,
                Message = $"Command execution failed: {ex.Message}",
                Error = ex
            };
        }
    }

    /// <summary>
    /// Генерирует ключ кэша
    /// </summary>
    private string GenerateCacheKey(string description, Dictionary<string, object> parameters)
    {
        var paramString = parameters != null ? 
            string.Join(",", parameters.Select(kvp => $"{kvp.Key}={kvp.Value}")) : "";
        
        return $"{description}|{paramString}".GetHashCode().ToString();
    }

    /// <summary>
    /// Очищает кэш команд
    /// </summary>
    public void ClearCommandCache()
    {
        _commandCache.Clear();
        Logger.Info("Command cache cleared");
    }

    /// <summary>
    /// Получает статистику по кэшу
    /// </summary>
    public CommandCacheStatistics GetCacheStatistics()
    {
        return new CommandCacheStatistics
        {
            TotalCachedCommands = _commandCache.Count,
            ValidCommands = _commandCache.Values.Count(c => c.IsValid),
            OldestCommandAge = _commandCache.Values.Any() ? 
                DateTime.Now - _commandCache.Values.Min(c => c.CreatedAt) : TimeSpan.Zero
        };
    }

    /// <summary>
    /// Добавляет новый шаблон команды
    /// </summary>
    public void AddCommandTemplate(CommandTemplate template)
    {
        _templateLibrary.AddTemplate(template);
        Logger.Info($"Added new command template: {template.Name}");
    }

    /// <summary>
    /// Получает доступные шаблоны
    /// </summary>
    public IEnumerable<CommandTemplate> GetAvailableTemplates()
    {
        return _templateLibrary.GetAllTemplates();
    }

    /// <summary>
    /// Предварительно компилирует часто используемые команды
    /// </summary>
    public async Task PrecompileCommonCommands()
    {
        var commonCommands = new[]
        {
            "Создай воздуховод между двумя точками",
            "Проанализируй пространство модели",
            "Создай трубопровод с оптимальным маршрутом",
            "Оптимизируй существующие MEP системы"
        };

        Logger.Info("Precompiling common commands...");

        foreach (var command in commonCommands)
        {
            try
            {
                var generated = _codeGenerator.GenerateCommand(command).Result;
                var compiled = _compiler.CompileCommand(generated).Result;
                
                if (compiled.Success)
                {
                    var cacheKey = GenerateCacheKey(command, null);
                    _commandCache.TryAdd(cacheKey, new CompiledCommandCache
                    {
                        Command = compiled.CompiledCommand,
                        CreatedAt = DateTime.Now,
                        IsValid = true,
                        GeneratedCode = compiled.GeneratedCode
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error precompiling command: {command}", ex);
            }
        }

        Logger.Info($"Precompilation completed. Cached {_commandCache.Count} commands.");
    }
}

/// <summary>
/// Кэш скомпилированной команды
/// </summary>
public class CompiledCommandCache
{
    public IDynamicCommand Command { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsValid { get; set; }
    public string GeneratedCode { get; set; }
}

/// <summary>
/// Статистика кэша команд
/// </summary>
public class CommandCacheStatistics
{
    public int TotalCachedCommands { get; set; }
    public int ValidCommands { get; set; }
    public TimeSpan OldestCommandAge { get; set; }
} 