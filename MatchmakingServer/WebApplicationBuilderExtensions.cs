using MatchmakingServer.Authentication;
using MatchmakingServer.Authentication.JWT;
using MatchmakingServer.Authentication.Passwordless;
using MatchmakingServer.Authentication.Player;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace MatchmakingServer;

public static class WebApplicationBuilderExtensions
{
    public static void AddAuthentication(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<JwtSettings>()
                        .BindConfiguration("JWT")
                        .ValidateDataAnnotations();

        builder.Services.AddTransient<TokenService>();
        builder.Services.AddTransient<PasswordlessAuthenticationService>();

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)

            // api key
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
            })

            // player client (query params)
            .AddScheme<AuthenticationSchemeOptions, ClientAuthenticationHandler>("client", null)

            // JWT (default)
            .AddJwtBearer();

        // Configure JWT bearer
        builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtSettings>>((options, jwtSettings) =>
            {
                options.SaveToken = true;
                options.TokenValidationParameters = new()
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings.Value.Issuer,
                    ValidateAudience = false,
                    ValidAudience = jwtSettings.Value.Audience,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        System.Text.Encoding.UTF8.GetBytes(jwtSettings.Value.Secret)
                    )
                };
            });
    }

    public static void AddSwagger(this WebApplicationBuilder builder)
    {
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
    }
}
