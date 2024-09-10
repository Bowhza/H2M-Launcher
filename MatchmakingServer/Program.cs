using System.Text.Json.Serialization;

using H2MLauncher.Core.Interfaces;
using H2MLauncher.Core.Services;
using H2MLauncher.Core.Utilities;

using MatchmakingServer;
using MatchmakingServer.Authentication;
using MatchmakingServer.SignalR;

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

builder.Services.AddSingleton<GameServerCommunicationService<GameServer>>();
builder.Services.AddSingleton<IEndpointResolver, CachedIpv6EndpointResolver>();

builder.Services.AddSingleton<ServerInstanceCache>();

builder.Services.AddSingleton<ServerStore>();
builder.Services.AddSingleton<QueueingService>();
builder.Services.AddSingleton<MatchmakingServer.MatchmakingService>();
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

app.UseSerilogRequestLogging();

app.UseAuthentication();
app.MapControllers();
app.MapHealthChecks("/health");

//app.UseHttpsRedirection();

app.MapHub<QueueingHub>("/Queue");

app.Services.GetRequiredService <MatchmakingServer.MatchmakingService>();

app.Run();