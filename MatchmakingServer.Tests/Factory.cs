using System.Data.Common;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;

using H2MLauncher.Core.Social;

using MatchmakingServer.Authentication.JWT;
using MatchmakingServer.Database;
using MatchmakingServer.SignalR;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;

using NSubstitute;

namespace MatchmakingServer.Tests;

public class Factory : WebApplicationFactory<Program>
{
    public IHubContext<SocialHub, ISocialClient> SocialHubContextMock { get; }

    public Factory()
    {
        SocialHubContextMock = Substitute.For<IHubContext<SocialHub, ISocialClient>>();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Database

            var dbContextOptionsDescriptor = services.Single(
                d => d.ServiceType ==
                    typeof(IDbContextOptionsConfiguration<DatabaseContext>));

            services.Remove(dbContextOptionsDescriptor);

            // Create open SqliteConnection so EF won't automatically close it.
            services.AddSingleton<DbConnection>(container =>
            {
                var connection = new SqliteConnection("DataSource=:memory:");
                connection.Open();

                return connection;
            });
            
            services.AddDbContext<DatabaseContext>((container, options) =>
            {
                var connection = container.GetRequiredService<DbConnection>();
                options.UseSqlite(connection);

                // see https://github.com/dotnet/efcore/issues/34431
                options.ConfigureWarnings(warnings => warnings.Log(RelationalEventId.PendingModelChangesWarning));
            }, optionsLifetime: ServiceLifetime.Singleton);

            services.AddSingleton(SocialHubContextMock);


            // Authentication
            services.Configure<JwtSettings>(options =>
            {
                options.Issuer = "Test_Issuer";
                options.Audience = "Test_Issuer";
                options.Secret = Guid.NewGuid().ToString();
            });
        });

        builder.UseEnvironment("Development");
    }

    public HttpClient CreateAuthenticatedClient(Guid userId, string userName)
    {
        HttpClient client = CreateClient();

        TokenService tokenService = Services.GetRequiredService<TokenService>();
        TokenResponse token = tokenService.CreateToken([
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, userName),
        ]);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        return client;
    }
}
