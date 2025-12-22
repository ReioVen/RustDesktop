using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RustDesktop.Core.Services;
using RustDesktop.App.ViewModels;
using System;
using System.Windows;

namespace RustDesktop.App;

public partial class App : Application
{
    private IHost? _host;

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
                
                // Register ViewModels
                services.AddTransient<MainViewModel>(sp => new MainViewModel(
                    sp.GetRequiredService<IRustPlusService>(),
                    sp.GetRequiredService<IRustDataService>(),
                    sp.GetRequiredService<IActiveSessionService>(),
                    sp.GetRequiredService<IPairingService>(),
                    sp.GetService<IPairingListener>(),
                    sp.GetService<ILoggingService>(),
                    sp.GetService<IItemNameService>(),
                    sp.GetService<IconValidationService>()));
                
                // Register Views
                services.AddTransient<Views.MainWindow>();
            })
                .Build();

            var serviceProvider = _host.Services;
            
            // Initialize IconPathConverter with services
            var iconValidationService = serviceProvider.GetService<IconValidationService>();
            var logger = serviceProvider.GetService<ILoggingService>();
            Converters.IconPathConverter.Initialize(iconValidationService, logger);
            
            var mainWindow = serviceProvider.GetRequiredService<Views.MainWindow>();
            mainWindow.Show();
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
        _host?.Dispose();
        base.OnExit(e);
    }
}

