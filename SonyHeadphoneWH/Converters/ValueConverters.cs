using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace SonyHeadphoneWH.Converters
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool b)
            {
                if (Invert) b = !b;
                return b ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is Visibility v)
            {
                bool b = v == Visibility.Visible;
                return Invert ? !b : b;
            }
            return false;
        }
    }

    public class BatteryGlyphConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is int level)
            {
                if (level >= 90) return "\uEBAA"; // Battery10
                if (level >= 75) return "\uEBA8"; // Battery8
                if (level >= 50) return "\uEBA6"; // Battery6
                if (level >= 25) return "\uEBA3"; // Battery3
                if (level >= 10) return "\uEBA1"; // Battery1
                return "\uEBA0"; // Battery0
            }
            return "\uEBA0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }
}
