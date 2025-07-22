using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace mymcp.Dynamic;

/// <summary>
/// Интерфейс для динамически генерируемых команд
/// </summary>
public interface IDynamicCommand
{
    /// <summary>
    /// Имя команды
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Описание команды
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Уровень безопасности команды
    /// </summary>
    SecurityLevel SecurityLevel { get; }

    /// <summary>
    /// Выполнить команду
    /// </summary>
    /// <param name="uiApp">Приложение Revit</param>
    /// <param name="parameters">Параметры команды</param>
    /// <returns>Результат выполнения</returns>
    DynamicCommandResult Execute(UIApplication uiApp, Dictionary<string, object> parameters);

    /// <summary>
    /// Валидация параметров перед выполнением
    /// </summary>
    /// <param name="parameters">Параметры для проверки</param>
    /// <returns>Результат валидации</returns>
    ValidationResult ValidateParameters(Dictionary<string, object> parameters);
}

/// <summary>
/// Уровень безопасности команды
/// </summary>
public enum SecurityLevel
{
    Safe = 0,      // Безопасные операции чтения
    Moderate = 1,  // Операции создания/модификации
    High = 2,      // Сложные операции с несколькими элементами
    Critical = 3   // Операции, влияющие на всю модель
}

/// <summary>
/// Результат выполнения динамической команды
/// </summary>
public class DynamicCommandResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public Exception Error { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public int ElementsCreated { get; set; }
    public int ElementsModified { get; set; }
    public List<ElementId> AffectedElements { get; set; } = new();
}

/// <summary>
/// Результат валидации параметров
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
} 