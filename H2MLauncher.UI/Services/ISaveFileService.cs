namespace H2MLauncher.UI.Services;

public interface ISaveFileService
{
    Task<string?> SaveFileAs(string initialFileName, string extension);
}