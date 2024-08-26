using System.IO;
using System.Reflection;
using System.Windows;

using Awesome.Net.WritableOptions.Extensions;

using H2MLauncher.Core;
using H2MLauncher.Core.Services;
using H2MLauncher.Core.Settings;
using H2MLauncher.Core.ViewModels;
using H2MLauncher.UI.Dialog;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Serilog;

namespace H2MLauncher.UI
{
    public partial class App : Application
    {
        public IServiceProvider ServiceProvider { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            SetupExceptionHandling();
            InitializeLogging();
            CreateNeededConfiguration();
            IConfigurationRoot config = BuildConfiguration();
            
            ServiceCollection serviceCollection = new();
            ConfigureServices(serviceCollection, config);

            ServiceProvider = serviceCollection.BuildServiceProvider();

            MainWindow mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();

            base.OnStartup(e);
        }

        private void CreateNeededConfiguration()
        {
            string storageDirectory = Path.Combine(Constants.LocalDir, "Storage");

            if (!Directory.Exists(storageDirectory))
                Directory.CreateDirectory(storageDirectory);

            string userFavoritesFilePath = Path.Combine(storageDirectory, "UserFavorites.json");

            if (File.Exists(userFavoritesFilePath))
                return;

            File.WriteAllText(userFavoritesFilePath, "[]");
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
                H2MLauncherService.CurrentVersion);
        }

        private static void ConfigureServices(IServiceCollection services, IConfigurationRoot config)
        {
            services.ConfigureWritableOptions<H2MLauncherSettings>(config, "H2MLauncher", GetLauncherSettings());

            services.Configure<ResourceSettings>(config.GetSection("Resource"));

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
            services.AddTransient<ISaveFileService, SaveFileService>();
            services.AddTransient<ServerBrowserViewModel>();

            services.AddTransient<MainWindow>();
        }

        private static string GetLauncherSettings()
        {
            return Path.Combine(Constants.LocalDir, "launchersettings.json");
        }

        private static IConfigurationRoot BuildConfiguration()
        {
            const string defaultAppsettings = "appsettings.json";

            Assembly host = Assembly.GetEntryAssembly()!;
            string fullFileName = $"{host.GetName().Name}.{defaultAppsettings}";
            using Stream input = host.GetManifestResourceStream(fullFileName)!;

            IConfigurationBuilder builder = new ConfigurationBuilder()
                .AddJsonStream(input);

            // NOTE: using two builders because we use the embedded resource as a stream
            IConfigurationRoot root = builder.Build();

            IConfigurationBuilder coolerBuilder = new ConfigurationBuilder()
                .AddConfiguration(root);

            string customSettigns = GetLauncherSettings();
            if (!File.Exists(customSettigns))
            {
                H2MLauncherSettings defaultH2MLauncherSettings = root.GetRequiredSection("H2MLauncher").Get<H2MLauncherSettings>()!;
                JsonFileHelper.AddOrUpdateSection(customSettigns, "H2MLauncher", defaultH2MLauncherSettings);
            }

            coolerBuilder
                .AddJsonFile(customSettigns)
                .AddJsonFile("appsettings.local.json", optional: true);

            return coolerBuilder.Build();
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
