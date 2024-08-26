using System.Windows;

namespace H2MLauncher.UI;

public static class AsyncWindowExtension
{
    public static Task<bool?> ShowDialogAsync(this Window self)
    {
        if (self == null) throw new ArgumentNullException(nameof(self));

        TaskCompletionSource<bool?> completion = new();
        self.Dispatcher.BeginInvoke(new Action(() => completion.SetResult(self.ShowDialog())));

        return completion.Task;
    }
}