using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using Nice3point.Revit.Toolkit.External.Handlers;

namespace mymcp.Dynamic
{
    /// <summary>
    /// Безопасный исполнитель динамического кода в контексте Revit
    /// </summary>
    public class DynamicCodeExecutor
    {
        private readonly UIApplication _uiApplication;
        private readonly Document _document;
        private readonly ActionEventHandler _actionEventHandler;

        public DynamicCodeExecutor(UIApplication uiApplication)
        {
            _uiApplication = uiApplication;
            _document = uiApplication.ActiveUIDocument.Document;
            _actionEventHandler = new ActionEventHandler();
        }

        /// <summary>
        /// Выполнить динамический код с базовым контекстом Revit
        /// </summary>
        public async Task<object> ExecuteScriptAsync(string script, int timeoutSeconds = 5)
        {
            var options = ScriptOptions.Default
                .AddReferences(
                    typeof(Document).Assembly,
                    typeof(Element).Assembly,
                    typeof(TaskDialogResult).Assembly
                )
                .WithImports(
                    "System",
                    "System.Linq",
                    "Autodesk.Revit.DB",
                    "Autodesk.Revit.UI"
                );

            var scriptGlobals = new RevitScriptContext
            {
                Document = _document,
                UiApplication = _uiApplication
            };

            object result = null;
            Exception scriptException = null;

            try
            {
                var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                
                // Выполняем код в правильном Revit контексте через ActionEventHandler
                _actionEventHandler.Raise(application =>
                {
                    try
                    {
                        using var transaction = new Transaction(_document, "Dynamic Revit Command");
                        transaction.Start();

                        // Компилируем и выполняем скрипт
                        result = CSharpScript.EvaluateAsync(script, options, scriptGlobals, cancellationToken: cts.Token).Result;
                        
                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        scriptException = ex;
                    }
                });

                // Ждем выполнения с таймаутом
                await Task.Delay(100); // Небольшая задержка для завершения

                if (scriptException != null)
                    throw scriptException;

                return result;
            }
            catch (OperationCanceledException)
            {
                throw new Exception($"Скрипт превысил максимальное время выполнения ({timeoutSeconds} сек)");
            }
            catch (CompilationErrorException ex)
            {
                throw new Exception($"Ошибка компиляции: {string.Join("\n", ex.Diagnostics)}");
            }
        }

        /// <summary>
        /// Контекст для выполнения скриптов с доступом к Revit
        /// </summary>
        public class RevitScriptContext
        {
            public Document Document { get; set; }
            public UIApplication UiApplication { get; set; }
        }
    }
} 