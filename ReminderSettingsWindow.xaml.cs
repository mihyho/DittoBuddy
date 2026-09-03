using System;
using System.Windows;

namespace DittoBuddy;

public partial class ReminderSettingsWindow : Window
{
    private readonly ReminderSettings _settings;

    public ReminderSettingsWindow()
    {
        InitializeComponent();
        _settings = ReminderStore.Load();
        WorkScheduleCheck.IsChecked = _settings.WorkScheduleEnabled;
        LunchStartBox.Text = _settings.LunchStart;
        WorkEndBox.Text = _settings.WorkEnd;
        RefreshList();
        Closing += (_, _) => SaveWorkSchedule();
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
            MessageBox.Show(this, "시간 형식은 HH:mm 이어야 합니다.", "DittoBuddy");
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
