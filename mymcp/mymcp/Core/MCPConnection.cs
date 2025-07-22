using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Autodesk.Revit.UI;

namespace mymcp.Core;

/// <summary>
/// Подключение к MCP серверу
/// </summary>
public class MCPConnection
{
    private static MCPConnection _instance;
    private TcpListener _listener;
    private Thread _listenerThread;
    private bool _isRunning;
    private readonly int _port = 8080;
    private UIApplication _uiApp;
    private CommandProcessor _commandProcessor;

    public static MCPConnection Instance => _instance ??= new MCPConnection();

    public bool IsRunning => _isRunning;

    public void Initialize(UIApplication uiApp)
    {
        _uiApp = uiApp;
        _commandProcessor = new CommandProcessor(uiApp);
        Logger.Info("MCP Connection initialized");
    }

    public void Start()
    {
        if (_isRunning) return;

        try
        {
            _isRunning = true;
            _listener = new TcpListener(System.Net.IPAddress.Any, _port);
            _listener.Start();

            _listenerThread = new Thread(ListenForClients)
            {
                IsBackground = true
            };
            _listenerThread.Start();

            Logger.Info($"MCP Server started on port {_port}");
        }
        catch (Exception ex)
        {
            _isRunning = false;
            Logger.Error("Failed to start MCP server", ex);
            throw;
        }
    }

    public void Stop()
    {
        if (!_isRunning) return;

        try
        {
            _isRunning = false;
            _listener?.Stop();
            _listener = null;

            if (_listenerThread?.IsAlive == true)
            {
                _listenerThread.Join(1000);
            }

            Logger.Info("MCP Server stopped");
        }
        catch (Exception ex)
        {
            Logger.Error("Error stopping MCP server", ex);
        }
    }

    private void ListenForClients()
    {
        try
        {
            while (_isRunning)
            {
                var client = _listener.AcceptTcpClient();
                var clientThread = new Thread(HandleClientCommunication)
                {
                    IsBackground = true
                };
                clientThread.Start(client);
            }
        }
        catch (SocketException)
        {
            // Нормальное завершение при остановке сервера
        }
        catch (Exception ex)
        {
            Logger.Error("Error in client listener", ex);
        }
    }

    private void HandleClientCommunication(object clientObj)
    {
        var tcpClient = (TcpClient)clientObj;
        var stream = tcpClient.GetStream();

        try
        {
            var buffer = new byte[8192];

            while (_isRunning && tcpClient.Connected)
            {
                int bytesRead = stream.Read(buffer, 0, buffer.Length);

                if (bytesRead == 0) break;

                var message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Logger.Debug($"Received message: {message}");

                var response = ProcessCommand(message);

                var responseData = Encoding.UTF8.GetBytes(response);
                stream.Write(responseData, 0, responseData.Length);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Error handling client communication", ex);
        }
        finally
        {
            tcpClient.Close();
        }
    }

    private string ProcessCommand(string commandJson)
    {
        try
        {
            // Используем CommandProcessor для обработки команд
            var result = _commandProcessor.ProcessCommand(commandJson);
            return JsonConvert.SerializeObject(result, Formatting.Indented);
        }
        catch (Exception ex)
        {
            Logger.Error("Error processing command", ex);
            
            var errorResponse = new
            {
                success = false,
                error = ex.Message,
                timestamp = DateTime.Now
            };

            return JsonConvert.SerializeObject(errorResponse);
        }
    }
} 