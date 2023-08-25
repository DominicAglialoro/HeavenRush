using System;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.HeavenRush; 

[CustomEntity("heavenRush/surfPlatform"), Tracked]
public class SurfPlatform : Solid {
    private Water.Surface waterSurface;
    private Level level;

    public SurfPlatform(EntityData data, Vector2 offset) : base(data.Position + offset, data.Width, 8f, false) {
        var texture = GFX.Game["objects/heavenRush/surfPlatform/texture"];

        for (int x = 0; x < Width; x += 8) {
            var image = new Image(texture);

            image.Position = new Vector2(x + 4f, 4f);
            image.CenterOrigin();
            Add(image);
        }

        waterSurface = new Water.Surface(Position + new Vector2(0.5f * Width, 6f), -Vector2.UnitY, Width, 0f);
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
        GameplayRenderer.End();
        waterSurface.Render(level.Camera);
        GameplayRenderer.Begin();
        base.Render();
    }
}