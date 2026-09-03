using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DittoBuddy;

internal static class KeyboardLock
{
    public const int RequiredSpacePresses = 5;

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int VK_SPACE = 0x20;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    // kept as a field so the delegate isn't garbage-collected while the hook is installed
    private static readonly LowLevelKeyboardProc Proc = HookCallback;
    private static IntPtr _hookId = IntPtr.Zero;
    private static int _spacePresses;

    public static bool IsActive => _hookId != IntPtr.Zero;

    public static event Action<int>? ProgressChanged; // remaining presses
    public static event Action? Unlocked;

    public static void Enable()
    {
        if (IsActive) return;
        _spacePresses = 0;
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, Proc, GetModuleHandle(curModule.ModuleName!), 0);
        ProgressChanged?.Invoke(RequiredSpacePresses);
    }

    public static void Disable()
    {
        if (!IsActive) return;
        UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
        Unlocked?.Invoke();
    }

    private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN))
        {
            int vkCode = Marshal.ReadInt32(lParam);
            if (vkCode == VK_SPACE)
            {
                _spacePresses++;
                ProgressChanged?.Invoke(Math.Max(0, RequiredSpacePresses - _spacePresses));
                if (_spacePresses >= RequiredSpacePresses)
                {
                    Disable();
                }
            }
            return (IntPtr)1; // swallow every key while cleaning mode is active
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);
}
