using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RustDesktop.Core.Services;
using RustDesktop.App.ViewModels;
using RustDesktop.App.Services;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Windows;

namespace RustDesktop.App;

public partial class App : Application
{
    private IHost? _host;
    private ISystemTrayService? _systemTrayService;
    private Views.MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Handle unhandled exceptions
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        try
        {
            _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Register services
                services.AddSingleton<ILoggingService, LoggingService>();
                services.AddSingleton<IRustIconService>(sp => 
                    new RustIconService(sp.GetService<ILoggingService>()));
                services.AddSingleton<IItemNameService>(sp => 
                    new ItemNameService(sp.GetService<IRustIconService>()));
                // Update RustIconService with IItemNameService after both are created (to avoid circular dependency)
                services.AddSingleton(provider =>
                {
                    var rustIconService = provider.GetRequiredService<IRustIconService>() as RustIconService;
                    var itemNameService = provider.GetRequiredService<IItemNameService>();
                    if (rustIconService != null)
                    {
                        // Use reflection to set the _itemNameService field
                        var field = typeof(RustIconService).GetField("_itemNameService", 
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (field != null)
                        {
                            field.SetValue(rustIconService, itemNameService);
                        }
                    }
                    return rustIconService!;
                });
                services.AddSingleton<IconValidationService>(sp => 
                    new IconValidationService(
                        sp.GetService<IItemNameService>(),
                        sp.GetService<ILoggingService>()));
                services.AddSingleton<IPairingService>(sp => 
                    new PairingService(sp.GetService<ILoggingService>()));
                services.AddSingleton<IServerInfoService, ServerInfoService>();
                services.AddSingleton<IPortScanner>(sp => 
                    new PortScanner(sp.GetService<ILoggingService>()));
                services.AddSingleton<IRustPlusService>(sp => 
                    new RustPlusService(
                        sp.GetService<ILoggingService>(),
                        sp.GetService<IServerInfoService>(),
                        sp.GetService<IItemNameService>()));
                services.AddSingleton<IConfigurationService, ConfigurationService>();
                services.AddSingleton<IRustDataService>(sp => 
                    new RustDataService(sp.GetService<ILoggingService>()));
                services.AddSingleton<IActiveSessionService>(sp => 
                    new ActiveSessionService(sp.GetService<ILoggingService>()));
                
                // Register FCM Pairing Listener (uses Node.js + rustplus-cli)
                services.AddSingleton<IPairingListener>(sp => 
                    new PairingListenerRealProcess(msg => sp.GetService<ILoggingService>()?.LogInfo(msg)));
                
                // Register App services
                services.AddSingleton<ISystemTrayService, SystemTrayService>();
                services.AddSingleton<INotificationService, NotificationService>();
                services.AddSingleton<IUpdateService>(sp => 
                    new UpdateService(sp.GetService<ILoggingService>()));
                
                // Register ViewModels
                services.AddTransient<MainViewModel>(sp => new MainViewModel(
                    sp.GetRequiredService<IRustPlusService>(),
                    sp.GetRequiredService<IRustDataService>(),
                    sp.GetRequiredService<IActiveSessionService>(),
                    sp.GetRequiredService<IPairingService>(),
                    sp.GetService<IPairingListener>(),
                    sp.GetService<ILoggingService>(),
                    sp.GetService<IItemNameService>(),
                    sp.GetService<IconValidationService>(),
                    sp.GetService<INotificationService>()));
                
                // Register Views
                services.AddTransient<Views.MainWindow>(sp => new Views.MainWindow(
                    sp.GetRequiredService<MainViewModel>(),
                    sp.GetService<IconValidationService>(),
                    sp.GetService<ILoggingService>(),
                    sp.GetService<ISystemTrayService>()));
            })
                .Build();

            var serviceProvider = _host.Services;
            
            // Initialize IconPathConverter with services
            var iconValidationService = serviceProvider.GetService<IconValidationService>();
            var logger = serviceProvider.GetService<ILoggingService>();
            Converters.IconPathConverter.Initialize(iconValidationService, logger);
            
            // Initialize system tray
            _systemTrayService = serviceProvider.GetRequiredService<ISystemTrayService>();
            _systemTrayService.Initialize();
            _systemTrayService.ShowRequested += OnShowRequested;
            _systemTrayService.ExitRequested += OnExitRequested;
            
            // Check for pending updates and install them
            CheckAndInstallPendingUpdate(serviceProvider);
            
            _mainWindow = serviceProvider.GetRequiredService<Views.MainWindow>();
            _mainWindow.Show();
            
