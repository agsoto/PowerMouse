using System.ComponentModel;
using System.Runtime.InteropServices;
using PowerMouse.App.Interop;
using PowerMouse.Core.Engine;

namespace PowerMouse.App.Services;

internal static class MouseInputService
{
    private const nuint CentralizedKeyboardHookBypassTag = 0x110;

    public static event Action? PointerMoved;

    public static void Send(IReadOnlyList<MouseCommand> commands)
    {
        if (commands.Count == 0)
        {
            return;
        }

        var inputs = commands.Select(CreateMouseInput).ToArray();
        Send(inputs);
    }

    public static void Move(int x, int y)
    {
        if (x == 0 && y == 0)
        {
            return;
        }

        Send(
        [
            new NativeMethods.Input
            {
                Type = NativeMethods.InputMouse,
                Data = new NativeMethods.InputUnion
                {
                    Mouse = new NativeMethods.MouseInput
                    {
                        X = x,
                        Y = y,
                        Flags = NativeMethods.MouseEventMove,
                    },
                },
            },
        ]);
        PointerMoved?.Invoke();
    }

    public static void ClickAt(int x, int y)
    {
        MoveTo(x, y);

        Send(
        [
            new MouseCommand(MouseCommandKind.LeftButtonDown),
            new MouseCommand(MouseCommandKind.LeftButtonUp),
        ]);
    }

    public static void MoveTo(int x, int y)
    {
        if (!NativeMethods.SetCursorPos(x, y))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not position the pointer on the selected hint.");
        }

        PointerMoved?.Invoke();
    }

    public static bool IsLockKeyOn(int virtualKey) => (NativeMethods.GetKeyState(virtualKey) & 1) != 0;

    public static void SetLockKeyState(int virtualKey, bool enabled)
    {
        if (IsLockKeyOn(virtualKey) == enabled)
        {
            return;
        }

        Send(
        [
            CreateKeyboardInput(virtualKey, isKeyUp: false),
            CreateKeyboardInput(virtualKey, isKeyUp: true),
        ]);
    }

    private static NativeMethods.Input CreateMouseInput(MouseCommand command)
    {
        var flags = command.Kind switch
        {
            MouseCommandKind.LeftButtonDown => NativeMethods.MouseEventLeftDown,
            MouseCommandKind.LeftButtonUp => NativeMethods.MouseEventLeftUp,
            MouseCommandKind.RightButtonDown => NativeMethods.MouseEventRightDown,
            MouseCommandKind.RightButtonUp => NativeMethods.MouseEventRightUp,
            MouseCommandKind.MiddleButtonDown => NativeMethods.MouseEventMiddleDown,
            MouseCommandKind.MiddleButtonUp => NativeMethods.MouseEventMiddleUp,
            MouseCommandKind.Wheel => NativeMethods.MouseEventWheel,
            _ => throw new ArgumentOutOfRangeException(nameof(command), command.Kind, "Unknown mouse command."),
        };

        return new NativeMethods.Input
        {
            Type = NativeMethods.InputMouse,
            Data = new NativeMethods.InputUnion
            {
                Mouse = new NativeMethods.MouseInput
                {
                    MouseData = command.Kind == MouseCommandKind.Wheel ? unchecked((uint)command.Data) : 0,
                    Flags = flags,
                },
            },
        };
    }

    private static NativeMethods.Input CreateKeyboardInput(int virtualKey, bool isKeyUp) => new()
    {
        Type = NativeMethods.InputKeyboard,
        Data = new NativeMethods.InputUnion
        {
            Keyboard = new NativeMethods.KeyboardInput
            {
                VirtualKey = checked((ushort)virtualKey),
                Flags = isKeyUp ? NativeMethods.KeyEventKeyUp : 0,
                ExtraInfo = CentralizedKeyboardHookBypassTag,
            },
        },
    };

    private static void Send(NativeMethods.Input[] inputs)
    {
        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.Input>());
        if (sent != inputs.Length)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows did not accept the requested input.");
        }
    }
}
