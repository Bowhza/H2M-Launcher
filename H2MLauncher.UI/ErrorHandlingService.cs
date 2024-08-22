using H2MLauncher.Core.Services;
using H2MLauncher.UI.Dialog;

namespace H2MLauncher.UI
{
    public class ErrorHandlingService(DialogService dialogService) : IErrorHandlingService
    {
        private readonly DialogService _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

        public void HandleError(string info)
        {
            _dialogService.OpenTextDialog("Error", info);
        }

        public void HandleException(Exception ex, string info = "")
        {
            HandleError(info);
            // log error.
        }
    }
}
