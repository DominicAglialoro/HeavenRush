using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.HeavenRush; 

[CustomEntity("heavenRush/demon"), Tracked]
public class Demon : Entity {
    private const float REMOVE_AFTER_KILL_TIME = 0.033f;

    private static readonly ParticleBurst[] KILL_PARTICLES_LARGE = {
        CreateKillParticleLarge(0, new Vector2(-4f, 0f)),
        CreateKillParticleLarge(1, new Vector2(1f, 4f)),
        CreateKillParticleLarge(2, new Vector2(2f, 2f))
    };
    
    private static readonly ParticleBurst KILL_PARTICLE_SMALL = new(new ParticleType {
        Source = GFX.Game["particles/triangle"],
        Color = Color.White,
        Color2 = Color.Black,
        ColorMode = ParticleType.ColorModes.Fade,
        FadeMode = ParticleType.FadeModes.Late,
        LifeMin = 0.2f,
        LifeMax = 0.3f,
        Size = 1f,
        DirectionRange = 0.78f,
        SpeedMin = 20f,
        SpeedMax = 80f,
        SpeedMultiplier = 0.005f,
        RotationMode = ParticleType.RotationModes.Random,
        SpinMin = 1.5707964f,
        SpinMax = 4.712389f,
        SpinFlippedChance = true
    }) {
        Amount = 8,
        Range = 6f * Vector2.One
    };

    private static ParticleBurst CreateKillParticleLarge(int index, Vector2 offset) => new(new ParticleType {
        Source = GFX.Game[$"particles/heavenRush/demonShatter/shatter{index}"],
        Color = Color.White,
        Color2 = Color.Black,
        ColorMode = ParticleType.ColorModes.Fade,
        FadeMode = ParticleType.FadeModes.Late,
        LifeMin = 0.5f,
        LifeMax = 0.8f,
        Size = 1f,
        DirectionRange = 0.78f,
        SpeedMin = 140f,
        SpeedMax = 210f,
        SpeedMultiplier = 0.005f,
        SpinMin = 1.5707964f,
        SpinMax = 4.712389f,
        SpinFlippedChance = true
    }) { Offset = offset };

    public static bool KillInRadius(Scene scene, Vector2 center, float radius) {
        int killedCount = 0;
            
        foreach (var entity in scene.Tracker.GetEntities<Demon>()) {
            if (Vector2.DistanceSquared(center, entity.Center) > radius * radius)
                continue;

            var demon = (Demon) entity;
            float angle = (demon.Center - center).Angle();
            
            demon.Die(false);
            demon.Add(new Coroutine(Util.AfterFrame(() => demon.SpawnKillParticles(angle))));
            killedCount++;
        }

        if (killedCount == 0)
            return false;
        
        Audio.Play(SFX.game_09_iceball_break, center);
        scene.Tracker.GetEntity<RushLevelController>()?.DemonsKilled(killedCount);

        return true;
    }

    private bool restoresDash;
    private Sprite body;
    private Image outline;
    private Image eyes;
    private Image feet;
    private SineWave sine;
    private Level level;
    private bool alive = true;
    private Vector2 lastPlayerPosition;

    public Demon(EntityData data, Vector2 offset) : base(data.Position + offset) {
        restoresDash = data.Bool("restoresDash");

        Collider = new Circle(10f);
        Depth = 100;
        
        Add(body = new Sprite(GFX.Game, "objects/heavenRush/demon/body"));
        body.AddLoop("body", "", 0.1f);
        body.Play("body");
        body.CenterOrigin();
        
        Add(outline = new Image(GFX.Game["objects/heavenRush/demon/outline"]));
        outline.CenterOrigin();
        
        if (!restoresDash)
            outline.Color = Color.Cyan;
        
        Add(eyes = new Image(GFX.Game["objects/heavenRush/demon/eyes"]));
        eyes.CenterOrigin();

        if (data.Bool("grounded")) {
            feet = new Image(GFX.Game["objects/heavenRush/demon/feet"]);
            
            Add(feet);
            feet.CenterOrigin();
            
            if (!restoresDash)
                feet.Color = Color.Cyan;
        }
        
        Add(sine = new SineWave(0.6f));
        sine.Randomize();
        
        Add(new VertexLight(Color.White, 1f, 32, 64));
        Add(new PlayerCollider(OnPlayer));

        lastPlayerPosition = Position;
        UpdateVisual();
    }
    
    public override void Added(Scene scene) {
        base.Added(scene);
        level = scene as Level;
    }

    public override void Update() {
        base.Update();
        UpdateVisual();
    }

    private void OnPlayer(Player player) {
        if (alive && player.HitDemon()) {
            Celeste.Freeze(0.016f);
            Audio.Play(SFX.game_09_iceball_break, Center);
            Die(true);
            Add(new Coroutine(Util.AfterFrame(() => {
                var speed = player.Speed;

                if (speed == Vector2.Zero)
                    speed = player.DashDir;

                if (speed == Vector2.Zero)
                    SpawnKillParticles(player.Facing == Facings.Right ? 0f : MathHelper.Pi);
                else
                    SpawnKillParticles(speed.Angle());
            })));
            Scene.Tracker.GetEntity<RushLevelController>()?.DemonsKilled(1);
        }
        
        if (!alive && restoresDash && player.RefillDash())
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

    private void Die(bool allowPhantomRestore) {
        alive = false;

        if (!restoresDash || !allowPhantomRestore)
            Collidable = false;

        body.Stop();
        body.Texture = GFX.Game["objects/heavenRush/demon/shatter"];
        outline.Visible = false;

        if (feet != null)
            feet.Visible = false;

        Add(new Coroutine(Util.AfterTime(REMOVE_AFTER_KILL_TIME, RemoveSelf)));
    }

    private void SpawnKillParticles(float angle) {
        Visible = false;
        level.ParticlesFG.Emit(KILL_PARTICLES_LARGE[0], Position, angle);
        level.ParticlesFG.Emit(KILL_PARTICLES_LARGE[1], Position, angle);
        level.ParticlesFG.Emit(KILL_PARTICLES_LARGE[2], Position, angle);
        level.ParticlesFG.Emit(KILL_PARTICLE_SMALL, Position, angle);
    }
}