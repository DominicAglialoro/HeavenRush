using System;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.HeavenRush; 

[CustomEntity("heavenRush/surfPlatform"), Tracked]
public class SurfPlatform : Solid {
    private DynamicWaterSurface waterSurface;
    private Level level;

    public SurfPlatform(EntityData data, Vector2 offset) : base(data.Position + offset, data.Width, data.Height, true) {
        waterSurface = new DynamicWaterSurface(Position - Vector2.UnitY, (int) Width, 9, 360f, 1300f, 1.25f);
        SurfaceSoundIndex = 0;
    }

    public override void Added(Scene scene) {
        base.Added(scene);
        level = scene as Level;
    }

    public override void Update() {
        base.Update();

        var player = GetPlayerOnTop();

        if (player != null && player.Speed.X != 0f)
            waterSurface.ApplyForce(player.Position.X, 7.5f * Math.Abs(player.Speed.X) * Engine.DeltaTime, 2);

        int cameraLeft = (int) level.Camera.Position.X;
        
        waterSurface.Update(cameraLeft, cameraLeft + 320, Engine.DeltaTime);
    }

    public override void Render() {
        base.Render();
        GameplayRenderer.End();
        waterSurface.Render(level.Camera);
        GameplayRenderer.Begin();
    }
}