using System.Collections.Generic;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace DittoBuddy;

public partial class ShortcutsWindow : Window
{
    private readonly List<ShortcutEntry> _entries;
    private bool _suppressAutoClose;
    private bool _hasActivated;

    public ShortcutsWindow()
    {
        InitializeComponent();
        _entries = ShortcutStore.Load();
        ShortcutList.ItemsSource = _entries;
        // Deactivated can fire once, spuriously, before the window has really finished becoming
        // active right after Show() — closing on that would mean it never gets a chance to be used.
        Activated += (_, _) => _hasActivated = true;
        Deactivated += (_, _) => { if (_hasActivated && !_suppressAutoClose) Close(); }; // click away to dismiss
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "바로가기로 등록할 파일 선택",
            Filter = "실행 파일/바로가기 (*.exe;*.lnk)|*.exe;*.lnk|모든 파일 (*.*)|*.*"
        };
        _suppressAutoClose = true;
        bool picked = dialog.ShowDialog(this) == true;
        _suppressAutoClose = false;
        if (!picked) return;

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
