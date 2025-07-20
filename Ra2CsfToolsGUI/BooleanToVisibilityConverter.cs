using System.Windows;

namespace Ra2CsfToolsGUI
{
    // https://stackoverflow.com/a/5182660
    public sealed class BooleanToVisibilityConverter : BooleanConverter<Visibility>
    {
        public BooleanToVisibilityConverter() :
            base(Visibility.Visible, Visibility.Collapsed)
        { }
    }
}
