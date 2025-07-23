import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { RevitConnection } from "../utils/revitConnection.js";

const revitConnection = new RevitConnection();

interface DynamicCommandArgs {
  command_description: string;
  complexity_level: "simple" | "moderate" | "complex" | "advanced";
  safety_mode: boolean;
  optimization_level: "speed" | "quality" | "balanced";
  parameters?: Record<string, any>;
}

export function registerSmartRevitTools(server: McpServer) {
  // Универсальный инструмент для динамических команд
  server.tool(
    "execute_dynamic_revit_command",
    "🚀 Execute ANY Revit command described in natural language with dynamic code generation! This is the most powerful tool that can handle any complex MEP, architectural, or engineering task.",
    {
      command_description: z.string().describe("Natural language description of what you want to do in Revit (in Russian or English). Examples: 'Создай систему вентиляции для офиса', 'Create smart ductwork avoiding obstacles', 'Optimize existing MEP systems for energy efficiency'"),
      complexity_level: z.enum(["simple", "moderate", "complex", "advanced"]).default("moderate").describe("Complexity level affects execution time and resource usage"),
      safety_mode: z.boolean().default(true).describe("Enable additional safety checks and validation"),
      optimization_level: z.enum(["speed", "quality", "balanced"]).default("balanced").describe("Optimization focus: speed for quick results, quality for best output, balanced for both"),
      parameters: z.record(z.any()).optional().describe("Additional parameters as key-value pairs (coordinates, sizes, etc.)")
    },
    async (args: DynamicCommandArgs) => {
      try {
        const response = await revitConnection.sendCommand("execute_dynamic_command", {
          description: args.command_description,
          complexity: args.complexity_level,
          safety: args.safety_mode,
          optimization: args.optimization_level,
          params: args.parameters
        });

        if (response.success) {
          return {
            content: [{
              type: "text",
              text: `✅ **Команда выполнена успешно!**\n\n` +
                    `📊 **Результат:**\n${response.result}\n\n` +
                    `⏱️ **Время выполнения:** ${response.executionTime}мс\n` +
                    `💬 **Сообщение:** ${response.message}`
            }]
          };
        } else {
          return {
            content: [{
              type: "text",
              text: `❌ **Ошибка выполнения команды:** ${response.error}`
            }]
          };
        }
      } catch (error) {
        return {
          content: [{
            type: "text",
            text: `💥 **Ошибка подключения:** ${error instanceof Error ? error.message : String(error)}\n\nПроверьте подключение к Revit.`
          }]
        };
      }
    }
  );

  // Оставляем только инструмент проверки соединения с Revit
  server.tool(
    "revit_health_check",
    "Check connection status and system health with Revit",
    {},
    async () => {
      try {
        const response = await revitConnection.sendCommand("health_check", {});
        if (response.success) {
          return {
            content: [{
              type: "text",
              text: `🟢 **Система работает отлично!**\n\n` +
                    `🔌 **Подключение к Revit:** ✅ Активно\n` +
                    `📄 **Документ:** ${response.status.documentName}\n` +
                    `🕐 **Время проверки:** ${new Date(response.status.timestamp).toLocaleString('ru-RU')}\n` +
                    `📦 **Версия:** ${response.status.version}\n\n` +
                    `🚀 **Динамические команды:** ${response.status.dynamicCommands?.enabled ? '✅ Включены' : '❌ Отключены'}\n` +
                    `💾 **Кэшированных команд:** ${response.status.dynamicCommands?.cachedCommands || 0}\n` +
                    `✅ **Валидных команд:** ${response.status.dynamicCommands?.validCommands || 0}\n\n` +
                    `🎯 **Готов к выполнению любых команд!**`
            }]
          };
        } else {
          return {
            content: [{
              type: "text",
              text: `🟡 **Проблемы с подключением**\n\n❌ ${response.error}`
            }]
          };
        }
      } catch (error) {
        return {
          content: [{
            type: "text",
            text: `🔴 **Revit недоступен**\n\n` +
                  `❗ Ошибка: ${error instanceof Error ? error.message : String(error)}\n\n` +
                  `🔧 **Проверьте:**\n` +
                  `• Revit запущен\n` +
                  `• Плагин загружен\n` +
                  `• MCP сервер запущен\n` +
                  `• Порт 3001 свободен`
          }]
        };
      }
    }
  );
}