using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using mymcp.Core;

namespace mymcp.Commands;

/// <summary>
///     External command entry point invoked from the Revit interface
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class StartupCommand : ExternalCommand
{
    public override void Execute()
    {
        try
        {
            var uiApp = ExternalCommandData.Application;
            var mcpConnection = MCPConnection.Instance;

            if (mcpConnection.IsRunning)
            {
                // Останавливаем сервер
                mcpConnection.Stop();
                TaskDialog.Show("MCP Server", 
                    "MCP сервер остановлен.\n\n" +
                    "Связь с Cursor прервана.");
                Logger.Info("MCP server stopped by user");
            }
            else
            {
                // Запускаем сервер
                mcpConnection.Initialize(uiApp);
                mcpConnection.Start();
                
                var dialog = new TaskDialog("MCP Server")
                {
                    MainContent = "MCP сервер запущен на порту 8080.\n\n" +
                                  "Теперь можно подключиться из Cursor:\n" +
                                  "1. Убедитесь, что MCP сервер настроен в Cursor\n" +
                                  "2. Используйте команды на естественном языке\n" +
                                  "3. Например: 'Создай воздуховод от точки А до Б с обходом препятствий'",
                    MainIcon = TaskDialogIcon.TaskDialogIconInformation,
                    CommonButtons = TaskDialogCommonButtons.Ok
                };
                
                dialog.Show();
                Logger.Info("MCP server started by user");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Error in StartupCommand", ex);
            TaskDialog.Show("Ошибка", 
                $"Произошла ошибка при управлении MCP сервером:\n{ex.Message}");
        }
    }
}