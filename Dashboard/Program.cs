using Dashboard;
using Dashboard.Components;
using Dashboard.Database;
using Dashboard.DownloadCount;
using Dashboard.GitHub;

using Microsoft.EntityFrameworkCore;

using Refit;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Database
builder.Services.AddDbContextPool<DatabaseContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Adding HTTP client and background service
builder.Services.AddHttpClient();
builder.Services.AddRefitClient<IGitHubApiClient>()
    .ConfigureHttpClient(client =>
    {
        client.BaseAddress = new Uri("https://api.github.com/");

        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
    });

builder.Services.AddHostedService<GitHubFetcherService>();

builder.Services.AddSingleton<IEventBus, InMemoryEventBus>();
builder.Services.AddTransient<DownloadCountService>();


var app = builder.Build();

// Ensure database is created and migrations are applied
using (var scope = app.Services.CreateScope())
{
    DatabaseContext db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
