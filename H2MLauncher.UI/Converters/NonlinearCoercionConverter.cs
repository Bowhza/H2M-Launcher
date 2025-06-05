using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace H2MLauncher.UI.Converters
{
    public class NonlinearCoercionConverter : MarkupExtension, IValueConverter
    {
        public int InputMin { get; set; } = 0;
        public int InputMax { get; set; }

        public int OutputMin { get; set; } = 0;
        public int OutputMax { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double x)
            {
                // Ensure x is within the input range [0, 200]
                x = Math.Max(InputMin, Math.Min(InputMax, x));

                if (x == InputMin)
                {
                    return OutputMin;
                }

                if (x == InputMax)
                {
                    return OutputMax;
                }

                // Normalize x to a [0, 1] range
                double normalizedX = x / InputMax;

                // Apply a nonlinear transformation.
                // Example 1: Quadratic curve (more emphasis on smaller x values)
                // double transformedValue = max * Math.Pow(normalizedX, 2); 

                // Example 2: Square root curve (more emphasis on larger x values)
                // double transformedValue = max * Math.Sqrt(normalizedX);

                // Example 3: Cubic curve (flatter near 0 and 200, steeper in the middle)
                // double transformedValue = max * (0.5 * (Math.Sin(Math.PI * (normalizedX - 0.5)) + 1));

                // Example 4: A custom power curve (e.g., power of 1.5 for a softer curve than quadratic)
                double transformedValue = OutputMax * Math.Pow(normalizedX, 0.4) + OutputMin;

                // You can experiment with different mathematical functions here
                // to achieve the desired "nonlinear curve" effect.
                // Common choices include:
                // - Math.Pow(normalizedX, exponent) where exponent > 1 for accelerating curve
                // - Math.Pow(normalizedX, exponent) where 0 < exponent < 1 for decelerating curve
                // - Trigonometric functions like Math.Sin or Math.Cos for S-curves or other shapes.

                return transformedValue;
            }

            return 0.0; // Default or error value
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // This converter is designed for one-way binding.
            // Implementing ConvertBack would require inverting the nonlinear function,
            // which can be complex or impossible depending on the chosen function.
            throw new NotImplementedException();
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }
}
