using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.HeavenRush; 

[CustomEntity("heavenRush/rushLevelController"), Tracked]
public class RushLevelController : Entity {
    private DemonCounter demonCounter;
    private int remainingDemonCount;
    private bool requireKillAllDemons;

    public RushLevelController(EntityData data, Vector2 offset) : base(data.Position + offset) {
        requireKillAllDemons = data.Bool("requireKillAllDemons");
        Tag = Tags.FrozenUpdate;
    }

    public override void Awake(Scene scene) {
        base.Awake(scene);
        remainingDemonCount = requireKillAllDemons ? Scene.Tracker.CountEntities<Demon>() : 0;

        if (remainingDemonCount > 0)
            Scene.Add(demonCounter = new DemonCounter(remainingDemonCount));
    }

    public void DemonsKilled(int count) {
        if (remainingDemonCount == 0)
            return;

        remainingDemonCount -= count;

        if (remainingDemonCount <= 0) {
            remainingDemonCount = 0;
            Util.PlaySound("event:/classic/sfx13", 2f);
            Scene.Tracker.GetEntity<RushGoal>()?.Activate();
        }
        else
            Util.PlaySound("event:/classic/sfx8", 2f);
        
        demonCounter?.UpdateDemonCount(remainingDemonCount);
    }
}