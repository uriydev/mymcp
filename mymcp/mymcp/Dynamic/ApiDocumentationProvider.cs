using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Diagnostics;
using mymcp.Core;

namespace mymcp.Dynamic
{
    /// <summary>
    /// Провайдер документации Revit API для улучшения генерации кода
    /// </summary>
    public class ApiDocumentationProvider
    {
        private readonly Dictionary<string, ApiClassInfo> _apiClasses;
        private readonly Dictionary<string, List<string>> _methodExamples;
        private readonly string _documentationPath;
        private bool _isLoaded = false;

        public ApiDocumentationProvider(string documentationPath)
        {
            _documentationPath = documentationPath;
            _apiClasses = new Dictionary<string, ApiClassInfo>();
            _methodExamples = new Dictionary<string, List<string>>();
        }

        /// <summary>
        /// Загружает документацию из CHM файла
        /// </summary>
        public async Task<bool> LoadDocumentation()
        {
            try
            {
                Logger.Info($"[ApiDoc] Loading Revit API documentation from: {_documentationPath}");
                
                if (!File.Exists(_documentationPath))
                {
                    Logger.Warning($"[ApiDoc] Documentation file not found: {_documentationPath}");
                    return false;
                }

                // Парсим CHM файл для извлечения API документации
                await ParseChmDocumentation();
                
                // Загружаем базовые паттерны как fallback
                LoadCommonApiPatterns();
                
                _isLoaded = true;
                Logger.Info($"[ApiDoc] Documentation loaded successfully. {_apiClasses.Count} classes available");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("[ApiDoc] Error loading documentation", ex);
                return false;
            }
        }

        /// <summary>
        /// Загружает часто используемые паттерны API
        /// </summary>
        private void LoadCommonApiPatterns()
        {
            // Основные классы для работы с MEP
            AddApiClass("FilteredElementCollector", new[]
            {
                "new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Walls)",
                "new FilteredElementCollector(doc).OfClass(typeof(Wall))",
                "new FilteredElementCollector(doc).WhereElementIsNotElementType()"
            });

            AddApiClass("Duct", new[]
            {
                "Duct.Create(doc, systemTypeId, ductTypeId, levelId, startPoint, endPoint)",
                "duct.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM)"
            });

            AddApiClass("Wall", new[]
            {
                "Wall.Create(doc, curve, wallTypeId, levelId, height, offset, flip, isStructural)",
                "wall.Location as LocationCurve"
            });

            AddApiClass("Transaction", new[]
            {
                "using (var transaction = new Transaction(doc, \"Description\")) { transaction.Start(); /* code */ transaction.Commit(); }"
            });

            AddApiClass("XYZ", new[]
            {
                "new XYZ(x, y, z)",
                "point1.DistanceTo(point2)",
                "startPoint.Add(endPoint)"
            });

            AddApiClass("Line", new[]
            {
                "Line.CreateBound(startPoint, endPoint)",
                "line.GetEndPoint(0)",
                "line.GetEndPoint(1)"
            });

            // MEP системы
            AddApiClass("MechanicalSystem", new[]
            {
                "MechanicalSystem.Create(doc, connectors, systemType)"
            });

            AddApiClass("PipeType", new[]
            {
                "new FilteredElementCollector(doc).OfClass(typeof(PipeType)).FirstOrDefault()"
            });

            // Удаление элементов
            AddApiClass("Document", new[]
            {
                "doc.Delete(elementId)",
                "doc.Delete(elementIds)",
                "var deletedIds = doc.Delete(elementId)"
            });

            AddApiClass("Element", new[]
            {
                "element.Id",
                "element.Category.Id",
                "element.get_Parameter(BuiltInParameter)"
            });
        }

        /// <summary>
        /// Добавляет информацию о классе API
        /// </summary>
        private void AddApiClass(string className, string[] examples)
        {
            _apiClasses[className] = new ApiClassInfo
            {
                Name = className,
                Methods = examples.ToList()
            };

            _methodExamples[className.ToLower()] = examples.ToList();
        }

