using System;
using System.Media;
using System.Windows;
using System.Windows.Threading;

namespace DittoBuddy;

public partial class TimerWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private TimeSpan _remaining;
    private bool _suppressAutoClose;
    private bool _hasActivated;

    public TimerWindow()
    {
        InitializeComponent();
        _timer.Tick += Timer_Tick;
        // Deactivated can fire once, spuriously, before the window has really finished becoming
        // active right after Show() — closing on that would mean it never gets a chance to be used.
        Activated += (_, _) => _hasActivated = true;
        Deactivated += (_, _) => { if (_hasActivated && !_suppressAutoClose) Close(); }; // click away to dismiss
    }

    private void StartCancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_timer.IsEnabled)
        {
            _timer.Stop();
            InputPanel.IsEnabled = true;
            StartCancelButton.Content = "시작";
            return;
        }

        if (!int.TryParse(MinutesBox.Text, out int minutes)) minutes = 0;
        if (!int.TryParse(SecondsBox.Text, out int seconds)) seconds = 0;
        _remaining = new TimeSpan(0, 0, minutes, seconds);
        if (_remaining <= TimeSpan.Zero) return;

        InputPanel.IsEnabled = false;
        StartCancelButton.Content = "취소";
        UpdateDisplay();
        _timer.Start();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        _remaining -= TimeSpan.FromSeconds(1);
        if (_remaining <= TimeSpan.Zero)
        {
            _timer.Stop();
            InputPanel.IsEnabled = true;
            StartCancelButton.Content = "시작";
            TimeText.Text = "00:00";
            SystemSounds.Exclamation.Play();
            _suppressAutoClose = true;
            MessageBox.Show(this, "타이머가 끝났습니다!", "DittoBuddy", MessageBoxButton.OK, MessageBoxImage.Information);
            _suppressAutoClose = false;
            return;
        }
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        TimeText.Text = $"{(int)_remaining.TotalMinutes:00}:{_remaining.Seconds:00}";
    }
}
