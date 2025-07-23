using System;
using System.Collections.Generic;
using System.Linq;
using mymcp.Core;

namespace mymcp.Dynamic;

/// <summary>
/// Библиотека шаблонов для генерации динамических команд
/// </summary>
public class CommandTemplateLibrary
{
    private readonly Dictionary<string, CommandTemplate> _templates;

    public CommandTemplateLibrary()
    {
        _templates = new Dictionary<string, CommandTemplate>();
        LoadDefaultTemplates();
    }

    /// <summary>
    /// Находит лучший шаблон для намерения
    /// </summary>
    public CommandTemplate FindBestMatch(CommandIntent intent)
    {
        var bestTemplate = _templates.Values
            .Select(template => new { Template = template, Score = template.CalculateMatchScore(intent) })
            .Where(x => x.Score > 0.1) // Минимальный порог совпадения
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();

        if (bestTemplate != null)
        {
            Logger.Info($"Selected template: {bestTemplate.Template.Name} (score: {bestTemplate.Score:F2})");
            return bestTemplate.Template;
        }

        Logger.Warning("No suitable template found, using default");
        return GetDefaultTemplate();
    }

    /// <summary>
    /// Добавляет новый шаблон
    /// </summary>
    public void AddTemplate(CommandTemplate template)
    {
        _templates[template.Id] = template;
        Logger.Info($"Added template: {template.Name}");
    }

    /// <summary>
    /// Получает шаблон по ID
    /// </summary>
    public CommandTemplate GetTemplate(string id)
    {
        return _templates.TryGetValue(id, out var template) ? template : null;
    }

