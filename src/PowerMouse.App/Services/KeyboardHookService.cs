using System.ComponentModel;
using System.Runtime.InteropServices;
using PowerMouse.App.Interop;
using PowerMouse.Core.Engine;

namespace PowerMouse.App.Services;

internal sealed class KeyboardHookService : IDisposable
{
    private readonly ManualResetEventSlim started = new(initialState: false);
    private readonly NativeMethods.HookProcedure hookProcedure;
    private Thread? hookThread;
    private nint hook;
    private uint hookThreadId;
    private Exception? startupException;
    private bool disposed;

    public KeyboardHookService()
    {
        hookProcedure = HookCallback;
    }

    public Func<KeyboardStroke, bool>? KeyReceived { get; set; }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (hookThread is not null)
        {
            return;
        }

        hookThread = new Thread(RunMessageLoop)
        {
            IsBackground = true,
            Name = "PowerMouse keyboard hook",
        };
        hookThread.Start();

        if (!started.Wait(TimeSpan.FromSeconds(5)))
        {
            throw new TimeoutException("The keyboard hook did not start in time.");
        }

        if (startupException is not null)
        {
            throw new InvalidOperationException("The keyboard hook could not be installed.", startupException);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (hookThreadId != 0)
        {
            NativeMethods.PostThreadMessage(hookThreadId, NativeMethods.WmQuit, 0, 0);
        }

        hookThread?.Join(TimeSpan.FromSeconds(2));
        started.Dispose();
    }

    private void RunMessageLoop()
    {
        try
        {
            hookThreadId = NativeMethods.GetCurrentThreadId();
            hook = NativeMethods.SetWindowsHookEx(
                NativeMethods.WhKeyboardLowLevel,
                hookProcedure,
                NativeMethods.GetModuleHandle(null),
                0);

            if (hook == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            started.Set();

            while (NativeMethods.GetMessage(out var message, 0, 0, 0) > 0)
            {
                NativeMethods.TranslateMessage(in message);
                NativeMethods.DispatchMessage(in message);
            }
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            startupException = exception;
            started.Set();
        }
        finally
        {
            if (hook != 0)
            {
                NativeMethods.UnhookWindowsHookEx(hook);
                hook = 0;
            }
        }
    }

    private nint HookCallback(int code, nuint wParam, nint lParam)
    {
        if (code != NativeMethods.HcAction || KeyReceived is null)
        {
            return NativeMethods.CallNextHookEx(hook, code, wParam, lParam);
        }

        try
        {
            var message = unchecked((uint)wParam);
            var isKeyDown = message is NativeMethods.WmKeyDown or NativeMethods.WmSysKeyDown;
            var isKeyUp = message is NativeMethods.WmKeyUp or NativeMethods.WmSysKeyUp;
            if (!isKeyDown && !isKeyUp)
            {
                return NativeMethods.CallNextHookEx(hook, code, wParam, lParam);
            }

            var data = Marshal.PtrToStructure<NativeMethods.KeyboardHookData>(lParam);
            var stroke = new KeyboardStroke(
                checked((int)data.VirtualKey),
                checked((int)data.ScanCode),
                IsExtended: (data.Flags & 0x01) != 0,
                IsKeyDown: isKeyDown,
                IsInjected: (data.Flags & 0x10) != 0);

            if (KeyReceived(stroke))
            {
                return 1;
            }
        }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            // A global hook must fail open so it can never lock the user's keyboard.
        }

        return NativeMethods.CallNextHookEx(hook, code, wParam, lParam);
    }
}
