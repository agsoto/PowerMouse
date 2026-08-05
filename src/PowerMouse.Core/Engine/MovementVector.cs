namespace PowerMouse.Core.Engine;

public readonly record struct MovementVector(int X, int Y)
{
    public bool IsZero => X == 0 && Y == 0;
}

