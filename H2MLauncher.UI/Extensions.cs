using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Nogic.WritableOptions;

namespace H2MLauncher.UI
{
    public static class Extensions
    {
        public static void Update<T>(this IWritableOptions<T> options, Func<T, T> updateFunc, bool reload = true)
            where T : class, new()
        {
            options.Update(updateFunc(options.CurrentValue), reload);
        }
    }
}
