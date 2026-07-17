using System.Globalization;
using System.Windows.Data;

namespace WpfApp3.Converters
{
    /// <summary>
    /// Converts null values to display-friendly text
    /// Used for handling missing or empty data
    /// </summary>
    public class NullToEmptyStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Placeholder for implementation
            throw new NotImplementedException();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Placeholder for implementation
            throw new NotImplementedException();
        }
    }
}
