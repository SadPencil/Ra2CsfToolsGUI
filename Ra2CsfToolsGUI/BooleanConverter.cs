using System.Globalization;
using System.Windows.Data;

namespace Ra2CsfToolsGUI
{
    // https://stackoverflow.com/a/5182660
    public class BooleanConverter<T> : IValueConverter
    {
        public BooleanConverter(T trueValue, T falseValue)
        {
            this.True = trueValue;
            this.False = falseValue;
        }

        public T True { get; set; }
        public T False { get; set; }

        public virtual object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            (value is bool b && b) ? this.True : this.False;

        public virtual object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is T t && EqualityComparer<T>.Default.Equals(t, this.True);
    }
}
