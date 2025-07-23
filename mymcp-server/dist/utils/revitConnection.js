import * as net from 'net';
export class RevitConnection {
    host = 'localhost';
    port = 8080;
    timeout = 5000; // 5 секунд
    async sendCommand(method, params) {
        return new Promise((resolve, reject) => {
            const client = new net.Socket();
            let responseData = '';
            // Устанавливаем тайм-аут
            client.setTimeout(this.timeout);
            client.on('timeout', () => {
                client.destroy();
                reject(new Error('Connection timeout. Make sure Revit plugin is running and MCP server is started.'));
            });
            client.on('error', (error) => {
                reject(new Error(`Failed to connect to Revit: ${error instanceof Error ? error.message : String(error)}. Please ensure the MCP server is running in Revit.`));
            });
            client.on('data', (data) => {
                responseData += data.toString();
            });
            client.on('close', () => {
                try {
                    const response = JSON.parse(responseData);
                    resolve(response);
                }
                catch (error) {
                    reject(new Error(`Invalid response from Revit: ${responseData}`));
                }
            });
            // Подключаемся к Revit
            client.connect(this.port, this.host, () => {
                const command = {
                    method: method,
                    params: params,
                    id: Date.now().toString()
                };
                client.write(JSON.stringify(command));
                client.end();
            });
        });
    }
    async testConnection() {
        try {
            await this.sendCommand('health_check', {});
            return true;
        }
        catch {
            return false;
        }
    }
}
//# sourceMappingURL=revitConnection.js.map