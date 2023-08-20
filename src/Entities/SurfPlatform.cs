using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.HeavenRush; 

[CustomEntity("heavenRush/surfPlatform"), TrackedAs(typeof(Solid))]
public class SurfPlatform : Solid {
    private Player lastPlayer;
    
    public SurfPlatform(EntityData data, Vector2 offset) : base(data.Position + offset, data.Width, 8f, false) { }

    public override void Added(Scene scene) {
        base.Added(scene);

        var texture = GFX.Game["objects/surfPlatform/texture"];

        for (int x = 0; x < Width; x += 8) {
            var image = new Image(texture);

            image.Position = new Vector2(x + 4f, 4f);
            image.CenterOrigin();
            Add(image);
        }
    }

    public override void Update() {
        base.Update();

        var player = GetPlayerOnTop();

        if (player == lastPlayer)
            return;
        
        if (lastPlayer != null)
            lastPlayer.ExtData().GroundBoostSources--;

        if (player != null)
            player.ExtData().GroundBoostSources++;
            
        lastPlayer = player;
    }
}