namespace RustDesktop.Core.Services;

public interface IPortScanner
{
    Task<List<int>> ScanPortsAsync(string ipAddress, int startPort, int endPort, int timeoutMs = 1000);
    Task<bool> IsPortOpenAsync(string ipAddress, int port, int timeoutMs = 1000);
}