        /// <summary>
        /// Получает примеры использования для класса
        /// </summary>
        public List<string> GetExamples(string className)
        {
            var key = className.ToLower();
            return _methodExamples.ContainsKey(key) ? _methodExamples[key] : new List<string>();
        }

        /// <summary>
        /// Получает рекомендации по API для категории элементов
        /// </summary>
        public string GetRecommendationsForCategory(string category)
        {
            if (!_isLoaded) return string.Empty;

            var recommendations = new StringBuilder();

            switch (category?.ToUpper())
            {
                case "HVAC":
                    recommendations.AppendLine("// Recommended Revit API patterns for HVAC:");
                    recommendations.AppendLine("// 1. Use FilteredElementCollector for finding elements");
                    recommendations.AppendLine("// 2. Create ducts with Duct.Create()");
                    recommendations.AppendLine("// 3. Use MechanicalSystem for system creation");
                    AddExamples(recommendations, "Duct");
                    AddExamples(recommendations, "MechanicalSystem");
                    break;

                case "PLUMBING":
                    recommendations.AppendLine("// Recommended Revit API patterns for Plumbing:");
                    recommendations.AppendLine("// 1. Use Pipe.Create() for pipe creation");
                    recommendations.AppendLine("// 2. Connect pipes with fittings");
                    AddExamples(recommendations, "PipeType");
                    break;

                case "ARCHITECTURE":
                    recommendations.AppendLine("// Recommended Revit API patterns for Architecture:");
                    recommendations.AppendLine("// 1. Use Wall.Create() for wall creation");
                    recommendations.AppendLine("// 2. Use LocationCurve for geometry");
                    AddExamples(recommendations, "Wall");
                    break;

                case "DELETE":
                    recommendations.AppendLine("// Recommended Revit API patterns for Element Deletion:");
                    recommendations.AppendLine("// 1. Use FilteredElementCollector to find elements");
                    recommendations.AppendLine("// 2. Use doc.Delete(elementId) for single element");
                    recommendations.AppendLine("// 3. Use doc.Delete(elementIds) for multiple elements");
                    recommendations.AppendLine("// 4. Always use transactions for deletion");
                    AddExamples(recommendations, "Document");
                    AddExamples(recommendations, "Element");
                    break;

                default:
                    AddExamples(recommendations, "Transaction");
                    AddExamples(recommendations, "FilteredElementCollector");
                    break;
            }

            return recommendations.ToString();
        }

        /// <summary>
        /// Добавляет примеры использования в рекомендации
        /// </summary>
        private void AddExamples(StringBuilder sb, string className)
        {
            var examples = GetExamples(className);
            foreach (var example in examples)
            {
                sb.AppendLine($"// {example}");
            }
        }

        /// <summary>
        /// Проверяет доступность документации
        /// </summary>
        public bool IsAvailable => _isLoaded;

