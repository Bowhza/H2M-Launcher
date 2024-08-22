using System.Reflection;
using System.Windows;

using H2MLauncher.Core.Services;
using H2MLauncher.Core.ViewModels;
using H2MLauncher.UI.Dialog;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Serilog;
using Serilog.Core;

namespace H2MLauncher.UI
{
    public partial class App : Application
    {
        public IServiceProvider ServiceProvider { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            SetupExceptionHandling();
            InitializeLogging();

            ServiceCollection serviceCollection = new();
            ConfigureServices(serviceCollection);
            ServiceProvider = serviceCollection.BuildServiceProvider();
            MainWindow mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
            base.OnStartup(e);
        }

        private static void InitializeLogging()
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File(Constants.LogFilePath,
                    rollingInterval: RollingInterval.Day,
                    rollOnFileSizeLimit: true)
                .WriteTo.Debug()
                .CreateLogger();

            Log.Information("{applicationName} started on version {version}.", 
                Assembly.GetExecutingAssembly().GetName(),
                H2MLauncherService.CURRENT_VERSION);
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(builder => builder.AddSerilog());

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

        private void SetupExceptionHandling()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                LogUnhandledException((Exception)e.ExceptionObject, "AppDomain.CurrentDomain.UnhandledException");

            DispatcherUnhandledException += (s, e) =>
            {
                try
                {
                    var dialogService = ServiceProvider?.GetService<DialogService>();
                    if (dialogService != null)
                    {
                        dialogService.OpenTextDialog("Error", e.Exception.Message);
                    }
                    else
                    {
                        MessageBox.Show(e.Exception.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Exception while showing error dialog");
                }
                finally
                {
                    LogUnhandledException(e.Exception, "Application.Current.DispatcherUnhandledException");
                    e.Handled = true;
                }
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                LogUnhandledException(e.Exception, "TaskScheduler.UnobservedTaskException");
                e.SetObserved();
            };
        }

        private static void LogUnhandledException(Exception exception, string source)
        {
            string message = $"Unhandled exception ({source})";
            try
            {
                AssemblyName assemblyName = Assembly.GetExecutingAssembly().GetName();
                message = string.Format("Unhandled exception in {0} v{1}", assemblyName.Name, assemblyName.Version);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception in LogUnhandledException");
            }
            finally
            {
                Log.Error(exception, message);
            }
        }
    }
}
