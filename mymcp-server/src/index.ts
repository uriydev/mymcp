import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { registerSmartRevitTools } from "./tools/smartRevitTools.js";

// Создаем сервер MCP
const server = new McpServer({
  name: "smart-revit-mcp",
  version: "1.0.0",
  description: "Smart Revit plugin with intelligent MEP routing and obstacle avoidance"
});

async function main() {
  console.error("Starting Smart Revit MCP Server...");

  // Регистрируем инструменты
  await registerSmartRevitTools(server);

  // Подключаемся к транспорту
  const transport = new StdioServerTransport();
  await server.connect(transport);

  console.error("Smart Revit MCP Server started successfully!");
  console.error("Available tools:");
  console.error("- execute_dynamic_revit_command: Выполнение любых команд на естественном языке");
  console.error("- revit_health_check: Проверка соединения с Revit");
}

main(); 