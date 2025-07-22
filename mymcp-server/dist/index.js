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
    console.error("- smart_create_duct: Create intelligent ductwork with obstacle avoidance");
    console.error("- smart_create_pipe: Create intelligent piping with optimal routing");
    console.error("- analyze_building_space: Analyze building space for optimal placement");
    console.error("- find_optimal_route: Find optimal MEP routing between points");
    console.error("- revit_health_check: Check connection status with Revit");
}
main().catch((error) => {
    console.error("Error starting Smart Revit MCP Server:", error);
    process.exit(1);
});
//# sourceMappingURL=index.js.map