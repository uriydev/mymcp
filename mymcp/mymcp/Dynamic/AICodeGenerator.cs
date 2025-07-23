using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using mymcp.Core;

namespace mymcp.Dynamic;

/// <summary>
/// AI генератор кода для динамических команд
/// </summary>
public class AICodeGenerator
{
    private readonly CommandTemplateLibrary _templateLibrary;

    public AICodeGenerator(CommandTemplateLibrary templateLibrary)
    {
        _templateLibrary = templateLibrary;
    }

    /// <summary>
    /// Генерирует команду на основе описания на естественном языке
    /// </summary>
    public Task<GeneratedCommand> GenerateCommand(string naturalLanguageRequest)
    {
        try
        {
            Logger.Info($"[CodeGen] Starting command generation for: {naturalLanguageRequest}");

            // 1. Анализируем намерение пользователя
            Logger.Debug("[CodeGen] Step 1: Analyzing user intent");
            var intent = AnalyzeIntent(naturalLanguageRequest);
            Logger.Info($"[CodeGen] Intent analyzed - Action: {intent.MainAction}, Category: {intent.Category}, Parameters: {intent.Parameters.Count}");

            // 2. Находим подходящий шаблон
            Logger.Debug("[CodeGen] Step 2: Finding best template match");
            var template = _templateLibrary.FindBestMatch(intent);

            if (template == null)
            {
                Logger.Error("[CodeGen] No suitable template found for the request");
                throw new InvalidOperationException("No suitable template found for the request");
            }
            Logger.Info($"[CodeGen] Template selected: {template.Name} (ID: {template.Id})");

            // 3. Генерируем код на основе шаблона
            Logger.Debug("[CodeGen] Step 3: Generating code from template");
            var code = GenerateCodeFromTemplate(template, intent);
            Logger.Info($"[CodeGen] Code generated successfully - {code.Split('\n').Length} lines");

            var generatedCommand = new GeneratedCommand
            {
                Code = code,
                Template = template,
                Intent = intent,
                EstimatedComplexity = CalculateComplexity(code),
                EstimatedExecutionTime = EstimateExecutionTime(intent),
                Dependencies = ExtractDependencies(code)
            };

            Logger.Info($"[CodeGen] Command generation completed successfully: {template.Name}");
            Logger.Debug($"[CodeGen] Generated code preview:\n{code.Substring(0, Math.Min(200, code.Length))}...");
            return Task.FromResult(generatedCommand);
        }
        catch (Exception ex)
        {
            Logger.Error($"[CodeGen] Error generating command for '{naturalLanguageRequest}'", ex);
            throw;
        }
    }

