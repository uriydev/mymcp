using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Autodesk.Revit.UI;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using mymcp.Core;
using System.Threading.Tasks;

namespace mymcp.Dynamic;

/// <summary>
/// Компилятор динамических команд с использованием Roslyn
/// </summary>
public class DynamicCommandCompiler
{
    private readonly List<MetadataReference> _references;
    private readonly SecuritySandbox _securitySandbox;

    public DynamicCommandCompiler()
    {
        _securitySandbox = new SecuritySandbox();
        _references = LoadRequiredReferences();
    }

    /// <summary>
    /// Компилирует код команды в исполняемую сборку
    /// </summary>
    // public async Task<CompilationResult> CompileCommand(GeneratedCommand generatedCommand)
    public Task<CompilationResult> CompileCommand(GeneratedCommand generatedCommand)
    {
        try
        {
            Logger.Info($"[Compiler] Starting compilation of command: {generatedCommand.Template.Name}");
            Logger.Debug($"[Compiler] Security level: {generatedCommand.Template.SecurityLevel}");

            // 1. Проверяем безопасность кода
            Logger.Debug("[Compiler] Step 1: Validating code security");
            var securityResult = _securitySandbox.ValidateCode(generatedCommand.Code);
            if (!securityResult.IsSecure)
            {
                Logger.Error($"[Compiler] Security validation failed: {string.Join(", ", securityResult.SecurityViolations)}");
                return Task.FromResult(new CompilationResult
                {
                    Success = false,
                    Errors = securityResult.SecurityViolations,
                    SecurityLevel = generatedCommand.Template.SecurityLevel
                });
            }
            Logger.Info("[Compiler] Security validation passed");

            // 2. Создаем полный код класса
            Logger.Debug("[Compiler] Step 2: Generating full class code");
            var fullCode = GenerateFullClassCode(generatedCommand);
            Logger.Info($"[Compiler] Full class code generated - {fullCode.Split('\n').Length} lines");
            
            // ОТЛАДКА: Выводим полный код для анализа проблемы
            Logger.Debug($"[Compiler] FULL GENERATED CODE:\n{fullCode}");
            Logger.Debug("[Compiler] END OF FULL GENERATED CODE");

            // 3. Создаем синтаксическое дерево
            Logger.Debug("[Compiler] Step 3: Creating syntax tree");
            var syntaxTree = CSharpSyntaxTree.ParseText(fullCode);
            Logger.Debug("[Compiler] Syntax tree created successfully");

            // 4. Создаем компиляцию
            Logger.Debug("[Compiler] Step 4: Setting up compilation");
            var compilation = CSharpCompilation.Create(
                $"DynamicCommand_{Guid.NewGuid():N}",
                new[] { syntaxTree },
                _references,
                new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Release,
                    allowUnsafe: false
                )
            );
            Logger.Debug($"[Compiler] Compilation setup complete with {_references.Count} references");

            // 5. Компилируем в память
            Logger.Debug("[Compiler] Step 5: Compiling to memory");
            using var ms = new MemoryStream();
            var emitResult = compilation.Emit(ms);

            if (!emitResult.Success)
            {
                var errors = emitResult.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.GetMessage())
                    .ToList();

                Logger.Error($"[Compiler] Compilation failed with {errors.Count} errors");
                foreach (var error in errors)
                {
                    Logger.Error($"[Compiler] Error: {error}");
                }

                return Task.FromResult(new CompilationResult
                {
                    Success = false,
                    Errors = errors,
                    Warnings = emitResult.Diagnostics
                        .Where(d => d.Severity == DiagnosticSeverity.Warning)
                        .Select(d => d.GetMessage())
                        .ToList()
                });
            }

            Logger.Info("[Compiler] Compilation successful");

            // 6. Загружаем сборку и создаем экземпляр
            Logger.Debug("[Compiler] Step 6: Loading assembly and creating instance");
            ms.Seek(0, SeekOrigin.Begin);
            var assembly = Assembly.Load(ms.ToArray());
            var commandType = assembly.GetTypes().FirstOrDefault(t => typeof(IDynamicCommand).IsAssignableFrom(t));

            if (commandType == null)
            {
                Logger.Error("[Compiler] Generated class does not implement IDynamicCommand interface");
                return Task.FromResult(new CompilationResult
                {
                    Success = false,
                    Errors = new List<string> { "Generated class does not implement IDynamicCommand interface" }
                });
            }

            var commandInstance = (IDynamicCommand)Activator.CreateInstance(commandType);
            Logger.Info($"[Compiler] Successfully compiled dynamic command: {commandInstance.Name}");

