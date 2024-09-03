using System.IO;
using System.Reflection;
using System.Windows;

using Flurl;

using H2MLauncher.Core;
using H2MLauncher.Core.Interfaces;
using H2MLauncher.Core.Models;
using H2MLauncher.Core.Services;
using H2MLauncher.Core.Settings;
using H2MLauncher.UI.Dialog;
using H2MLauncher.UI.ViewModels;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Nogic.WritableOptions;

using Serilog;

namespace H2MLauncher.UI
{
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; } = null!;
        private H2MLauncherSettings _defaultSettings = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            SetupExceptionHandling();
            InitializeLogging();
            IConfigurationRoot config = BuildConfiguration();

            ServiceCollection serviceCollection = new();
            ConfigureServices(serviceCollection, config);

            ServiceProvider = serviceCollection.BuildServiceProvider();

            MainWindow mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();

            // save options on startup to update user file with new settings            
            ServiceProvider.GetRequiredService<IWritableOptions<H2MLauncherSettings>>().Update((settings) =>
            {
                // make sure a valid master server url is set
                if (string.IsNullOrEmpty(settings.IW4MMasterServerUrl))
                {
                    return settings with
                    {
                        IW4MMasterServerUrl = _defaultSettings.IW4MMasterServerUrl
                    };
                }

                return settings;
            });

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
                H2MLauncherService.CurrentVersion);
        }

        private void ConfigureServices(IServiceCollection services, IConfigurationRoot config)
        {
            services.AddSingleton<IConfiguration>(config);

            services.ConfigureWritableWithExplicitPath<H2MLauncherSettings>(
                section: config.GetSection(Constants.LauncherSettingsSection),
                directoryPath: Path.GetDirectoryName(Constants.LauncherSettingsFilePath) ?? Constants.LocalDir,
                file: Constants.LauncherSettingsFileName);

            services.Configure<ResourceSettings>(config.GetSection(Constants.ResourceSection));
            services.Configure<MatchmakingSettings>(config.GetSection(Constants.MatchmakingSection));

            services.AddKeyedSingleton(Constants.DefaultSettingsKey, _defaultSettings);

            services.AddLogging(builder => builder.AddSerilog());

            services.AddSingleton<DialogService>();
            services.AddTransient<IErrorHandlingService, ErrorHandlingService>();

            services.AddTransient<H2MLauncherService>();
            services.AddHttpClient<H2MLauncherService>();

            services.AddTransient<IIW4MAdminService, IW4MAdminService>();
            services.AddHttpClient<IIW4MAdminService, IW4MAdminService>();
    
            services.AddTransient<IIW4MAdminMasterService, IW4MAdminMasterService>();
            services.AddHttpClient<IIW4MAdminMasterService, IW4MAdminMasterService>()
              .ConfigureHttpClient((sp, client) =>
              {
                  // get the current value from the options monitor cache
                  H2MLauncherSettings launcherSettings = sp.GetRequiredService<IOptionsMonitor<H2MLauncherSettings>>().CurrentValue;

                  // make sure base address is set correctly without trailing slash
                  client.BaseAddress = Url.Parse(launcherSettings.IW4MMasterServerUrl).RemovePathSegment().ToUri();
              });

            services.AddSingleton<IH2MServersService, H2MServersService>();

            services.AddSingleton<H2MCommunicationService>();
            services.AddTransient<GameServerCommunicationService<IW4MServer>>();
            services.AddSingleton<IEndpointResolver, CachedIpv6EndpointResolver>();
            services.AddSingleton<IGameDetectionService, H2MGameDetectionService>();
            services.AddSingleton<IGameCommunicationService, H2MGameMemoryCommunicationService>();
            services.AddSingleton<GameDirectoryService>();

            services.AddTransient<IClipBoardService, ClipBoardService>();
            services.AddTransient<ISaveFileService, SaveFileService>();
            services.AddTransient<ServerBrowserViewModel>();

            services.AddTransient<MatchmakingService>();

            services.AddTransient<MainWindow>();
        }

        private IConfigurationRoot BuildConfiguration()
        {
            const string defaultAppsettings = "appsettings.json";

            Assembly host = Assembly.GetEntryAssembly()!;
            string fullFileName = $"{host.GetName().Name}.{defaultAppsettings}";
            using Stream input = host.GetManifestResourceStream(fullFileName)!;

            IConfigurationBuilder builder = new ConfigurationBuilder()
                .AddJsonStream(input);

            // NOTE: using two builders because we use the embedded resource as a stream
            IConfigurationRoot defaultConfiguration = builder.Build();

            IConfigurationBuilder coolerBuilder = new ConfigurationBuilder()
                .AddConfiguration(defaultConfiguration);

            H2MLauncherSettings? defaultH2MLauncherSettings = defaultConfiguration.GetRequiredSection(Constants.LauncherSettingsSection)
                                                                                  .Get<H2MLauncherSettings>();
            if (defaultH2MLauncherSettings is null)
            {
                // should never happen, except the appsettings.json are not included properly.
                Log.Fatal("No default appsettings.");
                Shutdown(-1);
                return defaultConfiguration;
            }

            _defaultSettings = defaultH2MLauncherSettings;

            coolerBuilder
                .AddJsonFile("appsettings.local.json", optional: true)
                .AddJsonFile(Constants.LauncherSettingsFilePath, optional: true, reloadOnChange: true);

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