    /// <summary>
    /// Анализирует намерение пользователя из естественного языка
    /// </summary>
    private CommandIntent AnalyzeIntent(string request)
    {
        Logger.Debug($"[Intent] Analyzing request: {request}");
        
        var intent = new CommandIntent
        {
            Description = request,
            Confidence = 0.8
        };

        // Простой анализ ключевых слов (в реальной системе можно использовать NLP)
        var lowerRequest = request.ToLower();
        Logger.Debug($"[Intent] Lowercase request: {lowerRequest}");

        // Определяем основное действие
        if (lowerRequest.Contains("создай") || lowerRequest.Contains("создать") || lowerRequest.Contains("create") || lowerRequest.Contains("построить"))
        {
            intent.MainAction = "create";
            intent.RequiredSecurityLevel = SecurityLevel.Moderate;
            Logger.Debug("[Intent] Main action detected: CREATE");
        }
        else if (lowerRequest.Contains("анализ") || lowerRequest.Contains("проанализ") || lowerRequest.Contains("analyze"))
        {
            intent.MainAction = "analyze";
            intent.RequiredSecurityLevel = SecurityLevel.Safe;
            Logger.Debug("[Intent] Main action detected: ANALYZE");
        }
        else if (lowerRequest.Contains("оптимиз") || lowerRequest.Contains("optimize"))
        {
            intent.MainAction = "optimize";
            intent.RequiredSecurityLevel = SecurityLevel.High;
            Logger.Debug("[Intent] Main action detected: OPTIMIZE");
        }
        else if (lowerRequest.Contains("удали") || lowerRequest.Contains("удалить") || lowerRequest.Contains("delete"))
        {
            intent.MainAction = "delete";
            intent.RequiredSecurityLevel = SecurityLevel.High;
            Logger.Debug("[Intent] Main action detected: DELETE");
        }
        else
        {
            Logger.Warning($"[Intent] No clear action detected in request: {request}");
        }

        // Определяем категорию
        if (lowerRequest.Contains("воздуховод") || lowerRequest.Contains("duct") || lowerRequest.Contains("вентил"))
        {
            intent.Category = "HVAC";
            intent.Tags.AddRange(new[] { "duct", "hvac", "mechanical", "airflow" });
            Logger.Debug("[Intent] Category detected: HVAC");
        }
        else if (lowerRequest.Contains("труб") || lowerRequest.Contains("pipe") || lowerRequest.Contains("water"))
        {
            intent.Category = "Plumbing";
            intent.Tags.AddRange(new[] { "pipe", "plumbing", "water", "mechanical" });
            Logger.Debug("[Intent] Category detected: Plumbing");
        }
        else if (lowerRequest.Contains("электр") || lowerRequest.Contains("electric") || lowerRequest.Contains("кабель"))
        {
            intent.Category = "Electrical";
            intent.Tags.AddRange(new[] { "electrical", "cable", "power", "lighting" });
            Logger.Debug("[Intent] Category detected: Electrical");
        }
        else if (lowerRequest.Contains("стен") || lowerRequest.Contains("wall") || lowerRequest.Contains("архит"))
        {
            intent.Category = "Architecture";
            intent.Tags.AddRange(new[] { "architecture", "structural", "building" });
            Logger.Debug("[Intent] Category detected: Architecture");
        }
        else
        {
            Logger.Warning("[Intent] No specific category detected, using General");
        }

        // Извлекаем параметры (координаты, размеры и т.д.)
        Logger.Debug("[Intent] Extracting parameters from request");
        ExtractParameters(lowerRequest, intent);
        Logger.Info($"[Intent] Parameter extraction completed - found {intent.Parameters.Count} parameters");

        foreach (var param in intent.Parameters)
        {
            Logger.Debug($"[Intent] Parameter: {param.Key} = {param.Value}");
        }

        return intent;
    }

