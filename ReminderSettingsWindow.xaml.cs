using System;
using System.Windows;

namespace DittoBuddy;

public partial class ReminderSettingsWindow : Window
{
    private readonly ReminderSettings _settings;
    private bool _suppressAutoClose;
    private bool _hasActivated;

    public ReminderSettingsWindow()
    {
        InitializeComponent();
        _settings = ReminderStore.Load();
        WorkScheduleCheck.IsChecked = _settings.WorkScheduleEnabled;
        LunchStartBox.Text = _settings.LunchStart;
        WorkEndBox.Text = _settings.WorkEnd;
        RefreshList();
        Closing += (_, _) => SaveWorkSchedule();
        // Deactivated can fire once, spuriously, before the window has really finished becoming
        // active right after Show() — closing on that would mean it never gets a chance to be used.
        Activated += (_, _) => _hasActivated = true;
        Deactivated += (_, _) => { if (_hasActivated && !_suppressAutoClose) Close(); }; // click away to dismiss
    }

    private void RefreshList()
    {
        ReminderList.ItemsSource = null;
        ReminderList.ItemsSource = _settings.Custom;
    }

    private void AddReminder_Click(object sender, RoutedEventArgs e)
    {
        if (!TimeSpan.TryParse(NewTimeBox.Text, out _))
        {
            _suppressAutoClose = true;
            MessageBox.Show(this, "시간 형식은 HH:mm 이어야 합니다.", "DittoBuddy");
            _suppressAutoClose = false;
            return;
        }
        if (string.IsNullOrWhiteSpace(NewTextBox.Text)) return;

        _settings.Custom.Add(new CustomReminder(NewTimeBox.Text.Trim(), NewTextBox.Text.Trim()));
        ReminderStore.Save(_settings);
        RefreshList();
    }

    private void RemoveReminder_Click(object sender, RoutedEventArgs e)
    {
        if (ReminderList.SelectedItem is not CustomReminder selected) return;
        _settings.Custom.Remove(selected);
        ReminderStore.Save(_settings);
        RefreshList();
    }

    private void SaveWorkSchedule()
    {
        _settings.WorkScheduleEnabled = WorkScheduleCheck.IsChecked == true;
        if (TimeSpan.TryParse(LunchStartBox.Text, out _)) _settings.LunchStart = LunchStartBox.Text.Trim();
        if (TimeSpan.TryParse(WorkEndBox.Text, out _)) _settings.WorkEnd = WorkEndBox.Text.Trim();
        ReminderStore.Save(_settings);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
