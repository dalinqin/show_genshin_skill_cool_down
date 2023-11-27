using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using Vanara.PInvoke;

// WPF的键盘事件

namespace Raiden;

public class KeyboardHook : IDisposable
{
    private User32.SafeHHOOK _hookHandle = new(IntPtr.Zero);
    private readonly User32.HookProc _hookProc;


    public KeyboardHook()
    {
        _hookProc = HookProc;
    }


    public void Dispose()
    {
        Stop();
        _hookHandle.Dispose();
    }

    public event EventHandler<KeyEventArgs> KeyUP;

    public void Start()
    {
        if (_hookHandle.IsInvalid)
        {
            _hookHandle =
                User32.SetWindowsHookEx(User32.HookType.WH_KEYBOARD_LL, _hookProc, Kernel32.GetModuleHandle());
            if (_hookHandle.IsInvalid) throw new SystemException("Failed to set hook");
        }
    }

    public void Stop()
    {
        if (!_hookHandle.IsClosed && !_hookHandle.IsInvalid)
        {
            var result = User32.UnhookWindowsHookEx(_hookHandle);
            if (!result)
            {
                var errorCode = Marshal.GetLastWin32Error();
                throw new SystemException($"Failed to remove hook. Error code: {errorCode}");
            }

            _hookHandle.Close();
        }
    }

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
            // 只处理键盘释放事件
            if (wParam == (IntPtr)User32.WindowMessage.WM_KEYUP ||
                wParam == (IntPtr)User32.WindowMessage.WM_SYSKEYUP)
            {
                var vkCode = Marshal.ReadInt32(lParam);
                var key = KeyInterop.KeyFromVirtualKey(vkCode);
                var args = new KeyEventArgs(Keyboard.PrimaryDevice,
                    PresentationSource.FromVisual(Application.Current.MainWindow), 0, key)
                {
                    RoutedEvent = Keyboard.KeyUpEvent
                };

                KeyUP?.Invoke(this, args);
            }

        return User32.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }
}