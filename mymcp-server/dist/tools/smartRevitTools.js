import { z } from "zod";
import { RevitConnection } from "../utils/revitConnection.js";
const revitConnection = new RevitConnection();
export function registerSmartRevitTools(server) {
    // ====== НОВЫЙ УНИВЕРСАЛЬНЫЙ ИНСТРУМЕНТ ДЛЯ ДИНАМИЧЕСКИХ КОМАНД ======
    server.tool("execute_dynamic_revit_command", "🚀 Execute ANY Revit command described in natural language with dynamic code generation! This is the most powerful tool that can handle any complex MEP, architectural, or engineering task.", {
        command_description: z.string().describe("Natural language description of what you want to do in Revit (in Russian or English). Examples: 'Создай систему вентиляции для офиса', 'Create smart ductwork avoiding obstacles', 'Optimize existing MEP systems for energy efficiency'"),
        complexity_level: z.enum(["simple", "moderate", "complex", "advanced"]).default("moderate").describe("Complexity level affects execution time and resource usage"),
        safety_mode: z.boolean().default(true).describe("Enable additional safety checks and validation"),
        optimization_level: z.enum(["speed", "quality", "balanced"]).default("balanced").describe("Optimization focus: speed for quick results, quality for best output, balanced for both"),
        parameters: z.record(z.any()).optional().describe("Additional parameters as key-value pairs (coordinates, sizes, etc.)")
    }, async (args) => {
        try {
            const response = await revitConnection.sendCommand("execute_dynamic_command", {
                description: args.command_description,
                complexity: args.complexity_level,
                safety: args.safety_mode,
                optimization: args.optimization_level,
                parameters: args.parameters || {}
            });
            if (response.success) {
                return {
                    content: [{
                            type: "text",
                            text: `🎯 **Динамическая команда выполнена успешно!**\n\n` +
                                `📝 **Описание:** ${args.command_description}\n` +
                                `⚙️ **Сгенерировано кода:** ${response.generatedLines || 0} строк\n` +
                                `⏱️ **Время выполнения:** ${response.executionTime || 0}мс\n` +
                                `📊 **Сложность:** ${args.complexity_level}\n` +
                                `🛡️ **Безопасность:** ${args.safety_mode ? 'включена' : 'отключена'}\n` +
                                `🎛️ **Оптимизация:** ${args.optimization_level}\n\n` +
                                `✅ **Результат:** ${response.message}\n\n` +
                                `${response.details || 'Команда завершена успешно!'}\n\n` +
                                `${response.warnings && response.warnings.length > 0 ?
                                    `⚠️ **Предупреждения:**\n${response.warnings.map((w) => `• ${w}`).join('\n')}\n\n` : ''}` +
                                `🆔 **Элементы:** создано ${response.elementsCreated || 0}, изменено ${response.elementsModified || 0}`
                        }]
                };
            }
            else {
                return {
                    content: [{
                            type: "text",
                            text: `❌ **Ошибка выполнения динамической команды**\n\n` +
                                `📝 **Описание:** ${args.command_description}\n` +
                                `❗ **Ошибка:** ${response.error}\n` +
                                `⏱️ **Время выполнения:** ${response.executionTime || 0}мс\n\n` +
                                `${response.warnings && response.warnings.length > 0 ?
                                    `⚠️ **Предупреждения:**\n${response.warnings.map((w) => `• ${w}`).join('\n')}` : ''}`
                        }]
                };
            }
        }
        catch (error) {
            return {
                content: [{
                        type: "text",
                        text: `💥 **Критическая ошибка при выполнении динамической команды**\n\n` +
                            `📝 **Описание:** ${args.command_description}\n` +
                            `❗ **Ошибка:** ${error instanceof Error ? error.message : String(error)}\n\n` +
                            `🔧 **Рекомендации:**\n` +
                            `• Проверьте подключение к Revit\n` +
                            `• Убедитесь что документ открыт\n` +
                            `• Попробуйте более простое описание`
                    }]
            };
        }
    });
    // ====== СУЩЕСТВУЮЩИЕ СПЕЦИАЛИЗИРОВАННЫЕ ИНСТРУМЕНТЫ ======
    server.tool("smart_create_duct", "Create intelligent ductwork with advanced pathfinding and obstacle avoidance", {
        start_point: z.object({
            x: z.number(),
            y: z.number(),
            z: z.number()
        }).describe("Starting point coordinates in Revit units"),
        end_point: z.object({
            x: z.number(),
            y: z.number(),
            z: z.number()
        }).describe("Ending point coordinates in Revit units"),
        width: z.number().default(1.0).describe("Duct width in feet"),
        height: z.number().default(0.5).describe("Duct height in feet"),
        system_type: z.string().optional().describe("HVAC system type")
    }, async (args) => {
        try {
            const response = await revitConnection.sendCommand("smart_create_duct", {
                start: args.start_point,
                end: args.end_point,
                width: args.width,
                height: args.height,
                systemType: args.system_type
            });
            if (response.success) {
                return {
                    content: [{
                            type: "text",
                            text: `✅ **Умный воздуховод создан!**\n\n` +
                                `📏 **Путь:** ${response.route.path.length} сегментов\n` +
                                `🔧 **Фитинги:** ${response.route.fittings.length} элементов\n` +
                                `📐 **Длина:** ${response.route.totalLength.toFixed(2)} футов\n` +
                                `💬 **Сообщение:** ${response.message}`
                        }]
                };
            }
            else {
                return {
                    content: [{
                            type: "text",
                            text: `❌ **Ошибка создания воздуховода:** ${response.error}`
                        }]
                };
            }
        }
        catch (error) {
            return {
                content: [{
                        type: "text",
                        text: `💥 **Ошибка подключения:** ${error instanceof Error ? error.message : String(error)}\n\nПроверьте подключение к Revit.`
                    }]
            };
        }
    });
    server.tool("smart_create_pipe", "Create intelligent piping with optimal routing and system integration", {
        start_point: z.object({
            x: z.number(),
            y: z.number(),
            z: z.number()
        }).describe("Starting point coordinates in Revit units"),
        end_point: z.object({
            x: z.number(),
            y: z.number(),
            z: z.number()
        }).describe("Ending point coordinates in Revit units"),
        diameter: z.number().default(0.5).describe("Pipe diameter in feet"),
        system_type: z.string().optional().describe("Plumbing system type")
    }, async (args) => {
        try {
            const response = await revitConnection.sendCommand("smart_create_pipe", {
                start: args.start_point,
                end: args.end_point,
                diameter: args.diameter,
                systemType: args.system_type
            });
            if (response.success) {
                return {
                    content: [{
                            type: "text",
                            text: `✅ **Умный трубопровод создан!**\n\n` +
                                `📏 **Путь:** ${response.route.path.length} сегментов\n` +
                                `🔧 **Фитинги:** ${response.route.fittings.length} элементов\n` +
                                `📐 **Длина:** ${response.route.totalLength.toFixed(2)} футов\n` +
                                `💬 **Сообщение:** ${response.message}`
                        }]
                };
            }
            else {
                return {
                    content: [{
                            type: "text",
                            text: `❌ **Ошибка создания трубопровода:** ${response.error}`
                        }]
                };
            }
        }
        catch (error) {
            return {
                content: [{
                        type: "text",
                        text: `💥 **Ошибка подключения:** ${error instanceof Error ? error.message : String(error)}\n\nПроверьте подключение к Revit.`
                    }]
            };
        }
    });
    server.tool("analyze_building_space", "Analyze building space for optimal MEP equipment placement and routing", {
        center_point: z.object({
            x: z.number(),
            y: z.number(),
            z: z.number()
        }).optional().describe("Center point for analysis (optional, uses model center if not provided)"),
        radius: z.number().default(10.0).describe("Analysis radius in feet"),
        analysis_type: z.enum(["general", "hvac", "plumbing", "electrical"]).default("general").describe("Type of analysis to perform")
    }, async (args) => {
        try {
            const response = await revitConnection.sendCommand("analyze_space", {
                center: args.center_point,
                radius: args.radius,
                type: args.analysis_type
            });
            if (response.success) {
                return {
                    content: [{
                            type: "text",
                            text: `📊 **Анализ пространства завершен!**\n\n` +
                                `📍 **Центр анализа:** ${response.analysis.center.x.toFixed(1)}, ${response.analysis.center.y.toFixed(1)}, ${response.analysis.center.z.toFixed(1)}\n` +
                                `📏 **Радиус:** ${response.analysis.radius} футов\n` +
                                `🏗️ **Найдено элементов:** ${response.analysis.elementCount}\n` +
                                `✅ **Свободное пространство:** ${response.analysis.isSpaceAvailable ? 'Да' : 'Нет'}\n\n` +
                                `**Найденные элементы:**\n` +
                                `${response.analysis.elements.map((e) => `• ${e.category}: ${e.name} (ID: ${e.id})`).join('\n')}`
                        }]
                };
            }
            else {
                return {
                    content: [{
                            type: "text",
                            text: `❌ **Ошибка анализа:** ${response.error}`
                        }]
                };
            }
        }
        catch (error) {
            return {
                content: [{
                        type: "text",
                        text: `💥 **Ошибка подключения:** ${error instanceof Error ? error.message : String(error)}\n\nПроверьте подключение к Revit.`
                    }]
            };
        }
    });
    server.tool("find_optimal_route", "Find optimal routing path between two points with obstacle avoidance", {
        start_point: z.object({
            x: z.number(),
            y: z.number(),
            z: z.number()
        }).describe("Starting point coordinates"),
        end_point: z.object({
            x: z.number(),
            y: z.number(),
            z: z.number()
        }).describe("Ending point coordinates"),
        clearance: z.number().default(0.5).describe("Required clearance around route in feet"),
        route_type: z.enum(["duct", "pipe", "cable", "general"]).default("general").describe("Type of route to optimize for")
    }, async (args) => {
        try {
            const response = await revitConnection.sendCommand("find_optimal_route", {
                start: args.start_point,
                end: args.end_point,
                clearance: args.clearance,
                type: args.route_type
            });
            if (response.success) {
                return {
                    content: [{
                            type: "text",
                            text: `🗺️ **Оптимальный маршрут найден!**\n\n` +
                                `📏 **Основной путь:** ${response.routing.optimalPath.length} точек\n` +
                                `🔀 **Альтернативы:** ${response.routing.alternativePaths.length} вариантов\n` +
                                `🛡️ **Зазор:** ${response.routing.clearance} футов\n\n` +
                                `**Координаты пути:**\n` +
                                `${response.routing.optimalPath.slice(0, 5).map((p, i) => `${i + 1}. (${p.x.toFixed(1)}, ${p.y.toFixed(1)}, ${p.z.toFixed(1)})`).join('\n')}` +
                                `${response.routing.optimalPath.length > 5 ? `\n... и еще ${response.routing.optimalPath.length - 5} точек` : ''}`
                        }]
                };
            }
            else {
                return {
                    content: [{
                            type: "text",
                            text: `❌ **Ошибка поиска маршрута:** ${response.error}`
                        }]
                };
            }
        }
        catch (error) {
            return {
                content: [{
                        type: "text",
                        text: `💥 **Ошибка подключения:** ${error instanceof Error ? error.message : String(error)}\n\nПроверьте подключение к Revit.`
                    }]
            };
        }
    });
    server.tool("revit_health_check", "Check connection status and system health with Revit", {}, async (args) => {
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
            }
            else {
                return {
                    content: [{
                            type: "text",
                            text: `🟡 **Проблемы с подключением**\n\n❌ ${response.error}`
                        }]
                };
            }
        }
        catch (error) {
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
    });
}
//# sourceMappingURL=smartRevitTools.js.map