using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using mymcp.Core;
using System.IO;

namespace mymcp.Dynamic;

/// <summary>
/// AI генератор кода для динамических команд
/// </summary>
public class AICodeGenerator
{
    private readonly CommandTemplateLibrary _templateLibrary;
    private readonly ApiDocumentationProvider _apiDocumentation;

    public AICodeGenerator()
    {
        _templateLibrary = new CommandTemplateLibrary();
        
        // Инициализируем провайдер документации API
        // Путь к файлу документации в папке проекта
        var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
        var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
        var documentationPath = Path.Combine(assemblyDirectory, "Files", "2022.chm");
        _apiDocumentation = new ApiDocumentationProvider(documentationPath);
        
        // Загружаем документацию асинхронно
        Task.Run(async () => await _apiDocumentation.LoadDocumentation());
    }

    /// <summary>
    /// Генерирует команду на основе описания на естественном языке
    /// </summary>
    public Task<GeneratedCommand> GenerateCommand(string naturalLanguageRequest)
    {
        return GenerateCommand(naturalLanguageRequest, null);
    }

    /// <summary>
    /// Генерирует команду на основе описания на естественном языке с внешними параметрами
    /// </summary>
    public Task<GeneratedCommand> GenerateCommand(string naturalLanguageRequest, Dictionary<string, object> externalParameters)
    {
        try
        {
            Logger.Info($"[CodeGen] Starting command generation for: {naturalLanguageRequest}");

            // 1. Анализируем намерение пользователя
            Logger.Debug("[CodeGen] Step 1: Analyzing user intent");
            var intent = AnalyzeIntent(naturalLanguageRequest);
            
            // Добавляем внешние параметры, если они переданы
            if (externalParameters != null)
            {
                foreach (var param in externalParameters)
                {
                    intent.Parameters[param.Key] = param.Value;
                }
                Logger.Debug($"[CodeGen] Added {externalParameters.Count} external parameters");
            }
            
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
        if (lowerRequest.Contains("создай") || lowerRequest.Contains("создать") || lowerRequest.Contains("create") || lowerRequest.Contains("построить") || lowerRequest.Contains("добавить"))
        {
            intent.MainAction = "create";
            intent.RequiredSecurityLevel = SecurityLevel.Moderate;
            Logger.Debug("[Intent] Main action detected: CREATE");
        }
        else if (lowerRequest.Contains("анализ") || lowerRequest.Contains("проанализ") || lowerRequest.Contains("analyze") || 
                 lowerRequest.Contains("найди") || lowerRequest.Contains("найти") || lowerRequest.Contains("покажи") || 
                 lowerRequest.Contains("показать") || lowerRequest.Contains("find") || lowerRequest.Contains("show"))
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
        var code = template.Code;

        // Добавляем API рекомендации если доступны
        Logger.Debug($"[CodeGen] API Documentation available: {_apiDocumentation.IsAvailable}");
        if (_apiDocumentation.IsAvailable)
        {
            var apiRecommendations = _apiDocumentation.GetRecommendationsForCategory(intent.Category);
            Logger.Debug($"[CodeGen] API recommendations for {intent.Category}: {!string.IsNullOrEmpty(apiRecommendations)}");
            if (!string.IsNullOrEmpty(apiRecommendations))
            {
                // Вставляем рекомендации в начало логики
                var logicPlaceholder = "{{GENERATED_LOGIC}}";
                var enhancedLogic = apiRecommendations + Environment.NewLine + GenerateSpecificLogic(template, intent);
                code = code.Replace(logicPlaceholder, enhancedLogic);
                Logger.Info($"[CodeGen] Enhanced code with API recommendations for {intent.Category}");
            }
            else
            {
                code = code.Replace("{{GENERATED_LOGIC}}", GenerateSpecificLogic(template, intent));
            }
        }
        else
        {
            code = code.Replace("{{GENERATED_LOGIC}}", GenerateSpecificLogic(template, intent));
        }

        // Заменяем остальные плейсхолдеры
        code = code.Replace("{{ACTION}}", intent.MainAction ?? "Execute");
        code = code.Replace("{{CATEGORY}}", intent.Category ?? "General");
        code = code.Replace("{{DESCRIPTION}}", intent.Description ?? "Dynamic command");

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
    private string GenerateSpecificLogic(CommandTemplate template, CommandIntent intent)
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

        return logicBuilder.ToString();
    }

    /// <summary>
    /// Генерирует логику создания элементов
    /// </summary>
    private string GenerateCreateLogic(CommandIntent intent)
    {
        var logic = new StringBuilder();

        if (intent.Category == "HVAC" || intent.Description.ToLower().Contains("воздуховод"))
        {
            logic.AppendLine("// Create duct using MEP pathfinder and actual Revit elements");
            logic.AppendLine("Logger.Info(\"Creating real duct elements in Revit\");");
            logic.AppendLine();

            // Проверяем, нужно ли анализировать стены
            bool followWalls = intent.Description.ToLower().Contains("стен") || 
                             intent.Description.ToLower().Contains("вдоль") ||
                             intent.Description.ToLower().Contains("wall");

            if (followWalls)
            {
                logic.AppendLine("// Analyze walls and create ducts along them");
                logic.AppendLine("Logger.Info(\"Analyzing walls in current view\");");
                logic.AppendLine();
                logic.AppendLine("// Get all walls from current view or document");
                logic.AppendLine("var walls = new FilteredElementCollector(doc)");
                logic.AppendLine("    .OfClass(typeof(Wall))");
                logic.AppendLine("    .Cast<Wall>()");
                logic.AppendLine("    .Where(w => w.Location is LocationCurve)");
                logic.AppendLine("    .ToList();");
                logic.AppendLine();
                logic.AppendLine("Logger.Info($\"Found {walls.Count} walls to process\");");
                logic.AppendLine();
                logic.AppendLine("// Set duct height offset (3 meters = ~10 feet)");
                logic.AppendLine("double heightOffset = 10.0; // feet");
                
                // Передаем параметр высоты если он есть
                if (intent.Parameters.ContainsKey("height"))
                {
                    var heightValue = intent.Parameters["height"];
                    logic.AppendLine($"double heightMm = {heightValue};");
                    logic.AppendLine("heightOffset = heightMm * 0.00328084; // Convert mm to feet");
                }
                else
                {
                    logic.AppendLine("// Using default height offset");
                }
                logic.AppendLine();
                logic.AppendLine("var createdDucts = 0;");
                logic.AppendLine("var spaceAnalyzer = new SpaceAnalyzer(uiApp);");
                logic.AppendLine("var routeCalculator = new RouteCalculator(spaceAnalyzer);");
                logic.AppendLine("var mepPathfinder = new MEPPathfinder(spaceAnalyzer, routeCalculator, doc);");
                logic.AppendLine();
                logic.AppendLine("foreach (var wall in walls)");
                logic.AppendLine("{");
                logic.AppendLine("    try");
                logic.AppendLine("    {");
                logic.AppendLine("        var locationCurve = wall.Location as LocationCurve;");
                logic.AppendLine("        var curve = locationCurve.Curve;");
                logic.AppendLine("        ");
                logic.AppendLine("        // Get wall endpoints and offset vertically");
                logic.AppendLine("        var startPoint = curve.GetEndPoint(0);");
                logic.AppendLine("        var endPoint = curve.GetEndPoint(1);");
                logic.AppendLine("        ");
                logic.AppendLine("        // Offset points vertically for duct height");
                logic.AppendLine("        startPoint = new XYZ(startPoint.X, startPoint.Y, startPoint.Z + heightOffset);");
                logic.AppendLine("        endPoint = new XYZ(endPoint.X, endPoint.Y, endPoint.Z + heightOffset);");
                logic.AppendLine("        ");
                logic.AppendLine("        Logger.Info($\"Creating duct along wall from ({startPoint.X:F2}, {startPoint.Y:F2}, {startPoint.Z:F2}) to ({endPoint.X:F2}, {endPoint.Y:F2}, {endPoint.Z:F2})\");");
                logic.AppendLine("        ");
                logic.AppendLine("        // Find route and create duct");
                logic.AppendLine("        var routeResult = mepPathfinder.FindDuctRoute(startPoint, endPoint, 1.0, 0.6);");
                logic.AppendLine("        ");
                logic.AppendLine("        if (routeResult.Success)");
                logic.AppendLine("        {");
                logic.AppendLine("            var smartResult = mepPathfinder.CreateSmartDuct(startPoint, endPoint, routeResult);");
                logic.AppendLine("            if (smartResult.Success)");
                logic.AppendLine("            {");
                logic.AppendLine("                createdDucts += smartResult.ElementsCreated;");
                logic.AppendLine("                Logger.Info($\"Successfully created duct along wall: {smartResult.ElementsCreated} elements\");");
                logic.AppendLine("            }");
                logic.AppendLine("        }");
                logic.AppendLine("    }");
                logic.AppendLine("    catch (Exception ex)");
                logic.AppendLine("    {");
                logic.AppendLine("        Logger.Warning($\"Failed to create duct along wall: {ex.Message}\");");
                logic.AppendLine("    }");
                logic.AppendLine("}");
                logic.AppendLine();
                logic.AppendLine("result.ElementsCreated = createdDucts;");
                logic.AppendLine("result.Message = $\"Created {createdDucts} duct elements along {walls.Count} walls\";");
                logic.AppendLine("Logger.Info($\"Completed wall analysis: {createdDucts} ducts created along {walls.Count} walls\");");
            }
            else
            {
                // Оригинальная логика для точечного создания воздуховодов

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
        }
        else if (intent.Category == "Architecture" || intent.Description.ToLower().Contains("стен"))
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
        logic.AppendLine("var analyzer = new SpaceAnalyzer(uiApp);");
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
        logic.AppendLine("var elements = analyzer.GetElementsInBoundingBox(boundingBox);");
        logic.AppendLine("var isSpaceAvailable = analyzer.IsSpaceAvailable(center, radius * 0.5);");
        logic.AppendLine();
        
        // Добавляем подсчет стен, комнат и других элементов
        logic.AppendLine("// Count specific element types");
        logic.AppendLine("var walls = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Walls).WhereElementIsNotElementType().ToList();");
        logic.AppendLine("var rooms = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rooms).WhereElementIsNotElementType().ToList();");
        logic.AppendLine("var doors = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Doors).WhereElementIsNotElementType().ToList();");
        logic.AppendLine("var windows = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Windows).WhereElementIsNotElementType().ToList();");
        logic.AppendLine("var ducts = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_DuctCurves).WhereElementIsNotElementType().ToList();");
        logic.AppendLine();
        
        // Добавляем извлечение координат стен и параметров если запрошено
        if (intent.Parameters.ContainsKey("extract_wall_coordinates") || 
            intent.Parameters.ContainsKey("detailed_geometry") ||
            intent.Description.ToLower().Contains("координат") ||
            intent.Description.ToLower().Contains("параметр") ||
            intent.Description.ToLower().Contains("высот"))
        {
            logic.AppendLine("// Extract wall coordinates and parameters");
            logic.AppendLine("var wallDetails = new List<string>();");
            logic.AppendLine("foreach (Wall wall in walls)");
            logic.AppendLine("{");
            logic.AppendLine("    var locationCurve = wall.Location as LocationCurve;");
            logic.AppendLine("    if (locationCurve != null)");
            logic.AppendLine("    {");
            logic.AppendLine("        var startPoint = locationCurve.Curve.GetEndPoint(0);");
            logic.AppendLine("        var endPoint = locationCurve.Curve.GetEndPoint(1);");
            logic.AppendLine("        ");
            logic.AppendLine("        // Get wall height parameter");
            logic.AppendLine("        var heightParam = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM);");
            logic.AppendLine("        var height = heightParam != null ? heightParam.AsDouble() : 0.0;");
            logic.AppendLine("        ");
            logic.AppendLine("        // Get wall type");
            logic.AppendLine("        var wallTypeName = wall.WallType != null ? wall.WallType.Name : \"Unknown\";");
            logic.AppendLine("        ");
            logic.AppendLine("        var detailString = string.Format(\"Wall {0} ({1}): Height={2:F1}ft, From=({3:F1},{4:F1},{5:F1}) To=({6:F1},{7:F1},{8:F1})\",");
            logic.AppendLine("            wall.Id.IntegerValue, wallTypeName, height, startPoint.X, startPoint.Y, startPoint.Z, endPoint.X, endPoint.Y, endPoint.Z);");
            logic.AppendLine("        wallDetails.Add(detailString);");
            logic.AppendLine("        Logger.Info(detailString);");
            logic.AppendLine("    }");
            logic.AppendLine("}");
            logic.AppendLine("result.Data[\"wall_details\"] = string.Join(\"; \", wallDetails);");
            logic.AppendLine();
        }
        
        logic.AppendLine("result.Data[\"total_elements\"] = elements.Count;");
        logic.AppendLine("result.Data[\"walls_count\"] = walls.Count;");
        logic.AppendLine("result.Data[\"rooms_count\"] = rooms.Count;");
        logic.AppendLine("result.Data[\"doors_count\"] = doors.Count;");
        logic.AppendLine("result.Data[\"windows_count\"] = windows.Count;");
        logic.AppendLine("result.Data[\"ducts_count\"] = ducts.Count;");
        logic.AppendLine("result.Data[\"space_available\"] = isSpaceAvailable;");
        logic.AppendLine("result.Data[\"analysis_center\"] = string.Format(\"({0:F1}, {1:F1}, {2:F1})\", center.X, center.Y, center.Z);");
        logic.AppendLine("result.Message = string.Format(\"Analysis: {0} walls, {1} rooms, {2} doors, {3} windows, {4} ducts. Total: {5} elements\", walls.Count, rooms.Count, doors.Count, windows.Count, ducts.Count, elements.Count);");

        return logic.ToString();
    }

    /// <summary>
    /// Генерирует логику оптимизации
    /// </summary>
    private string GenerateOptimizeLogic(CommandIntent intent)
    {
        var logic = new StringBuilder();

        logic.AppendLine("// Optimize existing systems");
        logic.AppendLine("var optimizer = new SpaceAnalyzer(uiApp);");
        logic.AppendLine("var routeCalculator = new RouteCalculator(optimizer);");
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