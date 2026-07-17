using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace eSureHi.Helpers
{
    public static class Adaptive
    {
        public static readonly DependencyProperty UseAdaptiveForegroundProperty =
            DependencyProperty.RegisterAttached(
                "UseAdaptiveForeground",
                typeof(bool),
                typeof(Adaptive),
                new PropertyMetadata(false, OnUseAdaptiveForegroundChanged));

        public static bool GetUseAdaptiveForeground(DependencyObject obj)
        {
            return (bool)obj.GetValue(UseAdaptiveForegroundProperty);
        }

        public static void SetUseAdaptiveForeground(DependencyObject obj, bool value)
        {
            obj.SetValue(UseAdaptiveForegroundProperty, value);
        }

        private static void OnUseAdaptiveForegroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                // Create a binding to the element's Background (or nearest ancestor with Background)
                Binding binding = new Binding("Background")
                {
                    Source = d,
                    Converter = new AdaptiveForegroundConverter()
                };

                // If it's a Border, bind to its own Background
                if (d is Border || d is Panel || d is Control)
                {
                    // Set the inherited TextElement.Foreground property
                    BindingOperations.SetBinding(d, TextElement.ForegroundProperty, binding);
                }
            }
        }
    }
}
