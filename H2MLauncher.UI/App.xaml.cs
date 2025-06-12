using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

using Flurl;

using H2MLauncher.Core;
using H2MLauncher.Core.Game;
using H2MLauncher.Core.Game.Memory;
using H2MLauncher.Core.IW4MAdmin;
using H2MLauncher.Core.Joining;
using H2MLauncher.Core.Matchmaking;
using H2MLauncher.Core.Models;
using H2MLauncher.Core.Networking;
using H2MLauncher.Core.Networking.GameServer;
using H2MLauncher.Core.Networking.GameServer.HMW;
using H2MLauncher.Core.OnlineServices;
using H2MLauncher.Core.OnlineServices.Authentication;
using H2MLauncher.Core.Party;
using H2MLauncher.Core.Services;
using H2MLauncher.Core.Settings;
using H2MLauncher.Core.Social;
using H2MLauncher.Core.Utilities;
using H2MLauncher.Core.Utilities.Http;
using H2MLauncher.Core.Utilities.SignalR;
using H2MLauncher.UI.Dialog;
using H2MLauncher.UI.Services;
using H2MLauncher.UI.ViewModels;

using MatchmakingServer.Core.Party;
using MatchmakingServer.Core.Social;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Nogic.WritableOptions;

using Refit;

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

            IConfigurationRoot config = BuildConfiguration();
            InitializeLogging(config);

            ServiceCollection serviceCollection = new();
            ConfigureServices(serviceCollection, config);

            ServiceProvider = serviceCollection.BuildServiceProvider();

            // save options on startup to update user file with new settings            
            ServiceProvider.GetRequiredService<IWritableOptions<H2MLauncherSettings>>().Update((settings) =>
            {
                // make sure a valid master server url is set                
                return settings with
                {
                    IW4MMasterServerUrl = string.IsNullOrEmpty(settings.IW4MMasterServerUrl)
                        ? _defaultSettings.IW4MMasterServerUrl
                        : settings.IW4MMasterServerUrl,

                    // always override with the default url because there is only one master (and no option to change in the UI)
                    HMWMasterServerUrl = _defaultSettings.HMWMasterServerUrl,
                };
            });

            // NOTE: this is really stupid but necessary to have the latest urls we just set above available
            config.Reload();

            MainWindow mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();

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

            services.AddSingleton(typeof(GameServerCommunicationService<>));

            services.AddKeyedSingleton<IGameServerInfoService<IServerConnectionDetails>>("UDP", (sp, key) =>
                sp.GetRequiredService<GameServerCommunicationService<IServerConnectionDetails>>());

            services.AddKeyedSingleton<IGameServerStatusService<IServerConnectionDetails>>("UDP", (sp, key) =>
                sp.GetRequiredService<GameServerCommunicationService<IServerConnectionDetails>>());

            services.AddKeyedSingleton<IGameServerCommunicationService<IServerConnectionDetails>>("UDP", (sp, key) =>
                sp.GetRequiredService<GameServerCommunicationService<IServerConnectionDetails>>());

            services.AddTransient<IGameServerInfoService<IServerConnectionDetails>, 
                TcpUdpDynamicGameServerInfoService<IServerConnectionDetails>>();

            services.AddSingleton<H2MCommunicationService>();
            services.AddSingleton<IEndpointResolver, CachedIpv6EndpointResolver>();
            services.AddSingleton<IGameDetectionService, H2MGameDetectionService>();
            services.AddSingleton<IGameCommunicationService, H2MGameMemoryCommunicationService>();
            services.AddSingleton<GameDirectoryService>();
            services.AddSingleton<IPlayerNameProvider, ConfigPlayerNameProvider>();
            services.AddSingleton<IGameConfigProvider, GameDirectoryService>(sp => sp.GetRequiredService<GameDirectoryService>());
            services.AddSingleton<IMapsProvider, InstalledMapsProvider>();
            services.AddMemoryCache();

            services.AddTransient<IClipBoardService, ClipBoardService>();
            services.AddTransient<ISaveFileService, SaveFileService>();
            services.AddSingleton<IServerJoinService, ServerJoinService>();
            services.AddTransient<ServerBrowserViewModel>();
            services.AddTransient<PartyViewModel>();
            services.AddTransient<FriendsViewModel>();
            services.AddTransient<FriendRequestsViewModel>();
            services.AddTransient<SocialOverviewViewModel>();
            services.AddSingleton<CustomizationManager>();
            services.AddTransient<CustomizationDialogViewModel>();

            // online services
            services.AddSingleton<OnlineServiceManager>();
            services.AddSingleton<IOnlineServices, OnlineServiceManager>(sp => sp.GetRequiredService<OnlineServiceManager>());
            services.AddSingleton<ClientContext>();

            // social
            services.AddRefitClient<IFriendshipApiClient>(sp =>
                new RefitSettings()
                {
                    AuthorizationHeaderValueGetter = async (req, ct) =>
                    {
                        ClientContext clientContext = sp.GetRequiredService<ClientContext>();
                        if (clientContext.IsAuthenticated)
                        {
                            return clientContext.AccessToken;
                        }

                        AuthenticationService authenticationService = sp.GetRequiredService<AuthenticationService>();
                        return await authenticationService.LoginAsync().ConfigureAwait(false) ?? "";
                    },
                    ContentSerializer = new SystemTextJsonContentSerializer(
                        new JsonSerializerOptions(JsonSerializerDefaults.Web)
                        {
                            Converters = { new JsonStringEnumConverter() }
                        }),
                    HttpMessageHandlerFactory = () =>
                        Core.Utilities.Http.HttpClientBuilderExtensions.CustomMessageHandlerFactory(sp)
                })
                .ConfigureMatchmakingClient();

            // authentication
            services.AddTransient<AuthenticationService>();
            services.AddHttpClient<AuthenticationService>()
                .ConfigureMatchmakingClient()
                .ConfigureCertificateValidationIgnore();

            services.AddSingleton(sp =>
            {
                var matchmakingSettings = sp.GetRequiredService<IOptions<MatchmakingSettings>>();
                var logger = sp.GetRequiredService<ILogger<RsaKeyManager>>();

                // Use random client id by not enabling persistence so a new key is generated in memory
                string? keyFilePath = matchmakingSettings.Value.UseRandomCliendId ? null : Constants.KeyFilePath;

                return new RsaKeyManager(keyFilePath, logger);
            });

            // server data / playlists
            services.AddTransient<CachedServerDataService>();
            services.AddTransient<IPlaylistService, CachedServerDataService>(sp => sp.GetRequiredService<CachedServerDataService>());
            services.AddHttpClient<CachedServerDataService>()
                .ConfigureMatchmakingClient()
                .ConfigureCertificateValidationIgnore();

            // hub clients
            services.AddHubClient<QueueingService, IMatchmakingHub>((sp, manager) => manager.QueueingHubConnection);
            services.AddHubClient<MatchmakingService, IMatchmakingHub>((sp, manager) => manager.QueueingHubConnection);
            services.AddHubClient<PartyClient, IPartyHub>((sp, manager) => manager.PartyHubConnection);
            services.AddHubClient<SocialClient, ISocialHub>((sp, manager) => manager.SocialHubConnection);


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
