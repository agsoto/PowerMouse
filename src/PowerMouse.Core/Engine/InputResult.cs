namespace PowerMouse.Core.Engine;

public readonly record struct InputResult(bool Suppress, IReadOnlyList<MouseCommand> Commands)
{
    public static InputResult Pass { get; } = new(false, Array.Empty<MouseCommand>());

    public static InputResult Suppressed { get; } = new(true, Array.Empty<MouseCommand>());
}

