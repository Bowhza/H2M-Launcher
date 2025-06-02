using System.Globalization;
using System.Windows.Data;

namespace H2MLauncher.UI.Converters
{
    public class ScaleOffsetMultiConverter : IMultiValueConverter
    {
        /// <summary>
        /// Converts a collection of values (original size and absolute offset) to a scaling factor.
        /// The order of values in the MultiBinding should be:
        /// [0] -> originalSize (double)
        /// [1] -> absoluteOffset (double)
        /// </summary>
        /// <param name="values">An array of objects that the source bindings produce.
        /// The first value should be the original size, and the second should be the absolute offset.</param>
        /// <param name="targetType">The type of the binding target property (should be double).</param>
        /// <param name="parameter">An optional parameter to be used in the converter (not used in this case, set to null).</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>A double representing the calculated scaling factor.</returns>
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2)
            {
                // Not enough values provided
                return 1.0;
            }

            bool invert = Equals(parameter, true);

            if (values[0] is double originalSize && values[1] is double absoluteOffset)
            {
                if (originalSize <= 0)
                {
                    // Handle cases where original size is zero or negative.
                    return 1.0; // No scaling if original size is invalid.
                }

                // Calculate the total amount to crop
                double totalCrop = 2 * absoluteOffset;

                // Ensure we don't try to scale to a negative or zero size
                double scaledSize = originalSize - totalCrop;

                if (scaledSize <= 0)
                {
                    // If the offset is too large, the content would disappear or invert.
                    // Return a very small positive scale to indicate it's almost gone, or 0.0.
                    return 0.0001; // Or 0.0 to make it disappear
                }

                return invert 
                    ? originalSize / scaledSize 
                    : scaledSize / originalSize;
            }

            // Return a default scale of 1.0 if inputs are invalid.
            return 1.0;
        }

        /// <summary>
        /// Not implemented for one-way binding scenario.
        /// </summary>
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("ScaleOffsetMultiConverter can only be used for one-way binding.");
        }
    }
}
