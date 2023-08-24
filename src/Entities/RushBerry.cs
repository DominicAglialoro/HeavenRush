using System;
using System.Collections;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.HeavenRush; 

[CustomEntity("heavenRush/rushBerry"), RegisterStrawberry(true, false)]
public class RushBerry : Entity, IStrawberry {
    private EntityID id;
    private long objectiveTime;
    private bool alreadyAcquired;
    private Sprite sprite;
    private RushLevelController levelController;
    private bool collected;

    public RushBerry(EntityData data, Vector2 offset, EntityID gid) : base(data.Position + offset) {
        id = gid;
        objectiveTime = 10000 * data.Int("objectiveTime");
        alreadyAcquired = SaveData.Instance.CheckStrawberry(id);
        Depth = -100;
        
        Add(sprite = GFX.SpriteBank.Create(alreadyAcquired ? "ghostberry" : "strawberry"));
        
        if (alreadyAcquired)
            sprite.Color = Color.White * 0.8f;

        Tag = Tags.FrozenUpdate;
    }

    public override void Awake(Scene scene) {
        base.Awake(scene);
        levelController = scene.Tracker.GetEntity<RushLevelController>();
        levelController.LevelCleared += OnLevelCleared;
    }

    public override void Update() {
        base.Update();
        
        if (collected || levelController.Time <= objectiveTime)
            return;
        
        Audio.Play(SFX.game_gen_seed_poof, Position);

        for (int i = 0; i < 6; i++) {
            float angle = Calc.Random.NextFloat(MathHelper.TwoPi);
            
            SceneAs<Level>().ParticlesFG.Emit(StrawberrySeed.P_Burst, 1, Position + Calc.AngleToVector(angle, 4f), Vector2.Zero, angle);
        }
        
        RemoveSelf();
    }

    public void OnCollect() {
        collected = true;
        SaveData.Instance.AddStrawberry(id, false);
        
        var session = ((Level) Scene).Session;
        
        session.DoNotLoad.Add(id);
        session.Strawberries.Add(id);
        Add(new Coroutine(CollectRoutine()));
    }

    private void OnLevelCleared() {
        if (levelController.Time <= objectiveTime)
            OnCollect();
    }

    private IEnumerator CollectRoutine() {
        Audio.Play(SFX.game_gen_strawberry_get, Position, "colour", alreadyAcquired ? 1f : 0f);
        sprite.Play("collect");

        while (sprite.Animating)
            yield return null;
        
        RemoveSelf();
    }
}