            // Check for updates in background (don't block startup)
            _ = Task.Run(async () =>
            {
                await CheckForUpdatesAsync(serviceProvider);
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to start application: {ex.Message}\n\n{ex.StackTrace}",
                "Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            MessageBox.Show(
                $"An unhandled error occurred: {ex.Message}\n\n{ex.StackTrace}",
                "Application Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"An error occurred: {e.Exception.Message}\n\nThe application will continue running.",
            "Error",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        e.Handled = true; // Prevent app crash
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _systemTrayService?.Dispose();
        _host?.Dispose();
        base.OnExit(e);
    }

    private void OnShowRequested(object? sender, EventArgs e)
    {
        _mainWindow?.ShowWindow();
    }

    private void OnExitRequested(object? sender, EventArgs e)
    {
        Shutdown();
    }

    private void CheckAndInstallPendingUpdate(IServiceProvider serviceProvider)
    {
        try
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var markerPath = Path.Combine(appDir, "update-pending.txt");
            
            if (File.Exists(markerPath))
            {
                var logger = serviceProvider.GetService<ILoggingService>();
                logger?.LogInfo("Pending update detected, installing...");
                
                var newVersionPath = File.ReadAllText(markerPath).Trim();
                if (File.Exists(newVersionPath))
                {
                    // Extract the new version to a temp location
                    var tempDir = Path.Combine(Path.GetTempPath(), "RustDesktop-Update");
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                    Directory.CreateDirectory(tempDir);
                    
                    // Extract ZIP to temp directory
                    System.IO.Compression.ZipFile.ExtractToDirectory(newVersionPath, tempDir);
                    
                    // Create a batch script to replace files on next startup
                    var updateScript = Path.Combine(appDir, "apply-update.bat");
                    var scriptContent = $@"@echo off
timeout /t 2 /nobreak >nul
xcopy /Y /E /I ""{tempDir}\*"" ""{appDir}""
del ""{markerPath}""
del ""{newVersionPath}""
del ""{updateScript}""
start """" ""{Path.Combine(appDir, "RustDesktop.exe")}""
";
                    File.WriteAllText(updateScript, scriptContent);
                    
                    // Delete the marker file
                    File.Delete(markerPath);
                    
                    logger?.LogInfo("Update installation scheduled. Restart the app to apply.");
                    
                    MessageBox.Show(
                        "An update has been downloaded and will be installed on the next restart.\n\nPlease restart the application to apply the update.",
                        "Update Ready",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    File.Delete(markerPath);
                }
            }
        }
        catch (Exception ex)
        {
            var logger = serviceProvider.GetService<ILoggingService>();
            logger?.LogError($"Error checking for pending update: {ex.Message}", ex);
        }
    }

    private async Task CheckForUpdatesAsync(IServiceProvider serviceProvider)
    {
        try
        {
            var updateService = serviceProvider.GetService<IUpdateService>();
            var logger = serviceProvider.GetService<ILoggingService>();
            
            if (updateService == null) return;
            
            // Get update URL - use GitHub Releases API
            // Format: https://api.github.com/repos/{owner}/{repo}/releases/latest
            var githubOwner = "ReioVen"; // Your GitHub username
            var githubRepo = "RustDesktop"; // Your repository name
            var updateUrl = $"https://api.github.com/repos/{githubOwner}/{githubRepo}/releases/latest";
            
            var updateInfo = await updateService.CheckForUpdatesAsync(updateUrl);
            
            if (updateInfo != null)
            {
                logger?.LogInfo($"Update available: {updateInfo.Version}");
                
                // Show update notification to user
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var result = MessageBox.Show(
                        $"A new version ({updateInfo.Version}) is available!\n\n" +
                        $"Release Notes:\n{updateInfo.ReleaseNotes}\n\n" +
                        $"Would you like to download and install it now?",
                        "Update Available",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        _ = DownloadAndInstallUpdateAsync(updateService, updateInfo, logger);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            var logger = serviceProvider.GetService<ILoggingService>();
            logger?.LogError($"Error checking for updates: {ex.Message}", ex);
        }
    }

    private async Task DownloadAndInstallUpdateAsync(IUpdateService updateService, UpdateInfo updateInfo, ILoggingService? logger)
    {
        try
        {
            var tempDir = Path.GetTempPath();
            var downloadPath = Path.Combine(tempDir, $"RustDesktop-{updateInfo.Version}.zip");
            
            var progress = new Progress<double>(percentage =>
            {
                logger?.LogInfo($"Download progress: {percentage:F1}%");
            });
            
            var downloaded = await updateService.DownloadUpdateAsync(updateInfo.DownloadUrl, downloadPath, progress);
            
            if (downloaded)
            {
                updateService.ScheduleUpdateInstallation(downloadPath);
                logger?.LogInfo("Update downloaded. Will be installed on next restart.");
                
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(
                        "Update downloaded successfully!\n\n" +
                        "The update will be installed when you restart the application.",
                        "Update Downloaded",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                });
            }
            else
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(
                        "Failed to download the update. Please try again later.",
                        "Download Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                });
            }
        }
        catch (Exception ex)
        {
            logger?.LogError($"Error downloading update: {ex.Message}", ex);
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(
                    $"Error downloading update: {ex.Message}",
                    "Update Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            });
        }
    }
}

