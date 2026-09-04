using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace DittoBuddy;

// Calls the Claude Code CLI (same approach as jarvis_project/brain/claude_client.py: shells out
// to the `claude` subscription CLI instead of a billed API key). --allowedTools "" keeps it a
// plain Q&A box — it can't read/write files or run commands from here.
public partial class AiAgentWindow : Window
{
    private readonly Rect _characterScreenRect;
    private bool _closingAnimated;

    public AiAgentWindow(Rect characterScreenRect)
    {
        InitializeComponent();
        _characterScreenRect = characterScreenRect;
        ContentRendered += (_, _) => { PositionNearCharacter(); PlayGrowIn(); };
        Loaded += (_, _) => QuestionBox.Focus();
    }

    private void PositionNearCharacter()
    {
        var area = SystemParameters.WorkArea;
        double left = _characterScreenRect.Left + (_characterScreenRect.Width - ActualWidth) / 2;
        double top = _characterScreenRect.Top - ActualHeight - 6;
        if (top < area.Top) top = _characterScreenRect.Bottom + 6; // no room above: drop below instead

        Left = Math.Clamp(left, area.Left, Math.Max(area.Left, area.Right - ActualWidth));
        Top = Math.Clamp(top, area.Top, Math.Max(area.Top, area.Bottom - ActualHeight));
    }

    private void PlayGrowIn()
    {
        var sb = new Storyboard();
        AddScale(sb, "(UIElement.RenderTransform).(ScaleTransform.ScaleY)", 0.35, 1, 0.22, new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 });
        AddScale(sb, "(UIElement.RenderTransform).(ScaleTransform.ScaleX)", 0.85, 1, 0.22, new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 });
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.12));
        Storyboard.SetTarget(fade, RootBorder);
        Storyboard.SetTargetProperty(fade, new PropertyPath(OpacityProperty));
        sb.Children.Add(fade);
        sb.Begin();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_closingAnimated)
        {
            base.OnClosing(e);
            return;
        }
        e.Cancel = true;
        _closingAnimated = true;

        var sb = new Storyboard();
        AddScale(sb, "(UIElement.RenderTransform).(ScaleTransform.ScaleY)", 1, 0.35, 0.16, new BackEase { EasingMode = EasingMode.EaseIn, Amplitude = 0.3 });
        AddScale(sb, "(UIElement.RenderTransform).(ScaleTransform.ScaleX)", 1, 0.85, 0.16, new BackEase { EasingMode = EasingMode.EaseIn, Amplitude = 0.3 });
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.12));
        Storyboard.SetTarget(fade, RootBorder);
        Storyboard.SetTargetProperty(fade, new PropertyPath(OpacityProperty));
        sb.Children.Add(fade);
        sb.Completed += (_, _) => Close();
        sb.Begin();
    }

    private void AddScale(Storyboard sb, string property, double from, double to, double seconds, IEasingFunction easing)
    {
        var anim = new DoubleAnimation(from, to, TimeSpan.FromSeconds(seconds)) { EasingFunction = easing };
        Storyboard.SetTarget(anim, RootBorder);
        Storyboard.SetTargetProperty(anim, new PropertyPath(property));
        sb.Children.Add(anim);
    }

    private void QuestionBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) AskButton_Click(sender, e);
    }

    private async void AskButton_Click(object sender, RoutedEventArgs e)
    {
        string question = QuestionBox.Text.Trim();
        if (question.Length == 0) return;

        AskButton.IsEnabled = false;
        AnswerText.Text = "생각 중...";
        AnswerText.Text = await AskClaudeAsync(question);
        AskButton.IsEnabled = true;
    }

    private static async Task<string> AskClaudeAsync(string question)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "claude",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add(question);
        psi.ArgumentList.Add("--allowedTools");
        psi.ArgumentList.Add("");

        try
        {
            using var process = Process.Start(psi);
            if (process == null) return "AI 에이전트를 실행할 수 없습니다.";

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            var waitTask = process.WaitForExitAsync();

            if (await Task.WhenAny(waitTask, Task.Delay(TimeSpan.FromSeconds(45))) != waitTask)
            {
                try { process.Kill(true); } catch { /* already exited */ }
                return "응답이 너무 오래 걸려서 중단했습니다.";
            }

            string stdout = (await stdoutTask).Trim();
            string stderr = (await stderrTask).Trim();
            if (process.ExitCode != 0)
                return stderr.Length > 0 ? stderr : "AI 에이전트 호출에 실패했습니다.";
            return stdout.Length > 0 ? stdout : "(응답이 비어 있습니다)";
        }
        catch (Exception ex)
        {
            return $"Claude Code CLI를 찾을 수 없습니다. 'npm install -g @anthropic-ai/claude-code'로 설치되어 있는지 확인해주세요.\n({ex.Message})";
        }
    }
}
