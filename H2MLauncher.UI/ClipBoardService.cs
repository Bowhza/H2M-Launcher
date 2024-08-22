using System.Windows;

using H2MLauncher.Core.Services;

namespace H2MLauncher.UI
{
    public class ClipBoardService : IClipBoardService
    {
        public ClipBoardService()
        {
            
        }

        public void SaveToClipBoard(string text)
        {
            Clipboard.SetText(text);
        }
    }
}
