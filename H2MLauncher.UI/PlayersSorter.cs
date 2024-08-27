using System.Collections;

using H2MLauncher.UI.ViewModels;

namespace H2MLauncher.UI
{
    class PlayersSorter : IComparer
    {
        public int Compare(IServerViewModel x, IServerViewModel y)
        {
            if (x == null || y == null)
            {
                return 0;
            }

            int clientResult = Comparer<int>.Default.Compare(x.ClientNum, y.ClientNum);
            if (clientResult != 0)
            {
                return clientResult;
            }

            return Comparer<int>.Default.Compare(x.MaxClientNum, y.MaxClientNum);
        }

        public int Compare(object? x, object? y)
        {
            if (x is not IServerViewModel vmX || y is not IServerViewModel vmY)
            {
                return 0;
            }

            return Compare(vmX, vmY);
        }
    }
}