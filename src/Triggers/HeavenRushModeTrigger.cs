using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.HeavenRush.Triggers; 

[CustomEntity("heavenRush/heavenRushModeTrigger")]
public class HeavenRushModeTrigger : Trigger {
    private bool newValue;
    private bool coversScreen;
    
    public HeavenRushModeTrigger(EntityData data, Vector2 offset) : base(data, offset) {
        newValue = data.Bool("newValue");
        coversScreen = data.Bool("coversScreen");
    }

    public override void Added(Scene scene) {
        base.Added(scene);
        
        if (!coversScreen)
            return;

        var bounds = ((Level) scene).Bounds;
        
        Position = new Vector2(bounds.X, bounds.Y - 24f);
        Collider.Width = bounds.Width;
        Collider.Height = bounds.Height + 32f;
    }

    public override void OnEnter(Player player) {
        base.OnEnter(player);
        HeavenRushModule.Session.HeavenRushModeEnabled = newValue;
        Input.Grab.BufferTime = newValue ? 0.08f : 0f;
    }
}