namespace PowerMouse.Core.Models;

public static class VirtualKeys
{
    public const int None = 0x00;
    public const int Back = 0x08;
    public const int Tab = 0x09;
    public const int Return = 0x0D;
    public const int Shift = 0x10;
    public const int Control = 0x11;
    public const int Menu = 0x12;
    public const int Pause = 0x13;
    public const int Capital = 0x14;
    public const int Escape = 0x1B;
    public const int Space = 0x20;
    public const int Prior = 0x21;
    public const int Next = 0x22;
    public const int End = 0x23;
    public const int Home = 0x24;
    public const int Left = 0x25;
    public const int Up = 0x26;
    public const int Right = 0x27;
    public const int Down = 0x28;
    public const int Insert = 0x2D;
    public const int Delete = 0x2E;
    public const int Key0 = 0x30;
    public const int Key9 = 0x39;
    public const int KeyA = 0x41;
    public const int KeyW = 0x57;
    public const int KeyZ = 0x5A;
    public const int LeftWindows = 0x5B;
    public const int RightWindows = 0x5C;
    public const int Numpad0 = 0x60;
    public const int Numpad1 = 0x61;
    public const int Numpad2 = 0x62;
    public const int Numpad3 = 0x63;
    public const int Numpad4 = 0x64;
    public const int Numpad5 = 0x65;
    public const int Numpad6 = 0x66;
    public const int Numpad7 = 0x67;
    public const int Numpad8 = 0x68;
    public const int Numpad9 = 0x69;
    public const int Multiply = 0x6A;
    public const int Add = 0x6B;
    public const int Separator = 0x6C;
    public const int Subtract = 0x6D;
    public const int NumpadDecimal = 0x6E;
    public const int Divide = 0x6F;
    public const int F1 = 0x70;
    public const int F10 = 0x79;
    public const int F12 = 0x7B;
    public const int NumLock = 0x90;
    public const int ScrollLock = 0x91;
    public const int LeftShift = 0xA0;
    public const int RightShift = 0xA1;
    public const int LeftControl = 0xA2;
    public const int RightControl = 0xA3;
    public const int LeftMenu = 0xA4;
    public const int RightMenu = 0xA5;

    // Windows exposes both Enter keys as VK_RETURN. The input adapter assigns
    // this application-only value when scan code 0x1C has the extended flag.
    public const int NumpadEnter = 0x100;
}
