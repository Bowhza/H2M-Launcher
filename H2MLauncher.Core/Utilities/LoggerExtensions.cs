using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace H2MLauncher.Core.Utilities
{
    public static class LoggerExtensions
    {
        public static IDisposable? BeginPropertyScope(this ILogger logger, object value, [CallerArgumentExpression(nameof(value))] string expr = "")
        {
            Dictionary<string, object> dictionary = new() { { expr, value } };
            return logger.BeginScope(dictionary);
        }

        public static IDisposable? BeginPropertyScope(this ILogger logger,
            params ValueTuple<string, object>[] properties)
        {
            var dictionary = properties.ToDictionary(p => p.Item1, p => p.Item2);
            return logger.BeginScope(dictionary);
        }
    }
}
