using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace SoftwareDesign
{
    // Delete butonunu disable etmek için (system type'lar için)
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue; // System ise false (disabled), değilse true (enabled)
            }
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // JSON field'ları okunabilir formata çevirme
    public class JsonFieldsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string json && !string.IsNullOrEmpty(json))
            {
                try
                {
                    var fields = JsonSerializer.Deserialize<List<string>>(json);
                    return string.Join(", ", fields);
                }
                catch
                {
                    return "N/A";
                }
            }
            return "No fields";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}