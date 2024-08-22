using System.Collections;

using H2MLauncher.Core.ViewModels;

namespace H2MLauncher.UI
{
    class PlayersSorter : IComparer
    {
        public int Compare(ServerViewModel x, ServerViewModel y)
        {
            if (x == null || y == null)
            {
                return 0;
            }

            var clientResult = Comparer<int>.Default.Compare(x.ClientNum, y.ClientNum);
            if (clientResult != 0)
            {
                return clientResult;
            }

            return Comparer<int>.Default.Compare(x.MaxClientNum, y.MaxClientNum);
        }

        public int Compare(object? x, object? y)
        {
            if (x is not ServerViewModel vmX || y is not ServerViewModel vmY)
            {
                return 0;
            }

            return Compare(vmX, vmY);
        }
    }
}