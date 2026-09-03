using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DittoBuddy;

public partial class CleaningOverlayWindow : Window
{
    public CleaningOverlayWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            var area = SystemParameters.WorkArea;
            Left = area.Left + (area.Width - ActualWidth) / 2;
            Top = area.Top + (area.Height - ActualHeight) / 2;
        };
        UpdateProgress(KeyboardLock.RequiredSpacePresses);
    }

    public void UpdateProgress(int remaining)
    {
        Dots.Children.Clear();
        int done = KeyboardLock.RequiredSpacePresses - remaining;
        for (int i = 0; i < KeyboardLock.RequiredSpacePresses; i++)
        {
            bool isChecked = i < done;
            Dots.Children.Add(new Border
            {
                Width = 26,
                Height = 26,
                Margin = new Thickness(3),
                CornerRadius = new CornerRadius(13),
                Background = isChecked
                    ? new SolidColorBrush(Color.FromRgb(0x8B, 0x7C, 0xC7))
                    : Brushes.Transparent,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xB7, 0xA8, 0xDC)),
                BorderThickness = new Thickness(2),
                Child = isChecked
                    ? new TextBlock
                    {
                        Text = "✓",
                        Foreground = Brushes.White,
                        FontWeight = FontWeights.Bold,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                    : null
            });
        }
    }
}
