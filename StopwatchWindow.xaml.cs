using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace DittoBuddy;

public partial class StopwatchWindow : Window
{
    private readonly Stopwatch _stopwatch = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(50) };

    public StopwatchWindow()
    {
        InitializeComponent();
        _timer.Tick += (_, _) => UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        var t = _stopwatch.Elapsed;
        TimeText.Text = $"{(int)t.TotalMinutes:00}:{t.Seconds:00}.{t.Milliseconds / 100}";
    }

    private void StartStopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_stopwatch.IsRunning)
        {
            _stopwatch.Stop();
            _timer.Stop();
            StartStopButton.Content = "시작";
        }
        else
        {
            _stopwatch.Start();
            _timer.Start();
            StartStopButton.Content = "정지";
        }
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        _stopwatch.Reset();
        UpdateDisplay();
    }
}
