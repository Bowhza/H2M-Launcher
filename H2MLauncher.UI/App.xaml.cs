using System.Windows;

using H2MLauncher.Core.Services;
using H2MLauncher.Core.ViewModels;
using H2MLauncher.UI.Dialog;

using Microsoft.Extensions.DependencyInjection;

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
            services.AddSingleton<DialogViewModel>((s) =>
            {
                DialogViewModel dialogViewModel = new();
                Application.Current.Resources.Add("DialogViewModel", dialogViewModel);
                return dialogViewModel;
            });
            services.AddSingleton<DialogService>();
            services.AddTransient<IClipBoardService, ClipBoardService>();
            services.AddTransient<ServerBrowserViewModel>();
        }
    }
}
