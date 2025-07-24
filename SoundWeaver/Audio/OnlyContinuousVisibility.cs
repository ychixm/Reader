using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using SoundWeaver.Models;

namespace SoundWeaver.Audio
{
    public class OnlyContinuousVisibility : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (SfxType)value == SfxType.Continuous ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}