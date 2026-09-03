using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace DittoBuddy;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(30) };
    // flips the character back and forth while it's held, so being grabbed reads as flustered
    private readonly DispatcherTimer _wiggleTimer = new() { Interval = TimeSpan.FromMilliseconds(90) };
    private readonly Random _rng = new();
    private double _vx;
    private double _vy;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            Left = _rng.Next(0, (int)(SystemParameters.WorkArea.Width - ActualWidth));
            Top = _rng.Next(0, (int)(SystemParameters.WorkArea.Height - ActualHeight));
        };
        PickNewDirection();
        _timer.Tick += Wander;
        _timer.Start();
        _wiggleTimer.Tick += (_, _) => FlipTransform.ScaleX *= -1;

        KeyboardLock.ProgressChanged += remaining => Dispatcher.Invoke(() =>
        {
            _cleaningOverlay ??= new CleaningOverlayWindow();
            if (!_cleaningOverlay.IsVisible) _cleaningOverlay.Show();
            _cleaningOverlay.UpdateProgress(remaining);
        });
        KeyboardLock.Unlocked += () => Dispatcher.Invoke(() =>
        {
            _cleaningOverlay?.Close();
            _cleaningOverlay = null;
        });

        _reminders.Fired += message => Dispatcher.Invoke(() =>
        {
            var anchor = new System.Windows.Point(Left + ActualWidth / 2, Top);
            new SpeechBubbleWindow(message, anchor).ShowAndAutoClose(TimeSpan.FromSeconds(6));
        });

        SetupTrayIcon();
        if (!StartupManager.IsEnabled) StartupManager.Enable();
    }

    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private CleaningOverlayWindow? _cleaningOverlay;
    private readonly ReminderScheduler _reminders = new();

    private void SetupTrayIcon()
    {
        var resourceInfo = Application.GetResourceStream(new Uri("Assets/ditto.png", UriKind.Relative));
        using var bitmap = new System.Drawing.Bitmap(resourceInfo!.Stream);
        var hIcon = bitmap.GetHicon();

        var toggleItem = new System.Windows.Forms.ToolStripMenuItem("숨기기", null, (_, _) => ToggleVisibility());
        var startupItem = new System.Windows.Forms.ToolStripMenuItem("부팅 시 자동 시작") { Checked = StartupManager.IsEnabled, CheckOnClick = true };
        startupItem.Click += (_, _) =>
        {
            if (startupItem.Checked) StartupManager.Enable(); else StartupManager.Disable();
        };
        var exitItem = new System.Windows.Forms.ToolStripMenuItem("종료", null, (_, _) => Application.Current.Shutdown());

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add(toggleItem);
        menu.Items.Add(startupItem);
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(exitItem);
        menu.Opening += (_, _) => toggleItem.Text = IsVisible ? "숨기기" : "보이기";

        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.Icon.FromHandle(hIcon),
            Visible = true,
            Text = "DittoBuddy",
            ContextMenuStrip = menu
        };
        _trayIcon.DoubleClick += (_, _) => ToggleVisibility();
    }

    private void ToggleVisibility()
    {
        if (IsVisible)
        {
            Hide();
            _timer.Stop();
        }
        else
        {
            Show();
            _timer.Start();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        KeyboardLock.Disable();
        _trayIcon?.Dispose();
        base.OnClosed(e);
    }

    // Prefers directly above the character, like the previous placement always did, but drops
    // below (and clamps to the work area) when there isn't room — otherwise the menu, and the
    // close animation that mirrors its rect, render partly off-screen whenever the character is
    // wandering near a screen edge.
    private Rect ComputeMenuScreenRect(System.Windows.Size popupSize)
    {
        var target = PointToScreenDip(new System.Windows.Point(0, 0));
        double targetLeft = target.X;
        double targetTop = target.Y;
        var area = SystemParameters.WorkArea;

        double top = targetTop - popupSize.Height - 6;
        if (top < area.Top) top = targetTop + RootCanvas.ActualHeight + 6;

        double left = targetLeft + (RootCanvas.ActualWidth - popupSize.Width) / 2;
        left = Math.Clamp(left, area.Left, Math.Max(area.Left, area.Right - popupSize.Width));
        top = Math.Clamp(top, area.Top, Math.Max(area.Top, area.Bottom - popupSize.Height));

        return new Rect(left, top, popupSize.Width, popupSize.Height);
    }

    private void PickNewDirection()
    {
        double angle = _rng.NextDouble() * Math.PI * 2;
        double speed = _rng.NextDouble() * 1.5 + 0.6;
        _vx = Math.Cos(angle) * speed;
        _vy = Math.Sin(angle) * speed;
        FlipTransform.ScaleX = _vx < 0 ? -1 : 1;
    }

    private void Wander(object? sender, EventArgs e)
    {
        var area = SystemParameters.WorkArea;
        double w = Math.Max(ActualWidth, 1);
        double h = Math.Max(ActualHeight, 1);

        double newLeft = Left + _vx;
        double newTop = Top + _vy;

        bool hitEdge = false;
        if (newLeft < area.Left || newLeft + w > area.Right) { _vx = -_vx; hitEdge = true; }
        if (newTop < area.Top || newTop + h > area.Bottom) { _vy = -_vy; hitEdge = true; }
        if (hitEdge) FlipTransform.ScaleX *= -1;

        Left = Math.Clamp(Left + _vx, area.Left, area.Right - w);
        Top = Math.Clamp(Top + _vy, area.Top, area.Bottom - h);
    }

    private bool _isDragging;
    private System.Windows.Point _dragStartMouse;
    private double _dragStartLeft, _dragStartTop, _lastDragMouseX;

    private System.Windows.Point PointToScreenDip(System.Windows.Point clientPoint)
    {
        var screenPos = RootCanvas.PointToScreen(clientPoint);
        var dpi = VisualTreeHelper.GetDpi(RootCanvas);
        return new System.Windows.Point(screenPos.X / dpi.DpiScaleX, screenPos.Y / dpi.DpiScaleY);
    }

    private void RootCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        _dragStartMouse = PointToScreenDip(e.GetPosition(RootCanvas));
        _lastDragMouseX = _dragStartMouse.X;
        _dragStartLeft = Left;
        _dragStartTop = Top;
        _timer.Stop();
        _wiggleTimer.Start();
        RootCanvas.CaptureMouse();
    }

    private void RootCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!RootCanvas.IsMouseCaptured) return;

        var current = PointToScreenDip(e.GetPosition(RootCanvas));
        double dx = current.X - _dragStartMouse.X;
        double dy = current.Y - _dragStartMouse.Y;

        if (!_isDragging && (Math.Abs(dx) > 4 || Math.Abs(dy) > 4))
            _isDragging = true;
        if (!_isDragging) return;

        var area = SystemParameters.WorkArea;
        Left = Math.Clamp(_dragStartLeft + dx, area.Left, area.Right - ActualWidth);
        Top = Math.Clamp(_dragStartTop + dy, area.Top, area.Bottom - ActualHeight);
    }

    private void RootCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        RootCanvas.ReleaseMouseCapture();
        _wiggleTimer.Stop();
        if (_isDragging)
        {
            _isDragging = false;
            PickNewDirection(); // also resets the flip to match the new direction
            _timer.Start();
            return;
        }
        FlipTransform.ScaleX = _vx < 0 ? -1 : 1;
        Character_Click(sender, e);
    }

    private void PopCharacterBackIn()
    {
        CharacterImage.Visibility = Visibility.Visible;

        var pop = new ScaleTransform(0.4, 0.4);
        CharacterImage.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
        CharacterImage.RenderTransform = pop;

        var grow = new DoubleAnimation(0.4, 1, TimeSpan.FromSeconds(0.18))
        { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 } };
        grow.Completed += (_, _) => _timer.Start();
        pop.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        pop.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
    }

    private void Character_Click(object sender, MouseButtonEventArgs e)
    {
        _timer.Stop();
        CharacterImage.Visibility = Visibility.Hidden;
        var menu = new ContextMenu();

        // Transform back: pop the character itself in. Deliberately no stand-in window for the
        // menu's own shrink — every attempt at that flashed a stray purple box, because a second
        // top-level window can't be positioned and composited reliably in the instant a Popup dies.
        menu.Closed += (_, _) => PopCharacterBackIn();

        if (KeyboardLock.IsActive)
        {
            var lockHeader = new StackPanel { Orientation = Orientation.Horizontal };
            lockHeader.Children.Add(BuildIcon(Icons.Lock, 8));
            lockHeader.Children.Add(new TextBlock { Text = "청소 모드: 스페이스바 5번으로 해제", VerticalAlignment = VerticalAlignment.Center });
            menu.Items.Add(new MenuItem { Header = lockHeader, IsEnabled = false });
            menu.Items.Add(MakeItem("지금 바로 잠금 해제", Icons.Check, KeyboardLock.Disable));
        }
        else
        {
            menu.Items.Add(MakeItem("키보드 청소 모드 시작", Icons.Keyboard, KeyboardLock.Enable));
            menu.Items.Add(MakeItem("스톱워치", Icons.Stopwatch, () => new StopwatchWindow().Show()));
            menu.Items.Add(MakeItem("타이머", Icons.Hourglass, () => new TimerWindow().Show()));
            menu.Items.Add(MakeItem("리마인더 설정...", Icons.SpeechBubble, () => { new ReminderSettingsWindow().ShowDialog(); _reminders.Reload(); }));
            menu.Items.Add(new Separator());
            menu.Items.Add(MakeItem("메모장 열기", Icons.Notepad, () => Process.Start("notepad.exe")));
            menu.Items.Add(MakeItem("계산기 열기", Icons.Calculator, () => Process.Start("calc.exe")));
            menu.Items.Add(MakeItem("웹 검색 열기", Icons.Search, () => Process.Start(new ProcessStartInfo("https://www.google.com") { UseShellExecute = true })));
            menu.Items.Add(new Separator());
            menu.Items.Add(MakeItem("종료", Icons.Close, () => Application.Current.Shutdown()));

            menu.Items.Add(new Separator());
            var row = new WrapPanel { MaxWidth = 220 };
            foreach (var entry in ShortcutStore.Load())
            {
                var button = new Button
                {
                    Style = (Style)Application.Current.FindResource("GlassIconButtonStyle"),
                    Width = 30,
                    Height = 30,
                    Margin = new Thickness(2),
                    ToolTip = entry.Name,
                    Padding = new Thickness(0),
                    Content = MakeIconContent(entry)
                };
                button.Click += (_, _) =>
                {
                    Process.Start(new ProcessStartInfo(entry.Path) { UseShellExecute = true });
                    menu.IsOpen = false;
                };
                row.Children.Add(button);
            }
            var addButton = new Button
            {
                Style = (Style)Application.Current.FindResource("GlassIconButtonStyle"),
                Width = 30,
                Height = 30,
                Margin = new Thickness(2),
                ToolTip = "바로가기 관리",
                Padding = new Thickness(0),
                Content = BuildIcon(Icons.Plus)
            };
            addButton.Click += (_, _) =>
            {
                menu.IsOpen = false;
                new ShortcutsWindow().ShowDialog();
            };
            row.Children.Add(addButton);
            menu.Items.Add(new MenuItem { Header = row, Focusable = false });
        }

        menu.PlacementTarget = RootCanvas;
        menu.Placement = PlacementMode.Custom;
        menu.CustomPopupPlacementCallback = (popupSize, _, _) =>
        {
            var rect = ComputeMenuScreenRect(popupSize);
            var target = PointToScreenDip(new System.Windows.Point(0, 0));
            // CustomPopupPlacement is an offset relative to the target, not an absolute screen point
            return new[] { new CustomPopupPlacement(new System.Windows.Point(rect.Left - target.X, rect.Top - target.Y), PopupPrimaryAxis.None) };
        };
        menu.IsOpen = true;
    }

    private static object MakeIconContent(ShortcutEntry entry)
    {
        try
        {
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(entry.Path);
            if (icon != null)
            {
                var source = Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                return new System.Windows.Controls.Image { Source = source, Width = 24, Height = 24 };
            }
        }
        catch
        {
            // fall through to text fallback
        }
        return new TextBlock
        {
            Text = entry.Name.Length > 0 ? entry.Name[0].ToString() : "?",
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private static MenuItem MakeItem(string text, string iconData, Action action)
    {
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(BuildIcon(iconData, 8));
        header.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    private static Path BuildIcon(string data, double rightMargin = 0)
    {
        return new Path
        {
            Data = Geometry.Parse(data),
            Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x33, 0x2B, 0x40)),
            StrokeThickness = 1.4,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Fill = System.Windows.Media.Brushes.Transparent,
            Width = 16,
            Height = 16,
            Stretch = Stretch.Uniform,
            Margin = new Thickness(0, 0, rightMargin, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
    }
}
