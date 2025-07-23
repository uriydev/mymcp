using System;
using System.Collections.Generic;
using System.Linq;

namespace mymcp.Dynamic;

/// <summary>
/// Шаблон команды для генерации динамических команд
/// </summary>
public class CommandTemplate
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Category { get; set; }
    public SecurityLevel SecurityLevel { get; set; }
    public List<string> Tags { get; set; } = new List<string>();
    public List<string> RequiredNamespaces { get; set; } = new List<string>();
    public string Code { get; set; }
    
    /// <summary>
    /// Вычисляет совпадение с намерением команды
    /// </summary>
    public double CalculateMatch(CommandIntent intent)
    {
        double score = 0;

        // Проверяем категорию
        if (Category?.Equals(intent.Category, StringComparison.OrdinalIgnoreCase) == true)
        {
            score += 0.5;
        }

        // Проверяем теги
        if (Tags != null && Tags.Count > 0)
        {
            var matchingTags = Tags.Count(tag => 
                intent.Description.Contains(tag, StringComparison.OrdinalIgnoreCase));
            score += (double)matchingTags / Tags.Count * 0.3;
        }

        // Проверяем действие
        if (Tags != null && !string.IsNullOrEmpty(intent.MainAction))
        {
            if (Tags.Contains(intent.MainAction, StringComparer.OrdinalIgnoreCase))
            {
                score += 0.2;
            }
        }

        return Math.Min(score, 1.0);
    }
}

/// <summary>
/// Параметр шаблона команды
/// </summary>
public class TemplateParameter
{
    public string Name { get; set; }
    public string Type { get; set; }
    public bool IsRequired { get; set; }
    public object DefaultValue { get; set; }
    public string Description { get; set; }
    public List<string> AllowedValues { get; set; } = new();
    public string ValidationPattern { get; set; }
}

/// <summary>
/// Намерение пользователя, извлеченное из естественного языка
/// </summary>
public class CommandIntent
{
    public string Description { get; set; }
    public string MainAction { get; set; }
    public string Category { get; set; }
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, object> Parameters { get; set; } = new();
    public SecurityLevel RequiredSecurityLevel { get; set; }
    public double Confidence { get; set; }
}

/// <summary>
/// Результат генерации команды
/// </summary>
public class GeneratedCommand
{
    public string Code { get; set; }
    public CommandTemplate Template { get; set; }
    public CommandIntent Intent { get; set; }
    public int EstimatedComplexity { get; set; }
    public TimeSpan EstimatedExecutionTime { get; set; }
    public List<string> Dependencies { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
} 