    /// <summary>
    /// Загружает предустановленные шаблоны
    /// </summary>
    private void LoadDefaultTemplates()
    {
        // Шаблон для создания HVAC систем
        AddTemplate(new CommandTemplate
        {
            Id = "hvac_create",
            Name = "Create HVAC System",
            Description = "Create HVAC systems including ducts, equipment and routing",
            Category = "HVAC",
            SecurityLevel = SecurityLevel.Moderate,
            Tags = new List<string> { "hvac", "duct", "create", "mechanical", "airflow" },
            RequiredNamespaces = new List<string> { "Autodesk.Revit.DB.Mechanical" },
            CodeTemplate = @"
// HVAC System Creation Template
// This template creates HVAC systems with intelligent routing

using (var transaction = new Transaction(doc, ""{{ACTION}} {{CATEGORY}} System""))
{
    transaction.Start();
    
    try
    {
        {{GENERATED_LOGIC}}
        
        transaction.Commit();
        result.ElementsCreated = 1; // Will be updated by specific logic
        result.Message = ""{{CATEGORY}} system created successfully"";
    }
    catch (Exception ex)
    {
        transaction.RollBack();
        throw;
    }
}"
        });

        // Шаблон для анализа пространства
        AddTemplate(new CommandTemplate
        {
            Id = "space_analysis",
            Name = "Space Analysis",
            Description = "Analyze building space for optimal equipment placement and routing",
            Category = "Analysis",
            SecurityLevel = SecurityLevel.Safe,
            Tags = new List<string> { "analyze", "space", "optimization", "placement" },
            CodeTemplate = @"
// Space Analysis Template
// This template analyzes building space and provides recommendations

var spaceAnalyzer = new SpaceAnalyzer(uiApp);

try
{
    {{GENERATED_LOGIC}}
    
    result.Message = ""Space analysis completed successfully"";
}
catch (Exception ex)
{
    Logger.Error(""Error during space analysis"", ex);
    throw;
}"
        });

        // Шаблон для создания сантехнических систем
        AddTemplate(new CommandTemplate
        {
            Id = "plumbing_create",
            Name = "Create Plumbing System", 
            Description = "Create plumbing systems including pipes, fixtures and routing",
            Category = "Plumbing",
            SecurityLevel = SecurityLevel.Moderate,
            Tags = new List<string> { "plumbing", "pipe", "create", "water", "mechanical" },
            RequiredNamespaces = new List<string> { "Autodesk.Revit.DB.Plumbing" },
            CodeTemplate = @"
// Plumbing System Creation Template
// This template creates plumbing systems with optimal routing

using (var transaction = new Transaction(doc, ""{{ACTION}} {{CATEGORY}} System""))
{
    transaction.Start();
    
    try
    {
        {{GENERATED_LOGIC}}
        
        transaction.Commit();
        result.ElementsCreated = 1; // Will be updated by specific logic
        result.Message = ""{{CATEGORY}} system created successfully"";
    }
    catch (Exception ex)
    {
        transaction.RollBack();
        throw;
    }
}"
        });

        // Шаблон для электрических систем
        AddTemplate(new CommandTemplate
        {
            Id = "electrical_create",
            Name = "Create Electrical System",
            Description = "Create electrical systems including cables, equipment and routing",
            Category = "Electrical",
            SecurityLevel = SecurityLevel.Moderate,
            Tags = new List<string> { "electrical", "cable", "create", "power", "lighting" },
            RequiredNamespaces = new List<string> { "Autodesk.Revit.DB.Electrical" },
            CodeTemplate = @"
// Electrical System Creation Template
// This template creates electrical systems with cable routing

var spaceAnalyzer = new SpaceAnalyzer(uiApp);
var routeCalculator = new RouteCalculator(spaceAnalyzer);

using (var transaction = new Transaction(doc, ""{{ACTION}} {{CATEGORY}} System""))
{
    transaction.Start();
    
    try
    {
        {{GENERATED_LOGIC}}
        
        transaction.Commit();
        result.ElementsCreated = 1; // Will be updated by specific logic
        result.Message = ""{{CATEGORY}} system created successfully"";
    }
    catch (Exception ex)
    {
        transaction.RollBack();
        throw;
    }
}"
        });

        // Шаблон для архитектурных элементов
        AddTemplate(new CommandTemplate
        {
            Id = "architecture_create",
            Name = "Create Architectural Elements",
            Description = "Create architectural elements like walls, floors, roofs",
            Category = "Architecture",
            SecurityLevel = SecurityLevel.Moderate,
            Tags = new List<string> { "architecture", "wall", "floor", "roof", "create", "structural" },
            CodeTemplate = @"
// Architectural Elements Creation Template
// This template creates architectural elements

var spaceAnalyzer = new SpaceAnalyzer(uiApp);

using (var transaction = new Transaction(doc, ""{{ACTION}} {{CATEGORY}} Elements""))
{
    transaction.Start();
    
    try
    {
        {{GENERATED_LOGIC}}
        
        transaction.Commit();
        result.ElementsCreated = 1; // Will be updated by specific logic
        result.Message = ""{{CATEGORY}} elements created successfully"";
    }
    catch (Exception ex)
    {
        transaction.RollBack();
        throw;
    }
}"
        });

        // Шаблон для оптимизации систем
        AddTemplate(new CommandTemplate
        {
            Id = "system_optimization",
            Name = "System Optimization",
            Description = "Optimize existing MEP systems for better performance",
            Category = "Optimization",
            SecurityLevel = SecurityLevel.High,
            Tags = new List<string> { "optimize", "performance", "efficiency", "analysis" },
            CodeTemplate = @"
// System Optimization Template
// This template optimizes existing MEP systems

using (var transaction = new Transaction(doc, ""{{ACTION}} {{CATEGORY}} System""))
{
    transaction.Start();
    
    try
    {
        {{GENERATED_LOGIC}}
        
        transaction.Commit();
        result.ElementsCreated = 1; // Will be updated by specific logic
        result.Message = ""{{CATEGORY}} system optimized successfully"";
    }
    catch (Exception ex)
    {
        transaction.RollBack();
        throw;
    }
}"
        });

        // Шаблон для комплексных многосистемных проектов
        AddTemplate(new CommandTemplate
        {
            Id = "complex_multisystem",
            Name = "Complex Multi-System Project",
            Description = "Create complex projects involving multiple MEP systems",
            Category = "Complex",
            SecurityLevel = SecurityLevel.Critical,
            Tags = new List<string> { "complex", "multisystem", "integration", "coordination" },
            RequiredNamespaces = new List<string> 
            { 
                "Autodesk.Revit.DB.Mechanical", 
                "Autodesk.Revit.DB.Plumbing", 
                "Autodesk.Revit.DB.Electrical" 
            },
            CodeTemplate = @"
// Complex Multi-System Template
// This template handles complex projects with multiple interconnected systems

var spaceAnalyzer = new SpaceAnalyzer(uiApp);
var routeCalculator = new RouteCalculator(spaceAnalyzer);
var mepPathfinder = new MEPPathfinder(spaceAnalyzer, routeCalculator, doc);

using (var transaction = new Transaction(doc, ""{{ACTION}} Complex {{CATEGORY}} Project""))
{
    transaction.Start();
    
    try
    {
        // Phase 1: Analysis and Planning
        Logger.Info(""Starting complex project analysis..."");
        
        {{GENERATED_LOGIC}}
        
        // Phase 2: Integration and Coordination
        Logger.Info(""Integrating systems..."");
        
        transaction.Commit();
        result.ElementsCreated = 1; // Will be updated by specific logic
        result.Message = ""Complex {{CATEGORY}} project completed successfully"";
    }
    catch (Exception ex)
    {
        transaction.RollBack();
        throw;
    }
}"
        });

        // Шаблон для операций удаления элементов
        AddTemplate(new CommandTemplate
        {
            Id = "delete_elements",
            Name = "Delete Elements",
            Description = "Delete elements from the document with proper transaction handling",
            Category = "Modification",
            SecurityLevel = SecurityLevel.High,
            Tags = new List<string> { "delete", "remove", "modify", "cleanup" },
            CodeTemplate = @"
// Element Deletion Template
// This template safely deletes elements with proper transaction handling

using (var transaction = new Transaction(doc, ""Delete {{CATEGORY}} Elements""))
{
    transaction.Start();
    
    try
    {
        Logger.Info(""Starting element deletion: {{DESCRIPTION}}"");
        
        {{GENERATED_LOGIC}}
        
        transaction.Commit();
        result.Message = ""Elements deleted successfully"";
        Logger.Info(""Element deletion completed successfully"");
    }
    catch (Exception ex)
    {
        transaction.RollBack();
        Logger.Error(""Error during element deletion"", ex);
        throw;
    }
}"
        });

        Logger.Info($"Loaded {_templates.Count} default command templates");
    }

