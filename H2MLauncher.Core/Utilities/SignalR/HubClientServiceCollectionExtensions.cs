using H2MLauncher.Core.OnlineServices;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace H2MLauncher.Core.Utilities.SignalR;

public static class HubClientServiceCollectionExtensions
{
    /// <summary>
    /// Adds the <see cref="HubClient{THub}"/> of type <typeparamref name="T"/> as a singleton service. <br/>
    /// Uses the <paramref name="connectionFactory"/> to obtain a <see cref="HubConnection"/> for the client.
    /// </summary>
    public static IServiceCollection AddHubClient<T, THub>(this IServiceCollection services,
        Func<IServiceProvider, OnlineServiceManager, HubConnection>? connectionFactory = null)
        where T : HubClient<THub>
        where THub : class
    {
        services.AddSingleton(sp =>
        {
            HubConnection connection;
            if (connectionFactory is not null)
            {
                OnlineServiceManager manager = sp.GetRequiredService<OnlineServiceManager>();
                connection = connectionFactory(sp, manager);
            }
            else
            {
                connection = sp.GetRequiredService<HubConnection>();
            }

            T service = ActivatorUtilities.CreateInstance<T>(sp, connection);

            return service;
        });

        return services;
    }
}
