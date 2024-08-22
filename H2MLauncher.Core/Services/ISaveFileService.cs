
namespace H2MLauncher.Core;

public interface ISaveFileService
{
    Task<string?> SaveFileAs(string initialFileName, string extension);
}