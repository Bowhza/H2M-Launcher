using System.ComponentModel;
using System.Globalization;

using H2MLauncher.Core.Models;

namespace H2MLauncher.Core.Utilities
{
    public class ServerConnectionDetailsTypeConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        {
            if (sourceType == typeof(string))
            {
                return true;
            }
            return base.CanConvertFrom(context, sourceType);
        }

        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            if (value is string stringValue &&
                ServerConnectionDetails.TryParse(stringValue, out ServerConnectionDetails connectionDetails))
            {
                return connectionDetails;
            }
            return base.ConvertFrom(context, culture, value);
        }
    }
}
