using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace RustDesktop.Core.Services;

public class PortScanner : IPortScanner
{
    private readonly ILoggingService? _logger;

    public PortScanner(ILoggingService? logger = null)
    {
        _logger = logger;
    }

    public async Task<bool> IsPortOpenAsync(string ipAddress, int port, int timeoutMs = 1000)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var client = new TcpClient();
                var result = client.BeginConnect(ipAddress, port, null, null);
                var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(timeoutMs));
                
                if (success && client.Connected)
                {
                    client.EndConnect(result);
                    return true;
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        });
    }

    public async Task<List<int>> ScanPortsAsync(string ipAddress, int startPort, int endPort, int timeoutMs = 1000)
    {
        var openPorts = new List<int>();
        
        _logger?.LogDebug($"Scanning ports {startPort}-{endPort} on {ipAddress}...");
        
        var tasks = new List<Task>();
        var semaphore = new SemaphoreSlim(10); // Limit concurrent scans
        
        for (int port = startPort; port <= endPort; port++)
        {
            await semaphore.WaitAsync();
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    if (await IsPortOpenAsync(ipAddress, port, timeoutMs))
                    {
                        lock (openPorts)
                        {
                            openPorts.Add(port);
                        }
                        _logger?.LogDebug($"Found open port: {port}");
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }
        
        await Task.WhenAll(tasks);
        
        _logger?.LogInfo($"Port scan complete. Found {openPorts.Count} open ports: {string.Join(", ", openPorts)}");
        return openPorts.OrderBy(p => p).ToList();
    }
}
















