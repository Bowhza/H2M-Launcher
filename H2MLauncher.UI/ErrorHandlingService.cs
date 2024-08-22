using H2MLauncher.Core.Services;
using H2MLauncher.UI.Dialog;

using Microsoft.Extensions.Logging;

namespace H2MLauncher.UI
{
    public class ErrorHandlingService(DialogService dialogService, ILogger<ErrorHandlingService> logger) : IErrorHandlingService
    {
        private readonly DialogService _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        private readonly ILogger<ErrorHandlingService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        public void HandleError(string info)
        {
            _dialogService.OpenTextDialog("Error", info);
        }

        public void HandleException(Exception ex, string info = "")
        {
            _logger.LogError(ex, "{info}", info);
            HandleError(info);
        }
    }
}
