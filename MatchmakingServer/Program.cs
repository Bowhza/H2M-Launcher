using H2MLauncher.Core.Interfaces;
using H2MLauncher.Core.Models;
using H2MLauncher.Core.Services;

using MatchmakingServer;
using MatchmakingServer.SignalR;

var builder = WebApplication.CreateBuilder();


// Add services to the container.

builder.Services.AddLogging();
builder.Services.AddScoped<IIW4MAdminService, IW4MAdminService>();
builder.Services.AddHttpClient<IIW4MAdminService, IW4MAdminService>();
builder.Services.AddSingleton<GameServerCommunicationService<IServerConnectionDetails>>();

builder.Services.AddSingleton<QueueingService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSignalR();

WebApplication app = builder.Build();

IIW4MAdminService iw4mAdminService = app.Services.GetRequiredService<IIW4MAdminService>();

const string serverInstance = "http://51.161.192.200:2624";
const string serverId = "5116119220027020";

IW4MServerDetails serverDetails = await iw4mAdminService.GetServerDetailsAsync(serverInstance, serverId, CancellationToken.None);
PrintServerDetails(serverDetails);


IW4MServerStatus serverStatus = await iw4mAdminService.GetServerStatusAsync(serverInstance, serverId, CancellationToken.None);
Console.WriteLine($"{serverStatus.IsOnline}");
Console.WriteLine($"{serverStatus.Game}");
Console.WriteLine($"{serverStatus.CurrentPlayers}");
serverStatus.Players.ForEach((player) =>
{
    Console.WriteLine($"{player.ClientNumber} - {player.Name} - {player.Ping}ms - {player.State} - {player.ConnectionTime}");
});

IEnumerable<IW4MServerDetails> servers = await iw4mAdminService.GetServerListAsync(serverInstance, CancellationToken.None);
foreach (IW4MServerDetails server in servers)
    PrintServerDetails(server);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();

app.MapHub<QueueingHub>("/Queue");

app.Run();

static void PrintServerDetails(IW4MServerDetails serverDetails)
{
    Console.WriteLine($"{serverDetails.ServerName}");
    Console.WriteLine($"{serverDetails.Map.Alias}");
    Console.WriteLine($"{serverDetails.GameType.Name}");
    Console.WriteLine($"{serverDetails.ClientNum}/{serverDetails.MaxClients}");
}