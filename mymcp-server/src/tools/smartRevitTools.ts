import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { RevitConnection } from "../utils/revitConnection.js";

const revitConnection = new RevitConnection();

interface DynamicCommandArgs {
  command_description: string;
  safety_mode: boolean;
}

export function registerSmartRevitTools(server: McpServer) {
  // Универсальный инструмент для динамических команд
  server.tool(
    "execute_dynamic_revit_command",
    "🚀 Execute ANY Revit command described in natural language with dynamic code generation using Groq API!",
    {
      command_description: z.string().describe("Natural language description of what you want to do in Revit (in Russian or English). Examples: 'create wall from 0,0,0 to 10,0,0', 'create door in the first wall', 'select all walls'"),
      safety_mode: z.boolean().default(true).describe("Enable additional safety checks and validation")
    },
    async (args: DynamicCommandArgs) => {
      try {
        const response = await revitConnection.sendCommand("execute_groq_command", {
          description: args.command_description,
          safety: args.safety_mode
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

  // Инструмент для очистки кэша динамических команд
  server.tool(
    "clear_command_cache",
    "🧹 Clear the dynamic command cache to force regeneration of commands with latest code improvements",
    {
      force_clear: z.boolean().default(false).describe("Force clear all cached commands")
    },
    async (args: { force_clear?: boolean }) => {
      try {
        const response = await revitConnection.sendCommand("clear_command_cache", {
          force_clear: args.force_clear || false
        });

        if (response.success) {
          return {
            content: [{
              type: "text",
              text: `✅ **Кэш динамических команд очищен!**\n\n` +
                    `📊 **Статистика:**\n` +
                    `• **Удалено команд:** ${response.statistics.commandsRemoved}\n` +
                    `• **Было валидных:** ${response.statistics.before.validCommands}\n` +
                    `• **Возраст старейшей команды:** ${response.statistics.before.oldestCommandAge.toFixed(1)} мин\n\n` +
                    `💾 **После очистки:**\n` +
                    `• **Команд в кэше:** ${response.statistics.after.totalCommands}\n` +
                    `• **Валидных команд:** ${response.statistics.after.validCommands}\n\n` +
                    `⏰ **Время операции:** ${new Date(response.timestamp).toLocaleString('ru-RU')}\n` +
                    `🔄 **Принудительная очистка:** ${response.forceClear ? 'Да' : 'Нет'}\n\n` +
                    `✨ **Все новые команды будут перекомпилированы с последними улучшениями!**`
            }]
          };
        } else {
          return {
            content: [{
              type: "text",
              text: `❌ **Ошибка очистки кэша:** ${response.error}`
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
    {
      random_string: z.string().optional().describe("Dummy parameter for no-parameter tools")
    },
    async (args: { random_string?: string }) => {
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
                  `• Порт 8080 свободен`
          }]
        };
      }
    }
  );
}