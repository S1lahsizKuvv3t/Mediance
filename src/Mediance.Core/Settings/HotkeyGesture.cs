namespace Mediance.Core.Settings;

[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Windows = 0x0008
}

public readonly record struct HotkeyGesture(HotkeyModifiers Modifiers, uint VirtualKey)
{
    public static HotkeyGesture Default { get; } = new(HotkeyModifiers.Control | HotkeyModifiers.Alt, 'M');

    public bool IsValid => Modifiers != HotkeyModifiers.None &&
        (Modifiers & ~(HotkeyModifiers.Alt | HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Windows)) == 0 &&
        VirtualKey is >= 0x08 and <= 0xFE &&
        VirtualKey is not 0x10 and not 0x11 and not 0x12 and not 0x1B and not 0x5B and not 0x5C;
}
