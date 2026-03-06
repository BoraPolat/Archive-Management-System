#if WINDOWS
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace SoftwareDesign.Platforms.Windows
{
    public static class MouseWheelHelper
    {
        public static event EventHandler<int> MouseWheelScrolled;

        public static void Initialize(Microsoft.UI.Xaml.Window window)
        {
            if (window?.Content is UIElement content)
            {
                content.PointerWheelChanged += OnPointerWheelChanged;
            }
        }

        private static void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var delta = e.GetCurrentPoint(sender as UIElement).Properties.MouseWheelDelta;
            MouseWheelScrolled?.Invoke(null, delta);
        }
    }
}
#endif