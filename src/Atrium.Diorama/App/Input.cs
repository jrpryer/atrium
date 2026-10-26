using System.Numerics;
using Raylib_cs;

namespace Atrium.Diorama.App;

/// <summary>
/// All input goes through here, never straight to raylib, so scripted captures can drive the game.
/// Mouse positions are in virtual UI space (1600×900) unless stated otherwise.
/// </summary>
public static class Input
{
    public static bool Scripted { get; set; }

    /// <summary>While true (an overlay is open), the world under it animates but receives no input.</summary>
    public static bool Blocked { get; set; }

    // scripted state
    private static Vector2 _sMouse;
    private static bool _sDown, _sPressed, _sReleased, _sRightDown;
    private static float _sWheel;
    private static Vector2 _sRightDelta;
    private static readonly HashSet<KeyboardKey> _sKeys = new();

    public static float UiScale { get; set; } = 1;
    /// <summary>Screen-space offset of the letterboxed 1600×900 interface.</summary>
    public static Vector2 UiOffset { get; set; }

    public static Vector2 ScreenMouse => Scripted ? _sMouse * UiScale + UiOffset : Raylib.GetMousePosition();
    public static Vector2 Mouse => (ScreenMouse - UiOffset) / UiScale;

    /// <summary>Converts a virtual-space rectangle to screen pixels (for scissoring).</summary>
    public static Raylib_cs.Rectangle ToScreen(Raylib_cs.Rectangle r) =>
        new(r.X * UiScale + UiOffset.X, r.Y * UiScale + UiOffset.Y, r.Width * UiScale, r.Height * UiScale);

    public static bool Down => !Blocked && (Scripted ? _sDown : (bool)Raylib.IsMouseButtonDown(MouseButton.Left));
    public static bool Pressed => !Blocked && (Scripted ? _sPressed : (bool)Raylib.IsMouseButtonPressed(MouseButton.Left));
    public static bool Released => !Blocked && (Scripted ? _sReleased : (bool)Raylib.IsMouseButtonReleased(MouseButton.Left));
    public static bool RightDown => !Blocked && (Scripted ? _sRightDown : (bool)Raylib.IsMouseButtonDown(MouseButton.Right));
    public static Vector2 RightDelta => Blocked ? Vector2.Zero : Scripted ? _sRightDelta : (Raylib.IsMouseButtonDown(MouseButton.Right) ? Raylib.GetMouseDelta() : Vector2.Zero);
    public static float Wheel => Blocked ? 0 : Scripted ? _sWheel : Raylib.GetMouseWheelMove();

    public static bool KeyPressed(KeyboardKey k) => !Blocked && (Scripted ? _sKeys.Contains(k) : (bool)Raylib.IsKeyPressed(k));
    public static bool KeyDown(KeyboardKey k) => !Blocked && !Scripted && Raylib.IsKeyDown(k);

    /// <summary>Set by the frame loop: true when something already used this frame's click.</summary>
    public static bool ClickConsumed { get; set; }

    /// <summary>A left click (press) not already taken by the interface.</summary>
    public static bool Click => Pressed && !ClickConsumed;

    // ───── scripted control ─────

    public static void ScriptMove(Vector2 virtualPos) => _sMouse = virtualPos;
    public static void ScriptPress() { _sDown = true; _sPressed = true; }
    public static void ScriptRelease() { _sDown = false; _sReleased = true; }
    public static void ScriptKey(KeyboardKey k) => _sKeys.Add(k);
    public static void ScriptWheel(float w) => _sWheel = w;
    public static void ScriptOrbit(float dx) { _sRightDown = true; _sRightDelta = new Vector2(dx, 0); }

    /// <summary>Clears one-frame scripted events (called at the end of every frame).</summary>
    public static void EndFrame()
    {
        _sPressed = _sReleased = false;
        _sWheel = 0;
        _sKeys.Clear();
        _sRightDown = false;
        _sRightDelta = Vector2.Zero;
        ClickConsumed = false;
    }
}

/// <summary>Easing and a critically-damped spring for juice.</summary>
public static class Ease
{
    public static float OutCubic(float t) { t = Math.Clamp(t, 0, 1); return 1 - MathF.Pow(1 - t, 3); }
    public static float InOutSine(float t) => -(MathF.Cos(MathF.PI * Math.Clamp(t, 0, 1)) - 1) / 2;
    public static float OutBack(float t, float s = 1.7f) { t = Math.Clamp(t, 0, 1) - 1; return t * t * ((s + 1) * t + s) + 1; }
    public static float OutElastic(float t)
    {
        t = Math.Clamp(t, 0, 1);
        return t == 0 || t == 1 ? t : MathF.Pow(2, -10 * t) * MathF.Sin((t * 10 - 0.75f) * (MathF.Tau / 3)) + 1;
    }

    /// <summary>Exponential approach (frame-rate independent smoothing).</summary>
    public static float Approach(float current, float target, float rate, float dt) => target + (current - target) * MathF.Exp(-rate * dt);
    public static Vector3 Approach(Vector3 current, Vector3 target, float rate, float dt) => target + (current - target) * MathF.Exp(-rate * dt);
}

/// <summary>A damped spring value, for wobbles and settles.</summary>
public struct Spring
{
    public float Value, Velocity, Target;
    public float Stiffness, Damping;

    public Spring(float v, float stiffness = 180, float damping = 14) { Value = Target = v; Velocity = 0; Stiffness = stiffness; Damping = damping; }

    public void Update(float dt)
    {
        dt = MathF.Min(dt, 1 / 30f);
        float a = (Target - Value) * Stiffness - Velocity * Damping;
        Velocity += a * dt;
        Value += Velocity * dt;
    }

    public void Kick(float v) => Velocity += v;
}
