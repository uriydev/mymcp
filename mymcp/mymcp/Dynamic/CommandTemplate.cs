using System;
using System.Collections.Generic;
using System.Linq;

namespace mymcp.Dynamic;

/// <summary>
/// Шаблон для генерации динамических команд
/// </summary>
public class CommandTemplate
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Category { get; set; }
    public SecurityLevel SecurityLevel { get; set; }
    public string CodeTemplate { get; set; }
    public List<TemplateParameter> Parameters { get; set; } = new();
    public List<string> RequiredNamespaces { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public string CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }

    /// <summary>
    /// Проверяет, подходит ли шаблон для данного намерения
    /// </summary>
    public double CalculateMatchScore(CommandIntent intent)
    {
        double score = 0;

        // Проверяем совпадение по тегам
        var commonTags = Tags.Intersect(intent.Tags, StringComparer.OrdinalIgnoreCase).Count();
        score += commonTags * 0.3;

        // Проверяем совпадение по категории
        if (string.Equals(Category, intent.Category, StringComparison.OrdinalIgnoreCase))
        {
            score += 0.4;
        }

        // Проверяем совпадение по описанию (простой текстовый анализ)
        var descriptionWords = Description.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var intentWords = intent.Description.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var commonWords = descriptionWords.Intersect(intentWords, StringComparer.OrdinalIgnoreCase).Count();
        
        if (descriptionWords.Length > 0)
        {
            score += (double)commonWords / descriptionWords.Length * 0.3;
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