export declare class RevitConnection {
    private host;
    private port;
    private timeout;
    sendCommand(method: string, params: any): Promise<any>;
    testConnection(): Promise<boolean>;
}
