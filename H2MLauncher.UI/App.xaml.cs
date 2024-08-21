using System.Windows;

using H2MLauncher.Core.Services;
using H2MLauncher.Core.ViewModels;

using Microsoft.Extensions.DependencyInjection;

namespace H2MLauncher.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public IServiceProvider ServiceProvider { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            ServiceCollection serviceCollection = new();
            ConfigureServices(serviceCollection);
            ServiceProvider = serviceCollection.BuildServiceProvider();
            MainWindow mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddTransient<H2MLauncherService>();
            services.AddHttpClient<H2MLauncherService>();

            services.AddTransient<RaidMaxService>();
            services.AddHttpClient<RaidMaxService>();

            services.AddTransient<GameServerCommunicationService>();

            services.AddSingleton<H2MCommunicationService>();

            services.AddTransient<MainWindow>();

            services.AddTransient<ServerBrowserViewModel>();
        }
    }
}
