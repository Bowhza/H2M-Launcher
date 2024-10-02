namespace MatchmakingServer.Api
{
    public static class RouteHandlerBuilderExtensions
    {
        public static RouteHandlerBuilder WithValidation<TRequest>(this RouteHandlerBuilder builder)
        {
            return builder
                .AddEndpointFilter<ValidationFilter<TRequest>>()
                .ProducesValidationProblem();
        }
    }
}