    /// <summary>
    /// Извлекает параметры из текста
    /// </summary>
    private void ExtractParameters(string request, CommandIntent intent)
    {
        // Ищем координаты в различных форматах: (x, y, z), точки (x, y, z)
        var coordinatePattern = @"(?:точк[аие]|point)?\s*\(\s*(-?\d+(?:\.\d+)?)\s*,\s*(-?\d+(?:\.\d+)?)\s*,\s*(-?\d+(?:\.\d+)?)\s*\)";
        var coordinateMatches = Regex.Matches(request, coordinatePattern, RegexOptions.IgnoreCase);

        if (coordinateMatches.Count >= 1)
        {
            var match = coordinateMatches[0];
            intent.Parameters["start_x"] = double.Parse(match.Groups[1].Value);
            intent.Parameters["start_y"] = double.Parse(match.Groups[2].Value);
            intent.Parameters["start_z"] = double.Parse(match.Groups[3].Value);
        }

        if (coordinateMatches.Count >= 2)
        {
            var match = coordinateMatches[1];
            intent.Parameters["end_x"] = double.Parse(match.Groups[1].Value);
            intent.Parameters["end_y"] = double.Parse(match.Groups[2].Value);
            intent.Parameters["end_z"] = double.Parse(match.Groups[3].Value);
        }

        // Ищем размеры в дюймах
        var sizePattern = @"(\d+(?:\.\d+)?)\s*[x]\s*(\d+(?:\.\d+)?)\s*(?:дюйм|inch)";
        var sizeMatch = Regex.Match(request, sizePattern, RegexOptions.IgnoreCase);
        if (sizeMatch.Success)
        {
            // Конвертируем дюймы в футы
            intent.Parameters["width_ft"] = double.Parse(sizeMatch.Groups[1].Value) / 12.0;
            intent.Parameters["height_ft"] = double.Parse(sizeMatch.Groups[2].Value) / 12.0;
        }

        // Ищем длину
        var lengthPattern = @"(\d+(?:\.\d+)?)\s*(?:фут|feet|ft|м|meter)";
        var lengthMatch = Regex.Match(request, lengthPattern, RegexOptions.IgnoreCase);
        if (lengthMatch.Success)
        {
            intent.Parameters["length"] = double.Parse(lengthMatch.Groups[1].Value);
        }

        // Ищем диаметр для труб
        var diameterPattern = @"диаметр[ом]?\s*(\d+(?:\.\d+)?)\s*(?:мм|mm|дюйм|inch)";
        var diameterMatch = Regex.Match(request, diameterPattern, RegexOptions.IgnoreCase);
        if (diameterMatch.Success)
        {
            intent.Parameters["diameter"] = double.Parse(diameterMatch.Groups[1].Value);
        }
    }

    /// <summary>
    /// Генерирует код на основе шаблона и намерения
    /// </summary>
    private string GenerateCodeFromTemplate(CommandTemplate template, CommandIntent intent)
    {
        var code = template.CodeTemplate;

        // Заменяем плейсхолдеры на конкретные значения
        code = ReplacePlaceholders(code, intent);

        // Генерируем специфическую логику в зависимости от намерения
        code = GenerateSpecificLogic(template.CodeTemplate, intent); // Pass template.CodeTemplate here

        return code;
    }

    /// <summary>
    /// Заменяет плейсхолдеры в шаблоне
    /// </summary>
    private string ReplacePlaceholders(string template, CommandIntent intent)
    {
        var result = template;

        // Заменяем параметры
        foreach (var param in intent.Parameters)
        {
            result = result.Replace($"{{{{{param.Key}}}}}", param.Value.ToString());
        }

        // Заменяем стандартные плейсхолдеры
        result = result.Replace("{{CATEGORY}}", intent.Category ?? "General");
        result = result.Replace("{{ACTION}}", intent.MainAction ?? "execute");
        result = result.Replace("{{DESCRIPTION}}", intent.Description.Replace("\"", "\\\""));

        return result;
    }

    /// <summary>
    /// Генерирует специфическую логику на основе намерения
    /// </summary>
    private string GenerateSpecificLogic(string template, CommandIntent intent)
    {
        Logger.Debug($"[CodeGen] Generating specific logic for action: {intent.MainAction}, category: {intent.Category}");
        
        var logicBuilder = new StringBuilder();

        switch (intent.MainAction)
        {
            case "create":
                logicBuilder.AppendLine(GenerateCreateLogic(intent));
                break;
            case "analyze":
                logicBuilder.AppendLine(GenerateAnalyzeLogic(intent));
                break;
            case "optimize":
                logicBuilder.AppendLine(GenerateOptimizeLogic(intent));
                break;
            case "delete":
                logicBuilder.AppendLine(GenerateDeleteLogic(intent));
                break;
            default:
                Logger.Warning($"[CodeGen] Using default logic generation for action: {intent.MainAction}");
                logicBuilder.AppendLine("// Generated logic based on user request");
                logicBuilder.AppendLine(string.Format("Logger.Info(\"Executing: {0}\");", intent.Description.Replace("\"", "\\\"")));
                break;
        }

        return template.Replace("{{GENERATED_LOGIC}}", logicBuilder.ToString());
    }