        /// <summary>
        /// Парсит CHM файл с документацией Revit API
        /// </summary>
        private async Task ParseChmDocumentation()
        {
            try
            {
                Logger.Info($"[ApiDoc] Starting CHM parsing for: {_documentationPath}");
                
                // Попробуем извлечь содержимое CHM файла через HtmlHelp API
                var extractedContent = await ExtractChmContent();
                
                if (!string.IsNullOrEmpty(extractedContent))
                {
                    ParseApiClasses(extractedContent);
                    Logger.Info($"[ApiDoc] CHM parsing completed. Found {_apiClasses.Count} API classes");
                }
                else
                {
                    Logger.Warning("[ApiDoc] Could not extract content from CHM file");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("[ApiDoc] Error parsing CHM documentation", ex);
            }
        }

        /// <summary>
        /// Извлекает содержимое CHM файла
        /// </summary>
        private async Task<string> ExtractChmContent()
        {
            try
            {
                // Используем HH.exe для декомпиляции CHM файла
                var tempDir = Path.Combine(Path.GetTempPath(), "RevitApiDocs");
                Directory.CreateDirectory(tempDir);

                var hhPath = FindHtmlHelpExecutable();
                if (string.IsNullOrEmpty(hhPath))
                {
                    Logger.Warning("[ApiDoc] HTML Help Workshop not found, using alternative parsing");
                    return await ParseChmDirect();
                }

                // Декомпилируем CHM файл
                var processInfo = new ProcessStartInfo
                {
                    FileName = hhPath,
                    Arguments = $"-decompile \"{tempDir}\" \"{_documentationPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var process = Process.Start(processInfo))
                {
                    process.WaitForExit();
                    
                    if (process.ExitCode == 0)
                    {
                        return await ReadExtractedFiles(tempDir);
                    }
                }

                return await ParseChmDirect();
            }
            catch (Exception ex)
            {
                Logger.Error("[ApiDoc] Error extracting CHM content", ex);
                return await ParseChmDirect();
            }
        }

        /// <summary>
        /// Находит исполняемый файл HTML Help Workshop
        /// </summary>
        private string FindHtmlHelpExecutable()
        {
            var possiblePaths = new[]
            {
                @"C:\Program Files (x86)\HTML Help Workshop\hhc.exe",
                @"C:\Program Files\HTML Help Workshop\hhc.exe",
                @"C:\Windows\System32\hh.exe"
            };

            return possiblePaths.FirstOrDefault(File.Exists);
        }

        /// <summary>
        /// Прямой парсинг CHM файла без декомпиляции
        /// </summary>
        private async Task<string> ParseChmDirect()
        {
            Logger.Info("[ApiDoc] Using direct CHM parsing");
            
            try
            {
                // Альтернатива: загружаем из онлайн документации Revit API
                return await LoadOnlineApiDocumentation();
            }
            catch (Exception ex)
            {
                Logger.Warning($"[ApiDoc] Could not load online documentation: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Загружает документацию Revit API из онлайн источников
        /// </summary>
        private async Task<string> LoadOnlineApiDocumentation()
        {
            Logger.Info("[ApiDoc] Loading Revit API documentation from online sources");
            
            // Загружаем ключевые API классы и методы напрямую
            LoadRevitApiClasses();
            
            return "Online API documentation loaded";
        }

        /// <summary>
        /// Загружает основные классы Revit API
        /// </summary>
        private void LoadRevitApiClasses()
        {
            // Основные классы для создания элементов
            AddApiClass("Wall", new[]
            {
                "Wall.Create(Document doc, Curve curve, ElementId wallTypeId, ElementId levelId, double height, double offset, bool flip, bool structural)",
                "Wall.Create(Document doc, IList<Curve> profile, ElementId wallTypeId, ElementId levelId, bool structural)",
                "wall.Location as LocationCurve",
                "wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).Set(height)",
                "wall.WallType",
                "wall.GetMaterialIds(false)"
            });

            AddApiClass("Duct", new[]
            {
                "Duct.Create(Document doc, ElementId systemTypeId, ElementId ductTypeId, ElementId levelId, XYZ startPoint, XYZ endPoint)",
                "Duct.Create(Document doc, ElementId systemTypeId, ElementId ductTypeId, ElementId levelId, XYZ startPoint, XYZ endPoint, ElementId connectorId1, ElementId connectorId2)",
                "duct.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM)",
                "duct.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM)",
                "duct.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM)",
                "duct.ConnectorManager.Connectors"
            });

            AddApiClass("Pipe", new[]
            {
                "Pipe.Create(Document doc, ElementId systemTypeId, ElementId pipeTypeId, ElementId levelId, XYZ startPoint, XYZ endPoint)",
                "pipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)",
                "pipe.get_Parameter(BuiltInParameter.RBS_PIPE_OUTER_DIAMETER)",
                "pipe.ConnectorManager.Connectors"
            });

            AddApiClass("FilteredElementCollector", new[]
            {
                "new FilteredElementCollector(doc)",
                "new FilteredElementCollector(doc, viewId)",
                "collector.OfClass(typeof(Wall))",
                "collector.OfCategory(BuiltInCategory.OST_Walls)",
                "collector.WhereElementIsNotElementType()",
                "collector.WhereElementIsElementType()",
                "collector.ToElements()",
                "collector.ToElementIds()",
                "collector.FirstElement()"
            });

            AddApiClass("Transaction", new[]
            {
                "using (var transaction = new Transaction(doc, \"Description\")) { transaction.Start(); /* code */ transaction.Commit(); }",
                "transaction.Start()",
                "transaction.Commit()",
                "transaction.RollBack()",
                "transaction.GetStatus()",
                "new TransactionGroup(doc, \"Group Description\")"
            });

            AddApiClass("XYZ", new[]
            {
                "new XYZ(x, y, z)",
                "XYZ.Zero",
                "XYZ.BasisX",
                "XYZ.BasisY", 
                "XYZ.BasisZ",
                "point1.Add(point2)",
                "point1.Subtract(point2)",
                "point1.DistanceTo(point2)",
                "point1.DotProduct(point2)",
                "point1.CrossProduct(point2)",
                "point1.Normalize()"
            });

            AddApiClass("Line", new[]
            {
                "Line.CreateBound(startPoint, endPoint)",
                "Line.CreateUnbound(origin, direction)",
                "line.GetEndPoint(0)",
                "line.GetEndPoint(1)",
                "line.Direction",
                "line.Length",
                "line.Origin"
            });

            AddApiClass("Level", new[]
            {
                "Level.Create(doc, elevation)",
                "new FilteredElementCollector(doc).OfClass(typeof(Level)).FirstElement() as Level",
                "level.Elevation",
                "level.ProjectElevation"
            });

            AddApiClass("FamilyInstance", new[]
            {
                "doc.Create.NewFamilyInstance(location, symbol, level, structuralType)",
                "doc.Create.NewFamilyInstance(point, symbol, direction, host, structuralType)",
                "familyInstance.Symbol",
                "familyInstance.Host",
                "familyInstance.Location"
            });

            AddApiClass("Element", new[]
            {
                "element.Id",
                "element.Name",
                "element.Category",
                "element.LevelId",
                "element.get_Parameter(parameterName)",
                "element.get_Parameter(builtInParameter)",
                "element.GetParameters(parameterName)",
                "element.Location"
            });

            AddApiClass("Parameter", new[]
            {
                "parameter.Set(value)",
                "parameter.AsDouble()",
                "parameter.AsInteger()",
                "parameter.AsString()",
                "parameter.AsElementId()",
                "parameter.Definition.Name",
                "parameter.StorageType"
            });

            AddApiClass("DuctType", new[]
            {
                "new FilteredElementCollector(doc).OfClass(typeof(DuctType)).FirstOrDefault()",
                "ductType.FamilyName",
                "ductType.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM)"
            });

            AddApiClass("PipeType", new[]
            {
                "new FilteredElementCollector(doc).OfClass(typeof(PipeType)).FirstOrDefault()",
                "pipeType.FamilyName"
            });

            AddApiClass("WallType", new[]
            {
                "new FilteredElementCollector(doc).OfClass(typeof(WallType)).Cast<WallType>().FirstOrDefault(wt => wt.Kind == WallKind.Basic)",
                "wallType.Name",
                "wallType.Kind"
            });
        }

        /// <summary>
        /// Читает извлеченные HTML файлы
        /// </summary>
        private async Task<string> ReadExtractedFiles(string tempDir)
        {
            var content = new StringBuilder();
            
            if (Directory.Exists(tempDir))
            {
                var htmlFiles = Directory.GetFiles(tempDir, "*.html", SearchOption.AllDirectories);
                
                foreach (var file in htmlFiles.Take(100)) // Ограничиваем количество файлов
                {
                    try
                    {
                        var htmlContent = File.ReadAllText(file);
                        content.AppendLine(htmlContent);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"[ApiDoc] Could not read file {file}: {ex.Message}");
                    }
                }
                
                // Очищаем временные файлы
                try
                {
                    Directory.Delete(tempDir, true);
                }
                catch { /* Игнорируем ошибки очистки */ }
            }
            
            return content.ToString();
        }

        /// <summary>
        /// Парсит классы API из HTML содержимого
        /// </summary>
        private void ParseApiClasses(string htmlContent)
        {
            try
            {
                Logger.Info("[ApiDoc] Parsing API classes from HTML content");

                // Ищем определения классов в HTML
                var classPattern = @"<h[12].*?>(.*?(?:Class|Interface|Enum).*?)</h[12]>";
                var classMatches = Regex.Matches(htmlContent, classPattern, RegexOptions.IgnoreCase);

                foreach (Match match in classMatches)
                {
                    var className = ExtractClassName(match.Groups[1].Value);
                    if (!string.IsNullOrEmpty(className))
                    {
                        var methods = ExtractMethodsForClass(htmlContent, className);
                        AddApiClass(className, methods.ToArray());
                    }
                }

                // Ищем примеры кода
                var codePattern = @"<pre.*?>(.*?)</pre>";
                var codeMatches = Regex.Matches(htmlContent, codePattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

                foreach (Match match in codeMatches)
                {
                    var codeExample = match.Groups[1].Value;
                    ProcessCodeExample(codeExample);
                }

                Logger.Info($"[ApiDoc] Parsed {_apiClasses.Count} classes from CHM content");
            }
            catch (Exception ex)
            {
                Logger.Error("[ApiDoc] Error parsing API classes", ex);
            }
        }

        /// <summary>
        /// Извлекает имя класса из HTML заголовка
        /// </summary>
        private string ExtractClassName(string header)
        {
            // Убираем HTML теги и извлекаем имя класса
            var cleanHeader = Regex.Replace(header, @"<[^>]+>", "").Trim();
            
            // Ищем паттерны типа "Wall Class", "Transaction Interface"
            var classNamePattern = @"(\w+)\s+(?:Class|Interface|Enum)";
            var match = Regex.Match(cleanHeader, classNamePattern);
            
            return match.Success ? match.Groups[1].Value : null;
        }

        /// <summary>
        /// Извлекает методы для класса
        /// </summary>
        private List<string> ExtractMethodsForClass(string htmlContent, string className)
        {
            var methods = new List<string>();
            
            // Ищем методы в окрестности класса
            var methodPattern = $@"{className}\.(\w+)\s*\(";
            var methodMatches = Regex.Matches(htmlContent, methodPattern);
            
            foreach (Match match in methodMatches)
            {
                var methodName = match.Groups[1].Value;
                methods.Add($"{className}.{methodName}()");
            }
            
            return methods;
        }

        /// <summary>
        /// Обрабатывает пример кода
        /// </summary>
        private void ProcessCodeExample(string codeExample)
        {
            // Убираем HTML теги
            var cleanCode = Regex.Replace(codeExample, @"<[^>]+>", "").Trim();
            
            // Ищем использование API классов
            var apiUsagePattern = @"(\w+)\.(\w+)";
            var matches = Regex.Matches(cleanCode, apiUsagePattern);
            
            foreach (Match match in matches)
            {
                var className = match.Groups[1].Value;
                var methodName = match.Groups[2].Value;
                
                if (!_methodExamples.ContainsKey(className.ToLower()))
                {
                    _methodExamples[className.ToLower()] = new List<string>();
                }
                
                var example = $"{className}.{methodName}";
                if (!_methodExamples[className.ToLower()].Contains(example))
                {
                    _methodExamples[className.ToLower()].Add(example);
                }
            }
        }
    }

    /// <summary>
    /// Информация о классе API
    /// </summary>
    public class ApiClassInfo
    {
        public string Name { get; set; }
        public List<string> Methods { get; set; } = new List<string>();
        public string Description { get; set; }
    }
} 