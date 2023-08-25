using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.HeavenRush; 

[CustomEntity("heavenRush/rushGoal"), Tracked]
public class RushGoal : Entity {
    private Image back;
    private Sprite crystal;
    private Sprite effect;
    private SineWave sine;
    private RushLevelController levelController;
    
    public RushGoal(EntityData data, Vector2 offset) : base(data.Position + offset) {
        Collider = new Hitbox(16f, 24f, -8f, -24f);
        Depth = 100;
        Collidable = false;
        
        var outline = new Image(GFX.Game["heavenRush/objects/rushGoal/outline"]);
        
        Add(outline);
        outline.JustifyOrigin(0.5f, 1f);
        
        Add(back = new Image(GFX.Game["heavenRush/objects/rushGoal/back"]));
        back.Color = (Color.White * 0.25f) with { A = 0 };
        back.JustifyOrigin(0.5f, 1f);
        back.Visible = false;

        Add(crystal = new Sprite(GFX.Game, "heavenRush/objects/rushGoal/crystal"));
        crystal.AddLoop("crystal", "", 0.5f);
        crystal.Play("crystal");
        crystal.CenterOrigin();
        
        Add(effect = new Sprite(GFX.Game, "heavenRush/objects/rushGoal/effect"));
        effect.AddLoop("effect", "", 0.1f);
        effect.Play("effect");
        effect.Color = (Color.White * 0.5f) with { A = 0 };
        effect.JustifyOrigin(0.5f, 1f);
        effect.Visible = false;
        
        Add(sine = new SineWave(0.3f));
        sine.Randomize();
        
        Add(new PlayerCollider(OnPlayer));

        crystal.Y = 8f + 2f * sine.Value;
        Tag = Tags.FrozenUpdate;
    }

    public override void Update() {
        base.Update();
        crystal.Y = -12f + sine.Value;
    }

    public override void Awake(Scene scene) {
        base.Awake(scene);
        levelController = Scene.Tracker.GetEntity<RushLevelController>();
        levelController.DemonKilled += OnDemonKilled;
    }
    private void OnPlayer(Player player) {
        Collidable = false;
        Audio.Play(SFX.game_07_checkpointconfetti, Position);
        levelController.GoalReached();
    }

    private void OnDemonKilled() {
        if (levelController.DemonCount > 0)
            return;
        
        Collidable = true;
        back.Visible = true;
        effect.Visible = true;
    }
}