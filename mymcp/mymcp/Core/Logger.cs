using System;
using System.IO;

namespace mymcp.Core;

/// <summary>
/// Система логирования для плагина
/// </summary>
public static class Logger
{
    private static readonly string LogFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "mymcp", "logs", $"mymcp_{DateTime.Now:yyyyMMdd}.log");

    static Logger()
    {
        // Создаем директорию для логов если её нет
        var logDir = Path.GetDirectoryName(LogFilePath);
        if (!Directory.Exists(logDir))
        {
            Directory.CreateDirectory(logDir);
        }
    }

    public static void Info(string message)
    {
        WriteLog("INFO", message);
    }

    public static void Warning(string message)
    {
        WriteLog("WARNING", message);
    }

    public static void Error(string message, Exception ex = null)
    {
        var fullMessage = ex != null ? $"{message}. Exception: {ex}" : message;
        WriteLog("ERROR", fullMessage);
    }

    public static void Debug(string message)
    {
        WriteLog("DEBUG", message);
    }

    private static void WriteLog(string level, string message)
    {
        try
        {
            var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
            File.AppendAllText(LogFilePath, logEntry + Environment.NewLine);
            
            // Также выводим в консоль для отладки
            System.Diagnostics.Debug.WriteLine(logEntry);
        }
        catch
        {
            // Игнорируем ошибки логирования
        }
    }
} 