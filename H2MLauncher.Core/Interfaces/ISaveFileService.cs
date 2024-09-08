namespace H2MLauncher.Core.Interfaces;

public interface ISaveFileService
{
    Task<string?> SaveFileAs(string initialFileName, string extension);
}