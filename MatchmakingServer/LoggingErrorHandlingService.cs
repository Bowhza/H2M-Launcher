using H2MLauncher.Core.Services;

internal class LoggingErrorHandlingService : IErrorHandlingService
{
    private readonly ILogger<LoggingErrorHandlingService> _logger;

    public LoggingErrorHandlingService(ILogger<LoggingErrorHandlingService> logger)
    {
        _logger = logger;
    }

    public void HandleError(string info)
    {
        _logger.LogInformation("Error: {info}", info);
    }

    public void HandleException(Exception ex, string info = "")
    {
        _logger.LogError(ex, "{info}", info);
    }
}
