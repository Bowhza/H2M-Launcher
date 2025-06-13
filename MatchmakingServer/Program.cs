using System.Security.Claims;
using System.Text.Json.Serialization;

using FluentValidation;

using Flurl;

using H2MLauncher.Core.IW4MAdmin;
using H2MLauncher.Core.Networking;
using H2MLauncher.Core.Networking.GameServer;
using H2MLauncher.Core.Networking.GameServer.HMW;
using H2MLauncher.Core.Services;
using H2MLauncher.Core.Utilities;

using MatchmakingServer;
using MatchmakingServer.Api;
using MatchmakingServer.Authorization;
using MatchmakingServer.Database;
using MatchmakingServer.Matchmaking;
using MatchmakingServer.Parties;
using MatchmakingServer.Playlists;
using MatchmakingServer.Queueing;
using MatchmakingServer.SignalR;
using MatchmakingServer.Social;

using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Serilog;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.

Environment.SetEnvironmentVariable("BASEDIR", AppDomain.CurrentDomain.BaseDirectory);

builder.Configuration.AddJsonFile("appsettings.local.json", optional: true);

builder.Host.UseSerilog((context, logger) => logger.ReadFrom.Configuration(context.Configuration));
builder.Services.AddHealthChecks();

builder.Services.Configure<Settings>(builder.Configuration.GetSection("Settings"));
builder.Services.Configure<ServerSettings>(builder.Configuration.GetSection("ServerSettings"));
builder.Services.Configure<QueueingSettings>(builder.Configuration.GetSection("QueueingSettings"));

builder.Services.AddScoped<IIW4MAdminService, IW4MAdminService>();
builder.Services.AddHttpClient<IIW4MAdminService, IW4MAdminService>()
    .ConfigureAdditionalHttpMessageHandlers(
        (handlers, p) => handlers.Add(new TimeoutHandler()
        {
            DefaultTimeout = TimeSpan.FromSeconds(5)
        }));

builder.Services.AddScoped<IIW4MAdminMasterService, IW4MAdminMasterService>();
builder.Services.AddHttpClient<IIW4MAdminMasterService, IW4MAdminMasterService>()
    .ConfigureHttpClient((sp, client) =>
    {
        var settings = sp.GetRequiredService<IOptions<Settings>>();
        if (!Uri.TryCreate(settings.Value.IW4MAdminMasterApiUrl, UriKind.RelativeOrAbsolute, out var baseUri))
        {
            throw new Exception("Invalid master api url in settings.");
        }

        client.BaseAddress = baseUri;
    });

builder.Services.AddHttpClient<HMWMasterService>()
    .ConfigureHttpClient((sp, client) =>
    {
        // get the current value from the options monitor cache
        Settings launcherSettings = sp.GetRequiredService<IOptionsMonitor<Settings>>().CurrentValue;

        // make sure base address is set correctly without trailing slash
        client.BaseAddress = Url.Parse(launcherSettings.HMWMasterServerUrl).ToUri();
    });

builder.Services.AddTransient<IErrorHandlingService, LoggingErrorHandlingService>();
builder.Services.AddSingleton<IMasterServerService, HMWMasterService>();

builder.Services.AddTransient<UdpGameServerCommunication>();
builder.Services.AddSingleton<GameServerCommunicationService<GameServer>>();
builder.Services.AddTransient<IGameServerInfoService<GameServer>, HttpGameServerInfoService<GameServer>>();

builder.Services.AddKeyedSingleton<IGameServerStatusService<GameServer>>("UDP", (sp, key) =>
    sp.GetRequiredService<GameServerCommunicationService<GameServer>>());

builder.Services.AddKeyedSingleton<IGameServerCommunicationService<GameServer>>("UDP", (sp, key) => 
    sp.GetRequiredService<GameServerCommunicationService<GameServer>>());

builder.Services.AddSingleton<IEndpointResolver, CachedIpv6EndpointResolver>();

builder.Services.AddSingleton<ServerInstanceCache>();

builder.Services.AddSingleton<ServerStore>();
builder.Services.AddSingleton<PlayerStore>();
builder.Services.AddSingleton<PlaylistStore>();
builder.Services.AddSingleton<QueueingService>();
builder.Services.AddSingleton<Matchmaker>();
builder.Services.AddSingleton<MatchmakingService>();
builder.Services.AddHostedService(p => p.GetRequiredService<MatchmakingService>());
builder.Services.AddHostedService<PlaylistsSeedingService>();
builder.Services.AddSingleton<PartyService>();
builder.Services.AddSingleton<PartyMatchmakingService>();
builder.Services.AddSingleton<SocialService>();
builder.Services.AddSingleton<GameServerService>();
builder.Services.AddSingleton<IPlayerServerTrackingService, PlayerServerTrackingService>();
builder.Services.AddMemoryCache();

// Social
builder.Services.AddTransient<UserManager>();
builder.Services.AddTransient<FriendshipsService>();

builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Database
builder.Services.AddDbContextPool<DatabaseContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.AddSwagger();
builder.AddAuthentication();


// Authorization
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.CanReadFriends, policy =>
    {
        policy.AddRequirements(new ReadFriendsRequirement(ClaimTypes.NameIdentifier));
    })
    .AddPolicy(Policies.AccessFriendRequests, policy =>
    {
        policy.AddRequirements(new IsOwnerRequirement(ClaimTypes.NameIdentifier));
    })
    .AddPolicy(Policies.CanRemoveFriend, policy =>
    {
        policy.AddRequirements(new IsOwnerRequirement(ClaimTypes.NameIdentifier));
    });

// Register custom authorization handlers
builder.Services.AddScoped<IAuthorizationHandler, IsOwnerHandler>();
builder.Services.AddScoped<IAuthorizationHandler, ReadFriendsHandler>();

builder.Services.AddHttpContextAccessor();


builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        // serialize enums as strings in api responses
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddSignalR();

WebApplication app = builder.Build();

// Ensure database is created and migrations are applied
using (var scope = app.Services.CreateScope())
{
    DatabaseContext db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRequestLogging();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.UseWebSockets();
app.MapHubs();
app.MapEndpoints();

app.Run();

public partial class Program { }