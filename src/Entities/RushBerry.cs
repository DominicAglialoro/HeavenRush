using System.Collections;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.HeavenRush; 

[CustomEntity("heavenRush/rushBerry"), RegisterStrawberry(true, false), Tracked]
public class RushBerry : Entity, IStrawberry {
    private EntityID id;
    private bool alreadyAcquired;
    private Sprite sprite;
    private Sprite points;

    public RushBerry(EntityData data, Vector2 offset, EntityID gid) : base(data.Position + offset) {
        id = gid;
        alreadyAcquired = SaveData.Instance.CheckStrawberry(id);
        Depth = -100;
        
        Add(sprite = GFX.SpriteBank.Create(alreadyAcquired ? "ghostberry" : "strawberry"));
        Add(points = GFX.SpriteBank.Create("strawberry"));
        points.Visible = false;
        
        if (alreadyAcquired)
            sprite.Color = Color.White * 0.8f;

        Tag = Tags.FrozenUpdate;
    }

    public void OnCollect() {
        SaveData.Instance.AddStrawberry(id, false);
        
        var session = ((Level) Scene).Session;
        
        session.DoNotLoad.Add(id);
        session.Strawberries.Add(id);
        Add(new Coroutine(CollectRoutine()));
    }

    public void Pop() {
        Audio.Play(SFX.game_gen_seed_poof, Position);

        for (int i = 0; i < 6; i++) {
            float angle = Calc.Random.NextFloat(MathHelper.TwoPi);
            
            SceneAs<Level>().ParticlesFG.Emit(StrawberrySeed.P_Burst, 1, Position + Calc.AngleToVector(angle, 4f), Vector2.Zero, angle);
        }
        
        RemoveSelf();
    }

    private IEnumerator CollectRoutine() {
        Audio.Play(SFX.game_gen_strawberry_get, Position, "colour", alreadyAcquired ? 1f : 0f);
        sprite.Play("collect");

        while (sprite.Animating)
            yield return null;

        sprite.Visible = false;
        points.Visible = true;
        points.Play("fade0");
        
        while (points.Animating)
            yield return null;
        
        RemoveSelf();
    }
}