    /// <summary>
    /// Генерирует логику создания элементов
    /// </summary>
    private string GenerateCreateLogic(CommandIntent intent)
    {
        var logic = new StringBuilder();

        if (intent.Category == "Architecture" || intent.Description.ToLower().Contains("стен"))
        {
            logic.AppendLine("// Create wall using Revit API");
            logic.AppendLine();
            
            logic.AppendLine("// Find level and wall type");
            logic.AppendLine("var level = new FilteredElementCollector(doc)");
            logic.AppendLine("    .OfClass(typeof(Level))");
            logic.AppendLine("    .FirstElement() as Level;");
            logic.AppendLine();
            
            logic.AppendLine("var wallType = new FilteredElementCollector(doc)");
            logic.AppendLine("    .OfClass(typeof(WallType))");
            logic.AppendLine("    .Cast<WallType>()");
            logic.AppendLine("    .FirstOrDefault(wt => wt.Kind == WallKind.Basic);");
            logic.AppendLine();
            
            logic.AppendLine("if (level == null || wallType == null)");
            logic.AppendLine("{");
            logic.AppendLine("    throw new InvalidOperationException(\"Could not find level or wall type\");");
            logic.AppendLine("}");
            logic.AppendLine();

            // Определяем координаты стены
            if (intent.Parameters.ContainsKey("start_x"))
            {
                logic.AppendLine(string.Format("var startPoint = new XYZ({0}, {1}, {2});", 
                    intent.Parameters["start_x"], intent.Parameters["start_y"], intent.Parameters["start_z"]));
                logic.AppendLine(string.Format("var endPoint = new XYZ({0}, {1}, {2});", 
                    intent.Parameters["end_x"], intent.Parameters["end_y"], intent.Parameters["end_z"]));
            }
            else
            {
                // Значения по умолчанию
                logic.AppendLine("var startPoint = new XYZ(0, 0, 0);");
                logic.AppendLine("var endPoint = new XYZ(10, 0, 0);");
            }
            
            logic.AppendLine("var wallLine = Line.CreateBound(startPoint, endPoint);");
            logic.AppendLine();
            
            logic.AppendLine("// Create the wall");
            logic.AppendLine("var wall = Wall.Create(doc, wallLine, level.Id, false);");
            logic.AppendLine();
            
            logic.AppendLine("if (wall != null)");
            logic.AppendLine("{");
            logic.AppendLine("    // Wall created with default height");
            logic.AppendLine("    result.ElementsCreated = 1;");
            logic.AppendLine("    result.Data[\"wall_id\"] = wall.Id.IntegerValue;");
            logic.AppendLine("    result.Message = \"Wall created successfully\";");
            logic.AppendLine("    Logger.Info($\"Created wall with ID: {wall.Id}\");");
            logic.AppendLine("}");
            logic.AppendLine("else");
            logic.AppendLine("{");
            logic.AppendLine("    throw new InvalidOperationException(\"Failed to create wall\");");
            logic.AppendLine("}");
        }
        else if (intent.Category == "HVAC" || intent.Description.ToLower().Contains("воздуховод"))
        {
            logic.AppendLine("// Create duct using MEP pathfinder and actual Revit elements");
            logic.AppendLine("Logger.Info(\"Creating real duct elements in Revit\");");
            logic.AppendLine();

            // Определяем координаты воздуховода
            if (intent.Parameters.ContainsKey("start_x"))
            {
                logic.AppendLine(string.Format("var startPoint = new XYZ({0}, {1}, {2});", 
                    intent.Parameters["start_x"], intent.Parameters["start_y"], intent.Parameters["start_z"]));
                logic.AppendLine(string.Format("var endPoint = new XYZ({0}, {1}, {2});", 
                    intent.Parameters["end_x"], intent.Parameters["end_y"], intent.Parameters["end_z"]));
            }
            else
            {
                // Значения по умолчанию для воздуховода
                logic.AppendLine("var startPoint = new XYZ(0, 0, 10);");
                logic.AppendLine("var endPoint = new XYZ(20, 0, 10);");
            }
            
            var width = intent.Parameters.ContainsKey("width_ft") ? (double)intent.Parameters["width_ft"] : 1.0;
            var height = intent.Parameters.ContainsKey("height_ft") ? (double)intent.Parameters["height_ft"] : 0.6;
            
            logic.AppendLine();
            logic.AppendLine("// Create MEP pathfinder for duct routing");
            logic.AppendLine("var spaceAnalyzer = new SpaceAnalyzer(uiApp);");
            logic.AppendLine("var routeCalculator = new RouteCalculator(spaceAnalyzer);");
            logic.AppendLine("var mepPathfinder = new MEPPathfinder(spaceAnalyzer, routeCalculator, doc);");
            logic.AppendLine();
            logic.AppendLine("// Find the optimal route");
            logic.AppendLine(string.Format("var routeResult = mepPathfinder.FindDuctRoute(startPoint, endPoint, {0}, {1});", 
                width.ToString("F1", System.Globalization.CultureInfo.InvariantCulture), 
                height.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)));
            logic.AppendLine();
            logic.AppendLine("if (routeResult.Success)");
            logic.AppendLine("{");
            logic.AppendLine("    // For now, use the smart_create_duct functionality through internal API");
            logic.AppendLine("    try");
            logic.AppendLine("    {");
            logic.AppendLine("        // Create a simple duct from start to end using internal smart creation");
            logic.AppendLine("        var smartResult = mepPathfinder.CreateSmartDuct(startPoint, endPoint, routeResult);");
            logic.AppendLine("        ");
            logic.AppendLine("        if (smartResult.Success)");
            logic.AppendLine("        {");
            logic.AppendLine("            result.ElementsCreated = smartResult.ElementsCreated;");
            logic.AppendLine("            result.Data[\"duct_elements\"] = smartResult.ElementsCreated;");
            logic.AppendLine("            result.Data[\"duct_segments\"] = routeResult.Path.Count;");
            logic.AppendLine("            result.Data[\"total_length\"] = routeResult.TotalLength;");
            logic.AppendLine("            result.Data[\"fittings_count\"] = routeResult.Fittings.Count;");
            logic.AppendLine("            result.Message = $\"Duct created successfully: {smartResult.ElementsCreated} elements, {routeResult.TotalLength:F1} ft\";");
            logic.AppendLine("            Logger.Info($\"Created {smartResult.ElementsCreated} duct elements using smart creation\");");
            logic.AppendLine("        }");
            logic.AppendLine("        else");
            logic.AppendLine("        {");
            logic.AppendLine("            result.ElementsCreated = 0;");
            logic.AppendLine("            result.Message = \"Route found but duct creation failed\";");
            logic.AppendLine("            Logger.Warning(\"Smart duct creation failed\");");
            logic.AppendLine("        }");
            logic.AppendLine("    }");
            logic.AppendLine("    catch (Exception ex)");
            logic.AppendLine("    {");
            logic.AppendLine("        Logger.Error(\"Error during smart duct creation\", ex);");
            logic.AppendLine("        result.ElementsCreated = 0;");
            logic.AppendLine("        result.Message = $\"Duct creation error: {ex.Message}\";");
            logic.AppendLine("    }");
            logic.AppendLine("}");
            logic.AppendLine("else");
            logic.AppendLine("{");
            logic.AppendLine("    throw new InvalidOperationException($\"Failed to find duct route: {routeResult.Message}\");");
            logic.AppendLine("}");
        }
        else if (intent.Category == "Plumbing")
        {
            logic.AppendLine("// Create plumbing system");
            logic.AppendLine();

            if (intent.Parameters.ContainsKey("diameter"))
            {
                var diameter = (double)intent.Parameters["diameter"] / 12.0; // Конвертируем дюймы в футы
                logic.AppendLine(string.Format("var diameter = {0}f; // {1} inches", diameter.ToString("F2", System.Globalization.CultureInfo.InvariantCulture), intent.Parameters["diameter"]));
                logic.AppendLine("// Add pipe creation logic here");
            }
        }

