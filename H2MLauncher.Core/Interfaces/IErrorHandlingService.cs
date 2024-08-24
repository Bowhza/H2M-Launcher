namespace H2MLauncher.Core.Interfaces
{
    public interface IErrorHandlingService
    {
        void HandleException(Exception ex, string info = "");
        void HandleError(string info);
    }
}
