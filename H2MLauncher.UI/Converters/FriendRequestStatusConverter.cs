using System.Windows.Data;

using H2MLauncher.Core.Social;

namespace H2MLauncher.UI.Converters
{
    public class FriendRequestStatusConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is FriendRequestStatus status)
            {
                return status switch
                {
                    FriendRequestStatus.PendingIncoming => "Incoming",
                    FriendRequestStatus.PendingOutgoing => "Outgoing",
                    _ => "Other"
                };
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
