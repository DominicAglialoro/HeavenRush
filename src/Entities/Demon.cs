using System.Collections;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.HeavenRush; 

[CustomEntity("heavenRush/demon"), Tracked]
public class Demon : Entity {
    private const float REMOVE_AFTER_KILL_TIME = 0.033f;

    private ParticleType KILL_PARTICLE_LARGE = new() {
        Source = GFX.Game["particles/triangle"],
        Color = Color.Gray,
        Color2 = Color.Black,
        ColorMode = ParticleType.ColorModes.Fade,
        FadeMode = ParticleType.FadeModes.Late,
        LifeMin = 0.5f,
        LifeMax = 0.8f,
        Size = 2f,
        Direction = 0f,
        DirectionRange = 0.78f,
        SpeedMin = 140f,
        SpeedMax = 210f,
        SpeedMultiplier = 0.005f,
        RotationMode = ParticleType.RotationModes.Random,
        SpinMin = 1.5707964f,
        SpinMax = 4.712389f,
        SpinFlippedChance = true
    };
    
    private ParticleType KILL_PARTICLE_SMALL = new() {
        Color = Color.Gray,
        Color2 = Color.Black,
        ColorMode = ParticleType.ColorModes.Choose,
        FadeMode = ParticleType.FadeModes.Late,
        LifeMin = 0.5f,
        LifeMax = 0.8f,
        Size = 1f,
        DirectionRange = 1.5f,
        SpeedMin = 20f,
        SpeedMax = 80f,
        SpeedMultiplier = 0.005f
    };
    
    private bool grounded;
    private Sprite body;
    private Image outline;
    private Image eyes;
    private SineWave sine;
    private Level level;
    private bool alive = true;
    private Vector2 lastPlayerPosition;

    public Demon(EntityData data, Vector2 offset) : base(data.Position + offset) {
        grounded = data.Bool("grounded");

        Collider = new Circle(8f);
        
        Add(body = new Sprite(GFX.Game, "objects/demon/body"));
        body.AddLoop("body", "", 0.1f);
        body.Play("body");
        body.CenterOrigin();
        
        Add(outline = new Image(GFX.Game["objects/demon/outline"]));
        outline.CenterOrigin();
        
        Add(eyes = new Image(GFX.Game["objects/demon/eyes"]));
        eyes.CenterOrigin();

        if (grounded) {
            var feet = new Image(GFX.Game["objects/demon/feet"]);
            
            Add(feet);
            feet.CenterOrigin();
        }
        
        Add(sine = new SineWave(0.6f));
        sine.Randomize();
        
        Add(new PlayerCollider(OnPlayer));

        lastPlayerPosition = Position;
        UpdateVisual();
    }
    
    public override void Added(Scene scene) {
        base.Added(scene);
        level = SceneAs<Level>();
    }

    public override void Update() {
        base.Update();
        UpdateVisual();

        if (!alive && !CollideCheck<Player>())
            Collidable = false;
    }

    public void Die(float angle) {
        alive = false;
        Visible = false;
        Add(new Coroutine(KilledCoroutine(angle)));
    }
    
    private void Die(Player player) {
        alive = false;
        Visible = false;
        Add(new Coroutine(KilledCoroutine(player)));
    }

    private void OnPlayer(Player player) {
        if (alive && player.HitDemon()) {
            Celeste.Freeze(0.016f);
            Audio.Play(SFX.game_09_iceball_break, Position);
            Die(player);
        }
        
        if (!alive && player.RefillDash())
            Collidable = false;
    }

    private void UpdateVisual() {
        body.Y = outline.Y = sine.Value;

        var eyesOrigin = new Vector2(0f, sine.Value - 1f);
        var player = Scene?.Tracker.GetEntity<Player>();

        if (player != null)
            lastPlayerPosition = player.Position;

        if (lastPlayerPosition != Position)
            eyes.Position = eyesOrigin + Vector2.Normalize(lastPlayerPosition - Position).Round();
        else
            eyes.Position = eyesOrigin;
    }

    private void SpawnKillParticles(float angle) {
        level.ParticlesFG.Emit(KILL_PARTICLE_LARGE, 2, Position, 4f * Vector2.One, angle);
        level.ParticlesFG.Emit(KILL_PARTICLE_SMALL, 20, Position, 6f * Vector2.One, angle);
    }

    private IEnumerator KilledCoroutine(float angle) {
        yield return null;
        
        SpawnKillParticles(angle);

        yield return REMOVE_AFTER_KILL_TIME;
        
        RemoveSelf();
    }
    
    private IEnumerator KilledCoroutine(Player player) {
        yield return null;
        
        var speed = player.Speed;

        if (speed != Vector2.Zero)
            SpawnKillParticles(speed.Angle());
        else
            SpawnKillParticles(player.Facing == Facings.Right ? 0f : MathHelper.Pi);

        yield return REMOVE_AFTER_KILL_TIME;
        
        RemoveSelf();
    }
}