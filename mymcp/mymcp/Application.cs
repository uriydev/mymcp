using Nice3point.Revit.Toolkit.External;
using mymcp.Commands;
using mymcp.Core;

namespace mymcp;

/// <summary>
///     Application entry point
/// </summary>
[UsedImplicitly]
public class Application : ExternalApplication
{
    public override void OnStartup()
    {
        try
        {
            Logger.Info("Starting mymcp application");
            CreateRibbon();
            Logger.Info("mymcp application started successfully");
        }
        catch (Exception ex)
        {
            Logger.Error("Error starting mymcp application", ex);
        }
    }

    public override void OnShutdown()
    {
        try
        {
            // Останавливаем MCP сервер при завершении
            if (MCPConnection.Instance.IsRunning)
            {
                MCPConnection.Instance.Stop();
            }
            Logger.Info("mymcp application shutdown completed");
        }
        catch (Exception ex)
        {
            Logger.Error("Error during application shutdown", ex);
        }
    }

    private void CreateRibbon()
    {
        var panel = Application.CreatePanel("Smart MEP Commands", "mymcp");

        // Основная команда для запуска/остановки MCP сервера
        panel.AddPushButton<StartupCommand>("Start MCP Server")
            .SetImage("/mymcp;component/Resources/Icons/RibbonIcon16.png")
            .SetLargeImage("/mymcp;component/Resources/Icons/RibbonIcon32.png")
            .SetToolTip("Запустить/остановить MCP сервер для связи с Cursor");

        panel.AddSeparator();

        // В будущем добавим больше умных команд
        // panel.AddPushButton<SmartCreatePipeCommand>("Smart Pipe")
        // panel.AddPushButton<SmartCreateElectricalCommand>("Smart Electrical")
    }
}