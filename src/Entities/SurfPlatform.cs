using System;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.HeavenRush; 

[CustomEntity("heavenRush/surfPlatform"), Tracked]
public class SurfPlatform : Solid {
    private Water.Surface waterSurface;
    private Level level;

    public SurfPlatform(EntityData data, Vector2 offset) : base(data.Position + offset, data.Width, data.Height, true) {
        waterSurface = new Water.Surface(Position + new Vector2(0.5f * Width, 6f), -Vector2.UnitY, Width, Height);
        waterSurface.Rays.Clear();
        SurfaceSoundIndex = 0;
    }

    public override void Added(Scene scene) {
        base.Added(scene);
        level = scene as Level;
    }

    public override void Update() {
        base.Update();
        waterSurface.Update();
        
        var player = GetPlayerOnTop();

        if (player != null)
            waterSurface.DoRipple(player.Position, 0.04f * Math.Abs(player.Speed.X) * Engine.DeltaTime);
    }

    public override void Render() {
        Draw.Rect(X, Y + 6f, Width, Height - 6f, Water.FillColor);
        GameplayRenderer.End();
        waterSurface.Render(level.Camera);
        GameplayRenderer.Begin();
        base.Render();
    }
}