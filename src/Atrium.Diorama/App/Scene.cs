using System.Numerics;
using Atrium.Core;
using Atrium.Diorama.Render;
using Atrium.Diorama.UI;
using Raylib_cs;

namespace Atrium.Diorama.App;

/// <summary>A full screen of the game: a 3D view plus its interface. Rules live in Atrium.Core, never here.</summary>
public abstract class Scene
{
    protected GameApp App { get; private set; } = null!;
    protected GameSession S => App.Session!;
    protected Renderer R => App.Renderer;
    protected Ui Ui => App.Ui;

    /// <summary>Music and ambience mood (hub, library, apothecary, pools, sanctum, title).</summary>
    public virtual string Mood => "hub";
    public virtual bool ShowHud => true;
    public virtual string? Locale => null;
    public Camera3D Camera;
    public virtual Color Backdrop => R.Light.Fog;

    internal void Attach(GameApp app) => App = app;

    public virtual void Enter() { }
    public virtual void Exit() { }

    /// <summary>World input and animation. Check <c>Ui.WantsMouse</c> before picking things in the world.</summary>
    public abstract void Update(float dt);

    /// <summary>Called inside BeginMode3D on the post-processed target.</summary>
    public virtual void Draw3D() { }

    /// <summary>Interface in virtual 1600×900 space.</summary>
    public virtual void DrawUI() { }

    /// <summary>Called when Esc is pressed with no overlay open. Return true if handled.</summary>
    public virtual bool Back() => false;

    protected static Camera3D Orbit(Vector3 target, float yaw, float pitch, float dist, float fov = 32)
    {
        var dir = new Vector3(MathF.Sin(yaw) * MathF.Cos(pitch), MathF.Sin(pitch), MathF.Cos(yaw) * MathF.Cos(pitch));
        return new Camera3D(target + dir * dist, target, Vector3.UnitY, fov, CameraProjection.Perspective);
    }
}

/// <summary>A modal layer over the scene (story cards, pickers, the journal, the reader…).</summary>
public abstract class Overlay
{
    public bool Closed { get; protected set; }
    protected GameApp App { get; private set; } = null!;
    protected Ui Ui => App.Ui;
    protected GameSession S => App.Session!;
    internal void Attach(GameApp app) => App = app;
    public void Close() => Closed = true;

    /// <summary>Time since the overlay opened (for entrance animations).</summary>
    public float Age { get; internal set; }

    public abstract void Draw();

    /// <summary>Esc pressed while this overlay is on top.</summary>
    public virtual void Back() => Close();

    /// <summary>A one-line description for capture logs.</summary>
    public virtual string Describe() => GetType().Name;
}

/// <summary>An overlay offering a list of choices (captures can choose one directly).</summary>
public interface IChooser
{
    IReadOnlyList<string> Labels { get; }
    void Choose(int index);
}
