using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TransDiffer
{
    public class SearchTermToVisibility : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool invert = parameter is string ss && ss == "True";
            return (value is string s && s.Length > 0) != invert ? Visibility.Visible : Visibility.Hidden;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
