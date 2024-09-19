using H2MLauncher.UI.Services;

using Microsoft.Win32;

namespace H2MLauncher.UI;

internal class SaveFileService : ISaveFileService
{
    public async Task<string?> SaveFileAs(string initialFileName, string filter)
    {
        var dialog = new SaveFileDialog()
        {
            FileName = initialFileName,
            Filter = filter
        };

        await Task.Yield();

        if (dialog.ShowDialog() == true)
        {
            return dialog.FileName;
        }

        return null;
    }
}
