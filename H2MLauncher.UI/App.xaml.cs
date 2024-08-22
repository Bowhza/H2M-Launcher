using System.Windows;

using H2MLauncher.Core.Services;
using H2MLauncher.Core.ViewModels;
using H2MLauncher.UI.Dialog;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace H2MLauncher.UI
{
    public partial class App : Application
    {
        public IServiceProvider ServiceProvider { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            ServiceCollection serviceCollection = new();
            ConfigureServices(serviceCollection);
            ServiceProvider = serviceCollection.BuildServiceProvider();
            MainWindow mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
            base.OnStartup(e);
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // TODO: add logging provider
            //services.AddLogging(builder => builder.AddProvider());

            services.AddSingleton<DialogViewModel>((s) =>
            {
                return (DialogViewModel)Application.Current.FindResource("DialogViewModel");
            });
            
            services.AddSingleton<DialogService>();
            services.AddTransient<IErrorHandlingService, ErrorHandlingService>();

            services.AddTransient<H2MLauncherService>();
            services.AddHttpClient<H2MLauncherService>();

            services.AddTransient<RaidMaxService>();
            services.AddHttpClient<RaidMaxService>();

            services.AddSingleton<H2MCommunicationService>();
            services.AddTransient<GameServerCommunicationService>();

            services.AddTransient<IClipBoardService, ClipBoardService>();
            services.AddTransient<ServerBrowserViewModel>();

            services.AddTransient<MainWindow>();
        }
    }
}
