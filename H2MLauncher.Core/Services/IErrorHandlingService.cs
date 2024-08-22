namespace H2MLauncher.Core.Services
{
    public interface IErrorHandlingService
    {
        void HandleException(Exception ex, string info = "");
        void HandleError(string info);
    }
}