    /// <summary>
    /// Получает шаблон по умолчанию
    /// </summary>
    private CommandTemplate GetDefaultTemplate()
    {
        return new CommandTemplate
        {
            Id = "default",
            Name = "Default Command",
            Description = "Default template for general commands",
            Category = "General",
            SecurityLevel = SecurityLevel.Safe,
            Tags = new List<string> { "general", "default" },
            CodeTemplate = @"
// Default Command Template
// This template provides a safe execution environment for general commands

try
{
    Logger.Info(""Executing default command: {{DESCRIPTION}}"");
    
    {{GENERATED_LOGIC}}
    
    result.Message = ""Command executed successfully"";
}
catch (Exception ex)
{
    Logger.Error(""Error executing command"", ex);
    throw;
}"
        };
    }

    /// <summary>
    /// Получает все доступные шаблоны
    /// </summary>
    public IEnumerable<CommandTemplate> GetAllTemplates()
    {
        return _templates.Values;
    }

    /// <summary>
    /// Получает шаблоны по категории
    /// </summary>
    public IEnumerable<CommandTemplate> GetTemplatesByCategory(string category)
    {
        return _templates.Values.Where(t => 
            string.Equals(t.Category, category, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Получает шаблоны по тегу
    /// </summary>
    public IEnumerable<CommandTemplate> GetTemplatesByTag(string tag)
    {
        return _templates.Values.Where(t => 
            t.Tags.Any(tg => string.Equals(tg, tag, StringComparison.OrdinalIgnoreCase)));
    }
} 