namespace H2MLauncher.UI.Dialog
{
    public class RequestCloseEventArgs : EventArgs
    {
        public RequestCloseEventArgs(bool dialogResult)
        {
            this.DialogResult = dialogResult;
        }

        public bool DialogResult { get; private set; }
    }
}