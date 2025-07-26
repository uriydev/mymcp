using System;
using System.IO;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using GroqSharp;
using GroqSharp.Models;

namespace GroqStarter
{
    class Program
    {
        private static GroqSharp.IGroqClient? _groqClient;

        static async Task Main(string[] args)
        {
            // Не выводим приветствие - мешает автоматизации
            
            // Загрузка API ключа из переменных окружения или файла .env
            var apiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                if (File.Exists("groq.env"))
                {
                    var envContent = await File.ReadAllTextAsync("groq.env");
                    foreach (var line in envContent.Split('\n'))
                    {
                        var parts = line.Trim().Split('=', 2);
                        if (parts.Length == 2 && parts[0] == "GROQ_API_KEY")
                        {
                            apiKey = parts[1].Trim();
                            break;
                        }
                    }
                }
            }
            
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("Error: GROQ_API_KEY not found. Please set it in environment variables or groq.env file.");
                return;
            }

            // Инициализация GroqClient
            try
            {
                Console.Error.WriteLine($"DEBUG: Initializing Groq client with API key: {apiKey.Substring(0, 10)}...");
                _groqClient = new GroqClient(apiKey, "gemma2-9b-it")
                    .SetTemperature(0.5)
                    .SetMaxTokens(512)
                    .SetTopP(1)
                    .SetStop("NONE")
                    .SetStructuredRetryPolicy(5);

                Console.Error.WriteLine("DEBUG: Groq client initialized successfully");
                // Убираем вывод сообщения - мешает автоматизации
                
                Console.Error.WriteLine("DEBUG: Starting command processing loop");
                while (true)
                {
                    Console.Error.WriteLine("DEBUG: Waiting for input...");
                    var input = await Console.In.ReadLineAsync();
                    Console.Error.WriteLine($"DEBUG: Received input: {input}");
                    if (input == null || input.ToLower() == "exit")
                        break;
                    
                    try
                    {
                        Console.Error.WriteLine("DEBUG: Generating code...");
                        var result = await GenerateRevitCode(input);
                        Console.Error.WriteLine($"DEBUG: Generated result: {result}");
                        Console.WriteLine(result);
                        Console.WriteLine("===END_OF_RESPONSE==="); // Маркер конца ответа
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"DEBUG: Exception: {ex}");
                        Console.WriteLine($"Error: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize Groq client: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Генерирует C# код для Revit на основе команды на естественном языке
        /// </summary>
        public static async Task<string> GenerateRevitCode(string command)
        {
            try
            {
                Console.Error.WriteLine($"DEBUG: Starting code generation for command: {command}");
                
                var systemPrompt = @"You are a specialized Revit API code generator. Generate ONLY C# code for Revit API based on the user's natural language command.
Important rules:
1. Generate ONLY the C# code inside a transaction, without using statements, class or method declarations
2. Use only Revit API calls that would work within a transaction  
3. Assume variables 'Document' and 'UiApplication' are already available
4. Keep your code concise but complete for the requested operation
5. DO NOT include Transaction start/commit - that's already handled
6. DO NOT add comments or explanations, ONLY code
7. Use standard Revit API methods

Example command: 'create wall'
Example output:
Level level = null;
foreach (Level l in new FilteredElementCollector(Document).OfClass(typeof(Level))) { level = l; break; }
WallType wallType = null;
foreach (WallType wt in new FilteredElementCollector(Document).OfClass(typeof(WallType))) { wallType = wt; break; }
var startPoint = new XYZ(0, 0, 0);
var endPoint = new XYZ(10, 0, 0);
var curve = Line.CreateBound(startPoint, endPoint);
Wall.Create(Document, curve, wallType.Id, level.Id, 10, 0, false, false);";

                Console.Error.WriteLine("DEBUG: Sending request to Groq API...");
                var response = await _groqClient.CreateChatCompletionAsync(
                    new Message { Role = MessageRoleType.System, Content = systemPrompt },
                    new Message { Role = MessageRoleType.User, Content = $"Command: {command}" });

                Console.Error.WriteLine($"DEBUG: Raw response from Groq: '{response}'");
                Console.Error.WriteLine($"DEBUG: Response length: {response?.Length ?? 0}");

                // Очищаем от markdown блоков
                var cleanedResponse = response?.Trim() ?? "Error: No response from Groq";
                
                // Удаляем markdown-блоки ```csharp и ```
                cleanedResponse = System.Text.RegularExpressions.Regex.Replace(cleanedResponse, @"^```(csharp|C#)\s*", "", System.Text.RegularExpressions.RegexOptions.Multiline);
                cleanedResponse = System.Text.RegularExpressions.Regex.Replace(cleanedResponse, @"```\s*$", "", System.Text.RegularExpressions.RegexOptions.Multiline);
                
                // Удаляем лишние пробелы и переводы строк
                cleanedResponse = cleanedResponse.Trim();
                
                Console.Error.WriteLine($"DEBUG: Cleaned response: '{cleanedResponse}'");
                return cleanedResponse;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"DEBUG: Exception in GenerateRevitCode: {ex}");
                throw new Exception($"Error generating Revit code: {ex.Message}");
            }
        }
    }
}