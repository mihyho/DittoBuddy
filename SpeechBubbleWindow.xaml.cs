using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace DittoBuddy;

public partial class SpeechBubbleWindow : Window
{
    public SpeechBubbleWindow(string message, Point anchorTopCenter)
    {
        InitializeComponent();
        MessageText.Text = message;
        Loaded += (_, _) =>
        {
            var area = SystemParameters.WorkArea;
            Left = Math.Clamp(anchorTopCenter.X - 40, area.Left, area.Right - ActualWidth);
            Top = Math.Max(anchorTopCenter.Y - ActualHeight - 8, area.Top);
        };
    }

    public void ShowAndAutoClose(TimeSpan duration)
    {
        Show();
        var timer = new DispatcherTimer { Interval = duration };
        timer.Tick += (_, _) => { timer.Stop(); Close(); };
        timer.Start();
    }

    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => Close();
}
