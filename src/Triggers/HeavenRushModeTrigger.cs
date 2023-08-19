using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.HeavenRush.Triggers; 

[CustomEntity("heavenRush/heavenRushModeTrigger")]
public class HeavenRushModeTrigger : Trigger {
    private bool newValue;
    
    public HeavenRushModeTrigger(EntityData data, Vector2 offset) : base(data, offset) {
        newValue = data.Bool("newValue");
    }

    public override void OnEnter(Player player) {
        base.OnEnter(player);
        ((Level) Scene).Session.Inventory = new PlayerInventory(1, true, false, true);
        HeavenRushModule.Session.HeavenRushModeEnabled = newValue;
        Input.Grab.BufferTime = newValue ? 0.08f : 0f;
    }
}