            return Task.FromResult(new CompilationResult
            {
                Success = true,
                CompiledCommand = commandInstance,
                Assembly = assembly,
                GeneratedCode = fullCode,
                CompilationTime = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            Logger.Error($"[Compiler] Critical error compiling command '{generatedCommand.Template?.Name ?? "Unknown"}'", ex);
            return Task.FromResult(new CompilationResult
            {
                Success = false,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Генерирует полный код класса на основе шаблона
    /// </summary>
    private string GenerateFullClassCode(GeneratedCommand generatedCommand)
    {
        var sb = new StringBuilder();

        // Добавляем using директивы
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using Autodesk.Revit.DB;");
        sb.AppendLine("using Autodesk.Revit.UI;");
        sb.AppendLine("using mymcp.Dynamic;");
        sb.AppendLine("using mymcp.Analysis;");
        sb.AppendLine("using mymcp.Core;");

        foreach (var ns in generatedCommand.Template.RequiredNamespaces)
        {
            sb.AppendLine($"using {ns};");
        }

        sb.AppendLine();

        // Генерируем класс
        sb.AppendLine($"public class DynamicCommand_{Guid.NewGuid():N} : IDynamicCommand");
        sb.AppendLine("{");
        
        // Свойства интерфейса
        sb.AppendLine($"    public string Name => \"{generatedCommand.Template.Name}\";");
        sb.AppendLine($"    public string Description => \"{generatedCommand.Template.Description.Replace("\"", "\\\"")}\";");
        sb.AppendLine($"    public SecurityLevel SecurityLevel => SecurityLevel.{generatedCommand.Template.SecurityLevel};");
        
        sb.AppendLine();

        // Метод валидации параметров
        sb.AppendLine("    public ValidationResult ValidateParameters(Dictionary<string, object> parameters)");
        sb.AppendLine("    {");
        sb.AppendLine("        var result = new ValidationResult { IsValid = true };");
        sb.AppendLine("        // TODO: Add parameter validation logic");
        sb.AppendLine("        return result;");
        sb.AppendLine("    }");

        sb.AppendLine();

        // Основной метод выполнения
        sb.AppendLine("    public DynamicCommandResult Execute(UIApplication uiApp, Dictionary<string, object> parameters)");
        sb.AppendLine("    {");
        sb.AppendLine("        var startTime = DateTime.Now;");
        sb.AppendLine("        var result = new DynamicCommandResult");
        sb.AppendLine("        {");
        sb.AppendLine("            Data = new Dictionary<string, object>(),");
        sb.AppendLine("            AffectedElements = new List<ElementId>(),");
        sb.AppendLine("            Warnings = new List<string>()");
        sb.AppendLine("        };");
        sb.AppendLine("        ");
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine("            var doc = uiApp.ActiveUIDocument.Document;");
        sb.AppendLine("            var uiDoc = uiApp.ActiveUIDocument;");
        sb.AppendLine("            ");
        sb.AppendLine("            // Счетчики для отслеживания созданных элементов");
        sb.AppendLine("            var elementsBeforeCount = new FilteredElementCollector(doc).WhereElementIsNotElementType().ToList().Count;");
        sb.AppendLine();
        
        // Вставляем сгенерированный код
        sb.AppendLine("            // Generated command logic:");
        sb.AppendLine(IndentCode(generatedCommand.Code, 12));
        
        sb.AppendLine();
        sb.AppendLine("            // Подсчитываем созданные элементы");
        sb.AppendLine("            var elementsAfterCount = new FilteredElementCollector(doc).WhereElementIsNotElementType().ToList().Count;");
        sb.AppendLine("            result.ElementsCreated = Math.Max(0, elementsAfterCount - elementsBeforeCount);");
        sb.AppendLine();
        sb.AppendLine("            result.Success = true;");
        sb.AppendLine("            if (string.IsNullOrEmpty(result.Message))");
        sb.AppendLine("            {");
        sb.AppendLine("                result.Message = \"Command executed successfully\";");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("        catch (Exception ex)");
        sb.AppendLine("        {");
        sb.AppendLine("            result.Success = false;");
        sb.AppendLine("            result.Message = ex.Message;");
        sb.AppendLine("            result.Error = ex;");
        sb.AppendLine("            Logger.Error(\"Error executing dynamic command\", ex);");
        sb.AppendLine("        }");
        sb.AppendLine("        finally");
        sb.AppendLine("        {");
        sb.AppendLine("            result.ExecutionTime = DateTime.Now - startTime;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        return result;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Добавляет отступы к коду
    /// </summary>
    private string IndentCode(string code, int spaces)
    {
        var indent = new string(' ', spaces);
        return string.Join("\n", code.Split('\n').Select(line => indent + line));
    }

    /// <summary>
    /// Загружает необходимые ссылки для компиляции
    /// </summary>
    private List<MetadataReference> LoadRequiredReferences()
    {
        var references = new List<MetadataReference>();

        try
        {
            // Базовые .NET сборки
            references.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(Console).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location));

            // Revit API сборки
            var revitApiPath = Path.GetDirectoryName(typeof(Document).Assembly.Location);
            references.Add(MetadataReference.CreateFromFile(typeof(Document).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(UIApplication).Assembly.Location));

            // Наши сборки
            references.Add(MetadataReference.CreateFromFile(typeof(IDynamicCommand).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(Logger).Assembly.Location));

            // Дополнительные .NET Core/Framework ссылки
            var netcoreApp = Path.GetDirectoryName(typeof(object).Assembly.Location);
            var systemRuntime = Path.Combine(netcoreApp, "System.Runtime.dll");
            if (File.Exists(systemRuntime))
            {
                references.Add(MetadataReference.CreateFromFile(systemRuntime));
            }

            Logger.Info($"Loaded {references.Count} references for dynamic compilation");
        }
        catch (Exception ex)
        {
            Logger.Error("Error loading compilation references", ex);
        }

        return references;
    }
}

/// <summary>
/// Результат компиляции динамической команды
/// </summary>
public class CompilationResult
{
    public bool Success { get; set; }
    public IDynamicCommand CompiledCommand { get; set; }
    public Assembly Assembly { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public string GeneratedCode { get; set; }
    public DateTime CompilationTime { get; set; }
    public SecurityLevel SecurityLevel { get; set; }
} 