using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using EnvFlow.Services;
using EnvFlow.ViewModels;

namespace EnvFlow;

public partial class App : Application
{
    private Window? m_window;
    
    public static Window? MainWindow { get; private set; }
    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        this.InitializeComponent();
        this.UnhandledException += App_UnhandledException;
        
        // Configure DI
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Register services
        services.AddSingleton<EnvVarService>();
        
        // Register ViewModels
        services.AddTransient<MainWindowViewModel>();
        
        // Register Windows
        services.AddTransient<MainWindow>();
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"Unhandled exception: {e.Exception}");
        e.Handled = false; // Let it crash to see the error
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            m_window = Services.GetRequiredService<MainWindow>();
            MainWindow = m_window;
            m_window.Activate();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error launching window: {ex}");
            throw;
        }
    }
}
