using System.IO;
using System.Reflection;
using System.Windows;

using Flurl;

using H2MLauncher.Core;
using H2MLauncher.Core.Models;
using H2MLauncher.Core.Services;
using H2MLauncher.Core.Settings;
using H2MLauncher.UI.Dialog;
using H2MLauncher.UI.ViewModels;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Nogic.WritableOptions;

using Serilog;
using H2MLauncher.Core.Utilities;
using H2MLauncher.Core.Networking;
using H2MLauncher.Core.IW4MAdmin.Models;
using H2MLauncher.Core.IW4MAdmin;
using H2MLauncher.Core.Game;
using H2MLauncher.UI.Services;
using H2MLauncher.Core.Game.Memory;
using H2MLauncher.Core.Matchmaking;
using H2MLauncher.Core.Networking.GameServer.HMW;
using H2MLauncher.Core.Joining;
using H2MLauncher.Core.Party;
using MatchmakingServer.Core.Party;
using H2MLauncher.Core.OnlineServices;
using H2MLauncher.Core.Utilities.Http;
using H2MLauncher.Core.Utilities.SignalR;
using H2MLauncher.Core.OnlineServices.Authentication;
using H2MLauncher.Core.Networking.GameServer;

namespace H2MLauncher.UI
{
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; } = null!;
        private H2MLauncherSettings _defaultSettings = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            SetupExceptionHandling();

            IConfigurationRoot config = BuildConfiguration();
            InitializeLogging(config);

            ServiceCollection serviceCollection = new();
            ConfigureServices(serviceCollection, config);

            ServiceProvider = serviceCollection.BuildServiceProvider();

            MainWindow mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();

            // save options on startup to update user file with new settings            
            ServiceProvider.GetRequiredService<IWritableOptions<H2MLauncherSettings>>().Update((settings) =>
            {
                // make sure a valid master server url is set                
                return settings with
                {
                    IW4MMasterServerUrl = string.IsNullOrEmpty(settings.IW4MMasterServerUrl)
                        ? _defaultSettings.IW4MMasterServerUrl
                        : settings.IW4MMasterServerUrl,
                    HMWMasterServerUrl = string.IsNullOrEmpty(settings.HMWMasterServerUrl)
                        ? _defaultSettings.HMWMasterServerUrl
                        : settings.HMWMasterServerUrl,
                };
            });

            base.OnStartup(e);
        }

        private static void InitializeLogging(IConfiguration configuration)
        {
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();

            Log.Information("{applicationName} started on version {version}.",
                Assembly.GetExecutingAssembly().GetName(),
                LauncherService.CurrentVersion);
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

            services.AddTransient<LauncherService>();
            services.AddHttpClient<LauncherService>();

            services.AddTransient<IIW4MAdminService, IW4MAdminService>();
            services.AddHttpClient<IIW4MAdminService, IW4MAdminService>();

            services.AddTransient<IIW4MAdminMasterService, IW4MAdminMasterService>();
            services.AddHttpClient<IIW4MAdminMasterService, IW4MAdminMasterService>()
              .ConfigureHttpClient((sp, client) =>
              {
                  // get the current value from the options monitor cache
                  H2MLauncherSettings launcherSettings = sp.GetRequiredService<IOptionsMonitor<H2MLauncherSettings>>().CurrentValue;

                  // make sure base address is set correctly without trailing slash
                  client.BaseAddress = Url.Parse(launcherSettings.IW4MMasterServerUrl).ToUri();
              });

            services.AddKeyedSingleton<IMasterServerService, H2MServersService>("H2M");
            services.AddKeyedSingleton<IMasterServerService, HMWMasterService>("HMW");
            services.AddHttpClient<HMWMasterService>()
                .ConfigureHttpClient((sp, client) =>
                {
                    // get the current value from the options monitor cache
                    H2MLauncherSettings launcherSettings = sp.GetRequiredService<IOptionsMonitor<H2MLauncherSettings>>().CurrentValue;

                    // make sure base address is set correctly without trailing slash
                    client.BaseAddress = Url.Parse(launcherSettings.HMWMasterServerUrl).ToUri();
                });

            // game server communication
            services.AddTransient<UdpGameServerCommunication>();
            services.AddKeyedTransient(typeof(IGameServerInfoService<>), "TCP", typeof(HttpGameServerInfoService<>));
            services.AddKeyedTransient(typeof(IGameServerInfoService<>), "UDP", typeof(GameServerCommunicationService<>));
            services.AddTransient(typeof(IGameServerInfoService<>), typeof(TcpUdpDynamicGameServerInfoService<>));

            services.AddSingleton<H2MCommunicationService>();
            services.AddSingleton<IEndpointResolver, CachedIpv6EndpointResolver>();
            services.AddSingleton<IGameDetectionService, H2MGameDetectionService>();
            services.AddSingleton<IGameCommunicationService, H2MGameMemoryCommunicationService>();
            services.AddSingleton<GameDirectoryService>();
            services.AddSingleton<IPlayerNameProvider, ConfigPlayerNameProvider>();
            services.AddSingleton<IMapsProvider, InstalledMapsProvider>();
            services.AddMemoryCache();

            services.AddTransient<IClipBoardService, ClipBoardService>();
            services.AddTransient<ISaveFileService, SaveFileService>();
            services.AddSingleton<IServerJoinService, ServerJoinService>();
            services.AddTransient<ServerBrowserViewModel>();
            services.AddTransient<PartyViewModel>();

            // online services
            services.AddSingleton<OnlineServiceManager>();
            services.AddSingleton<IOnlineServices, OnlineServiceManager>(sp => sp.GetRequiredService<OnlineServiceManager>());
            services.AddSingleton<ClientContext>();

            // authentication
            services.AddTransient<AuthenticationService>();
            services.AddHttpClient<AuthenticationService>()
                .ConfigureMatchmakingClient();

            // server data / playlists
            services.AddTransient<CachedServerDataService>();
            services.AddTransient<IPlaylistService, CachedServerDataService>(sp => sp.GetRequiredService<CachedServerDataService>());
            services.AddHttpClient<CachedServerDataService>()
                .ConfigureMatchmakingClient();

            // hub clients
            services.AddHubClient<QueueingService, IMatchmakingHub>((sp, manager) => manager.QueueingHubConnection);
            services.AddHubClient<MatchmakingService, IMatchmakingHub>((sp, manager) => manager.QueueingHubConnection);
            services.AddHubClient<PartyClient, IPartyHub>((sp, manager) => manager.PartyHubConnection);


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
                Console.WriteLine("No default appsettings.");
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
#if DEBUG   // In debug mode do not custom-handle the exception, let Visual Studio handle it

                    e.Handled = false;
#else

                    e.Handled = MainWindow?.IsLoaded ?? false;
#endif
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
