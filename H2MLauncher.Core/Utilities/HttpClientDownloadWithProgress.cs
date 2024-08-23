using System.Buffers;

namespace H2MLauncher.Core.Utilities
{
    internal class HttpClientDownloadWithProgress(HttpClient httpClient, string destinationFilePath, Func<HttpRequestMessage> requestMessageBuilder)
    {
        private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        private readonly string _destinationFilePath = destinationFilePath ?? throw new ArgumentNullException(nameof(destinationFilePath));
        private readonly Func<HttpRequestMessage> _requestMessageBuilder = requestMessageBuilder ?? throw new ArgumentNullException(nameof(requestMessageBuilder));
        private readonly int _bufferSize = 8192;

        public event DownloadProgressHandler? ProgressChanged;

        public async Task StartDownloadAsync(CancellationToken cancellationToken)
        {
            using HttpRequestMessage requestMessage = _requestMessageBuilder.Invoke();
            using HttpResponseMessage response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            await DownloadAsync(response, cancellationToken);
        }

        private async Task DownloadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            response.EnsureSuccessStatusCode();

            long? totalBytes = response.Content.Headers.ContentLength;

            using Stream contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await ProcessContentStream(totalBytes, contentStream, cancellationToken);
        }

        private async Task ProcessContentStream(long? totalDownloadSize, Stream contentStream, CancellationToken cancellationToken)
        {
            long totalBytesRead = 0L;
            long readCount = 0L;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(_bufferSize);
            bool isMoreToRead = true;

            using (FileStream fileStream = new(_destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None, _bufferSize, true))
            {
                do
                {
                    int bytesRead = await contentStream.ReadAsync(buffer, cancellationToken);
                    if (bytesRead == 0)
                    {
                        isMoreToRead = false;
                        ReportProgress(totalDownloadSize, totalBytesRead);
                        continue;
                    }

                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);

                    totalBytesRead += bytesRead;
                    readCount += 1;

                    if (readCount % 100 == 0)
                        ReportProgress(totalDownloadSize, totalBytesRead);
                }
                while (isMoreToRead);
            }

            ArrayPool<byte>.Shared.Return(buffer);
        }

        private void ReportProgress(long? totalDownloadSize, long totalBytesRead)
        {
            double? progressPercentage = null;
            if (totalDownloadSize.HasValue)
                progressPercentage = Math.Round((double)totalBytesRead / totalDownloadSize.Value * 100, 2);

            ProgressChanged?.Invoke(totalDownloadSize, totalBytesRead, progressPercentage);
        }
    }
}
