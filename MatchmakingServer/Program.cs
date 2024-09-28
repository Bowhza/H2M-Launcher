using System.Security.Claims;
using System.Text.Json.Serialization;

using Flurl;

using H2MLauncher.Core.IW4MAdmin;
using H2MLauncher.Core.Networking;
using H2MLauncher.Core.Networking.GameServer;
using H2MLauncher.Core.Networking.GameServer.HMW;
using H2MLauncher.Core.Services;
using H2MLauncher.Core.Utilities;

using MatchmakingServer;
using MatchmakingServer.Authentication;
using MatchmakingServer.Authentication.Player;
using MatchmakingServer.Parties;
using MatchmakingServer.Queueing;
using MatchmakingServer.SignalR;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

using Serilog;

WebApplicationBuilder builder = WebApplication.CreateBuilder();

// Add services to the container.

Environment.SetEnvironmentVariable("BASEDIR", AppDomain.CurrentDomain.BaseDirectory);

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
builder.Services.AddKeyedSingleton<IMasterServerService, HMWMasterService>("HMW");

builder.Services.AddTransient<UdpGameServerCommunication>();
builder.Services.AddSingleton<GameServerCommunicationService<GameServer>>();
builder.Services.AddKeyedSingleton<IGameServerInfoService<GameServer>, GameServerCommunicationService<GameServer>>("UDP", (sp, _) => 
    sp.GetRequiredService<GameServerCommunicationService<GameServer>>());
builder.Services.AddKeyedSingleton<IGameServerInfoService<GameServer>, HttpGameServerInfoService<GameServer>>("TCP");
builder.Services.AddTransient<IGameServerInfoService<GameServer>, TcpUdpDynamicGameServerInfoService<GameServer>>();
builder.Services.AddSingleton<IEndpointResolver, CachedIpv6EndpointResolver>();

builder.Services.AddSingleton<ServerInstanceCache>();

builder.Services.AddSingleton<ServerStore>();
builder.Services.AddSingleton<PlayerStore>();
builder.Services.AddSingleton<QueueingService>();
builder.Services.AddSingleton<Matchmaker>();
builder.Services.AddSingleton<MatchmakingService>();
builder.Services.AddHostedService(p => p.GetRequiredService<MatchmakingService>());
builder.Services.AddSingleton<PartyService>();
builder.Services.AddSingleton<PartyMatchmakingService>();
builder.Services.AddMemoryCache();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "MatchmakingServer", Version = "v1" });

    OpenApiSecurityScheme apiKeyScheme = new()
    {
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = ApiKeyDefaults.AuthenticationScheme,
        },
        In = ParameterLocation.Header,
        Name = ApiKeyDefaults.RequestHeaderKey,
        Type = SecuritySchemeType.ApiKey,
    };

    c.AddSecurityDefinition(ApiKeyDefaults.AuthenticationScheme, apiKeyScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement() {
      {
          apiKeyScheme, []
      }
    });
});

builder.Services.AddAuthentication(ApiKeyDefaults.AuthenticationScheme)
                .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyDefaults.AuthenticationScheme, (options) =>
                {
                    options.ApiKey = builder.Configuration.GetValue<string>("ApiKey");
                    options.ForwardDefaultSelector = context =>
                    {
                        Endpoint? endpoint = context.GetEndpoint();

                        // Only forward the authentication if the endpoint has the [Authorize] attribute
                        bool requiresAuth = endpoint?.Metadata?.GetMetadata<AuthorizeAttribute>() is not null;

                        return requiresAuth ? ApiKeyDefaults.AuthenticationScheme : null;
                    };
                });

builder.Services.AddAuthentication(BearerTokenDefaults.AuthenticationScheme)
        .AddScheme<AuthenticationSchemeOptions, ClientAuthenticationHandler>("client", null)
        .AddBearerToken();

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        // serialize enums as strings in api responses
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddSignalR();

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms ({ClientAppName}/{ClientAppVersion})";
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        if (httpContext.Request.Headers.TryGetValue("X-App-Name", out var appNameValues) && appNameValues.Count != 0)
        {
            diagnosticContext.Set("ClientAppName", appNameValues.FirstOrDefault());
        }
        else
        {
            diagnosticContext.Set("ClientAppName", "Unknown");
        }

        if (httpContext.Request.Headers.TryGetValue("X-App-Version", out var appVersionValues) && appVersionValues.Count != 0)
        {
            diagnosticContext.Set("ClientAppVersion", appVersionValues.FirstOrDefault());
        }
        else
        {
            diagnosticContext.Set("ClientAppVersion", "?");
        }
    };
});

app.UseAuthentication();
app.MapControllers();
app.MapHealthChecks("/health");

//app.UseHttpsRedirection();

app.MapHub<QueueingHub>("/Queue");
app.MapHub<PartyHub>("/Party");

app.MapGet("/login", (string uid, string playerName) =>
{
    var claimsPrincipal = new ClaimsPrincipal(
      new ClaimsIdentity(
        [new Claim(ClaimTypes.Name, playerName),
         new Claim(ClaimTypes.NameIdentifier, uid)],
        BearerTokenDefaults.AuthenticationScheme
      )
    );

    return Results.SignIn(claimsPrincipal);
});

app.Run();