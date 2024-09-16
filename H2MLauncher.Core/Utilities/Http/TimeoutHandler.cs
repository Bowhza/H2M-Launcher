﻿namespace H2MLauncher.Core.Utilities;

public class TimeoutHandler : DelegatingHandler
{
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(100);

    protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using (var cts = GetCancellationTokenSource(request, cancellationToken))
        {
            try
            {
                return await base.SendAsync(request, cts?.Token ?? cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException();
            }
        }
    }

    private CancellationTokenSource? GetCancellationTokenSource(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var timeout = request.GetTimeout() ?? DefaultTimeout;
        if (timeout == Timeout.InfiniteTimeSpan)
        {
            // No need to create a CTS if there's no timeout
            return null;
        }
        else
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);
            return cts;
        }
    }
}
