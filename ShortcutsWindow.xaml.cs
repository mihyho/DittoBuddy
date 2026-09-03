using System.Collections.Generic;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace DittoBuddy;

public partial class ShortcutsWindow : Window
{
    private readonly List<ShortcutEntry> _entries;

    public ShortcutsWindow()
    {
        InitializeComponent();
        _entries = ShortcutStore.Load();
        ShortcutList.ItemsSource = _entries;
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "바로가기로 등록할 파일 선택",
            Filter = "실행 파일/바로가기 (*.exe;*.lnk)|*.exe;*.lnk|모든 파일 (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != true) return;

        _entries.Add(new ShortcutEntry(Path.GetFileNameWithoutExtension(dialog.FileName), dialog.FileName));
        ShortcutStore.Save(_entries);
        ShortcutList.Items.Refresh();
        ShortcutList.ItemsSource = null;
        ShortcutList.ItemsSource = _entries;
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (ShortcutList.SelectedItem is not ShortcutEntry selected) return;
        _entries.Remove(selected);
        ShortcutStore.Save(_entries);
        ShortcutList.ItemsSource = null;
        ShortcutList.ItemsSource = _entries;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
