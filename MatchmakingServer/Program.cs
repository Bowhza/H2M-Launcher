using H2MLauncher.Core.Interfaces;
using H2MLauncher.Core.Services;
using H2MLauncher.Core.Utilities;

using MatchmakingServer;
using MatchmakingServer.SignalR;

using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder();


// Add services to the container.

builder.Services.AddLogging();

builder.Services.Configure<Settings>(builder.Configuration.GetSection("Settings"));
builder.Services.Configure<ServerSettings>(builder.Configuration.GetSection("ServerSettings"));

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

builder.Services.AddSingleton<GameServerCommunicationService<IServerConnectionDetails>>();
builder.Services.AddSingleton<IEndpointResolver, CachedIpv6EndpointResolver>();

builder.Services.AddSingleton<ServerInstanceCache>();

builder.Services.AddSingleton<QueueingService>();
builder.Services.AddMemoryCache();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddControllers();

builder.Services.AddSignalR();

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

//app.UseHttpsRedirection();

app.MapHub<QueueingHub>("/Queue");

app.Run();