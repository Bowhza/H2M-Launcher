namespace H2MLauncher.Core.Utilities;

public static class HttpRequestExtensions
{
    private const string TimeoutOptionKey = "RequestTimeout";

    public static void SetTimeout(this HttpRequestMessage request, TimeSpan? timeout)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        request.Options.Set(new(TimeoutOptionKey), timeout);
    }

    public static TimeSpan? GetTimeout(this HttpRequestMessage request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (request.Options.TryGetValue(new(TimeoutOptionKey), out TimeSpan? value) && value is TimeSpan timeout)
        {
            return timeout;
        }

        return null;
    }
}
