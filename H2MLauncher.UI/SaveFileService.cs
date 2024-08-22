using H2MLauncher.Core;

using Microsoft.Win32;

namespace H2MLauncher.UI;

internal class SaveFileService : ISaveFileService
{
    public async Task<string?> SaveFileAs(string initialFileName, string extension)
    {
        var dialog = new SaveFileDialog()
        {
            FileName = initialFileName,
            DefaultExt = extension,
        };

        await Task.Yield();

        if (dialog.ShowDialog() == true)
        {
            return dialog.FileName;
        }

        return null;
    }
}