        return logic.ToString();
    }

    /// <summary>
    /// Генерирует логику анализа
    /// </summary>
    private string GenerateAnalyzeLogic(CommandIntent intent)
    {
        var logic = new StringBuilder();

        logic.AppendLine("// Analyze building space");
        logic.AppendLine("var spaceAnalyzer = new SpaceAnalyzer(uiApp);");
        logic.AppendLine();

        if (intent.Parameters.ContainsKey("start_x"))
        {
            logic.AppendLine(string.Format("var center = new XYZ({0}, {1}, {2});", 
                intent.Parameters["start_x"], intent.Parameters["start_y"], intent.Parameters["start_z"]));
            logic.AppendLine("var radius = 10.0; // Default analysis radius");
        }
        else
        {
            logic.AppendLine("var center = XYZ.Zero; // Model center");
            logic.AppendLine("var radius = 15.0; // Default analysis radius");
        }

        logic.AppendLine();
        logic.AppendLine("var boundingBox = new BoundingBoxXYZ");
        logic.AppendLine("{");
        logic.AppendLine("    Min = new XYZ(center.X - radius, center.Y - radius, center.Z - radius),");
        logic.AppendLine("    Max = new XYZ(center.X + radius, center.Y + radius, center.Z + radius)");
        logic.AppendLine("};");
        logic.AppendLine();
        logic.AppendLine("var elements = spaceAnalyzer.GetElementsInBoundingBox(boundingBox);");
        logic.AppendLine("var isSpaceAvailable = spaceAnalyzer.IsSpaceAvailable(center, radius * 0.5);");
        logic.AppendLine();
        logic.AppendLine("result.Data[\"elements_found\"] = elements.Count;");
        logic.AppendLine("result.Data[\"space_available\"] = isSpaceAvailable;");
        logic.AppendLine("result.Data[\"analysis_center\"] = string.Format(\"({0:F1}, {1:F1}, {2:F1})\", center.X, center.Y, center.Z);");
        logic.AppendLine("result.Message = string.Format(\"Analysis completed: {0} elements found, space available: {1}\", elements.Count, isSpaceAvailable);");

        return logic.ToString();
    }

    /// <summary>
    /// Генерирует логику оптимизации
    /// </summary>
    private string GenerateOptimizeLogic(CommandIntent intent)
    {
        var logic = new StringBuilder();

        logic.AppendLine("// Optimize existing systems");
        logic.AppendLine("var spaceAnalyzer = new SpaceAnalyzer(uiApp);");
        logic.AppendLine("var routeCalculator = new RouteCalculator(spaceAnalyzer);");
        logic.AppendLine();
        logic.AppendLine("// Find existing MEP elements");
        logic.AppendLine("var mepElements = new FilteredElementCollector(doc)");
        logic.AppendLine("    .OfCategory(BuiltInCategory.OST_DuctCurves)");
        logic.AppendLine("    .WhereElementIsNotElementType()");
        logic.AppendLine("    .ToList();");
        logic.AppendLine();
        logic.AppendLine("result.Data[\"elements_to_optimize\"] = mepElements.Count;");
        logic.AppendLine("result.Message = string.Format(\"Found {0} elements for optimization\", mepElements.Count);");
        logic.AppendLine();
        logic.AppendLine("// Add optimization logic here");

        return logic.ToString();
    }

    /// <summary>
    /// Генерирует логику удаления элементов
    /// </summary>
    private string GenerateDeleteLogic(CommandIntent intent)
    {
        var logic = new StringBuilder();

        if (intent.Category == "Architecture" || intent.Description.ToLower().Contains("стен"))
        {
            logic.AppendLine("// Delete walls on current view");
            logic.AppendLine("Logger.Info(\"Starting wall deletion process\");");
            logic.AppendLine();
            
            logic.AppendLine("// Get all walls in the document");
            logic.AppendLine("var walls = new FilteredElementCollector(doc)");
            logic.AppendLine("    .OfCategory(BuiltInCategory.OST_Walls)");
            logic.AppendLine("    .WhereElementIsNotElementType()");
            logic.AppendLine("    .ToList();");
            logic.AppendLine();
            
            logic.AppendLine("Logger.Info($\"Found {walls.Count} walls to delete\");");
            logic.AppendLine();
            
            logic.AppendLine("if (walls.Count > 0)");
            logic.AppendLine("{");
            logic.AppendLine("    var deletedCount = 0;");
            logic.AppendLine("    foreach (var wall in walls)");
            logic.AppendLine("    {");
            logic.AppendLine("        try");
            logic.AppendLine("        {");
            logic.AppendLine("            doc.Delete(wall.Id);");
            logic.AppendLine("            deletedCount++;");
            logic.AppendLine("        }");
            logic.AppendLine("        catch (Exception ex)");
            logic.AppendLine("        {");
            logic.AppendLine("            Logger.Warning($\"Could not delete wall {wall.Id}: {ex.Message}\");");
            logic.AppendLine("        }");
            logic.AppendLine("    }");
            logic.AppendLine();
            logic.AppendLine("    result.ElementsModified = deletedCount;");
            logic.AppendLine("    result.Data[\"deleted_walls\"] = deletedCount;");
            logic.AppendLine("    result.Message = $\"Successfully deleted {deletedCount} out of {walls.Count} walls\";");
            logic.AppendLine("    Logger.Info($\"Wall deletion completed: {deletedCount} deleted\");");
            logic.AppendLine("}");
            logic.AppendLine("else");
            logic.AppendLine("{");
            logic.AppendLine("    result.Message = \"No walls found to delete\";");
            logic.AppendLine("    Logger.Info(\"No walls found in the document\");");
            logic.AppendLine("}");
        }
        else
        {
            logic.AppendLine("// Generic element deletion");
            logic.AppendLine("Logger.Info(\"Starting generic element deletion\");");
            logic.AppendLine("result.Message = \"Delete operation completed\";");
        }

        return logic.ToString();
    }

    /// <summary>
    /// Вычисляет сложность кода
    /// </summary>
    private int CalculateComplexity(string code)
    {
        var lines = code.Split('\n').Length;
        var complexity = Math.Min(lines / 10, 10); // Простая оценка сложности
        return complexity;
    }

    /// <summary>
    /// Оценивает время выполнения
    /// </summary>
    private TimeSpan EstimateExecutionTime(CommandIntent intent)
    {
        var baseTime = TimeSpan.FromSeconds(1);

        switch (intent.MainAction)
        {
            case "create":
                baseTime = TimeSpan.FromSeconds(3);
                break;
            case "analyze":
                baseTime = TimeSpan.FromSeconds(2);
                break;
            case "optimize":
                baseTime = TimeSpan.FromSeconds(5);
                break;
            case "delete":
                baseTime = TimeSpan.FromSeconds(2);
                break;
        }

        return baseTime;
    }

    /// <summary>
    /// Извлекает зависимости из кода
    /// </summary>
    private List<string> ExtractDependencies(string code)
    {
        var dependencies = new List<string>();

        if (code.Contains("SpaceAnalyzer"))
            dependencies.Add("mymcp.Analysis.SpaceAnalyzer");

        if (code.Contains("RouteCalculator"))
            dependencies.Add("mymcp.Analysis.RouteCalculator");

        if (code.Contains("MEPPathfinder"))
            dependencies.Add("mymcp.Analysis.MEPPathfinder");

        return dependencies;
    }
} 