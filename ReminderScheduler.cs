using System;
using System.Collections.Generic;
using System.Windows.Threading;

namespace DittoBuddy;

internal class ReminderScheduler
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly HashSet<string> _firedToday = new();
    private DateTime _lastDate = DateTime.Today;
    private ReminderSettings _settings = ReminderStore.Load();

    public event Action<string>? Fired;

    public ReminderScheduler()
    {
        _timer.Tick += (_, _) => Check();
        _timer.Start();
    }

    public void Reload() => _settings = ReminderStore.Load();

    private void Check()
    {
        var now = DateTime.Now;
        if (now.Date != _lastDate)
        {
            _firedToday.Clear();
            _lastDate = now.Date;
        }
        string nowHm = now.ToString("HH:mm");

        if (_settings.WorkScheduleEnabled)
        {
            MaybeFireBefore("lunch", _settings.LunchStart, nowHm, "🍚 점심시간 5분 전이에요!");
            MaybeFireBefore("workend", _settings.WorkEnd, nowHm, "🏃 퇴근 5분 전이에요!");
        }

        foreach (var reminder in _settings.Custom)
            MaybeFireExact($"custom-{reminder.Time}-{reminder.Text}", reminder.Time, nowHm, $"⏰ {reminder.Text}");
    }

    private void MaybeFireBefore(string key, string targetHm, string nowHm, string message)
    {
        if (!TimeSpan.TryParse(targetHm, out var target)) return;
        string fireHm = DateTime.Today.Add(target).AddMinutes(-5).ToString("HH:mm");
        MaybeFireExact(key, fireHm, nowHm, message);
    }

    private void MaybeFireExact(string key, string targetHm, string nowHm, string message)
    {
        if (nowHm == targetHm && _firedToday.Add(key))
            Fired?.Invoke(message);
    }
}
