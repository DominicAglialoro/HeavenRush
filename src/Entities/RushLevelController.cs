using System;
using System.Collections;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.HeavenRush; 

[CustomEntity("heavenRush/rushLevelController"), Tracked]
public class RushLevelController : Entity {
    private const int AWAITING_RESPAWN = 0;
    private const int LOOK_AROUND = 1;
    private const int GAMEPLAY = 2;
    private const int COMPLETE = 3;
    
    public string LevelName { get; }
    
    public string LevelNumber { get; }

    public int RemainingDemonCount { get; private set; }

    public bool CanRetry => stateMachine.State == GAMEPLAY;

    public event Action LevelCompleted;

    public event Action DemonKilled;

    private StateMachine stateMachine;
    private Level level;
    private RushOverlayUI overlayUi;
    private bool requireKillAllDemons;
    private long berryObjectiveTime;
    private long bestTime;
    private long startedAtTime;
    private long completedAtTime;
    private bool berryFailed;
    private bool newBest;

    public RushLevelController(EntityData data, Vector2 offset) : base(data.Position + offset) {
        LevelName = data.Attr("levelName");
        LevelNumber = data.Attr("levelNumber");
        requireKillAllDemons = data.Bool("requireKillAllDemons");
        berryObjectiveTime = 10000 * data.Int("berryObjectiveTime");
        Tag = Tags.FrozenUpdate;
    }

    public override void Awake(Scene scene) {
        base.Awake(scene);
        level = (Level) scene;

        if (!HeavenRushModule.SaveData.BestTimes.TryGetValue(level.Session.Level, out bestTime))
            bestTime = -1;
        
        RemainingDemonCount = requireKillAllDemons ? level.Tracker.CountEntities<Demon>() : 0;
        overlayUi = level.Tracker.GetEntity<RushOverlayUI>();
        stateMachine = new StateMachine();
        stateMachine.SetCallbacks(AWAITING_RESPAWN, AwaitingRespawnUpdate, null, AwaitingRespawnBegin, AwaitingRespawnEnd);
        stateMachine.SetCallbacks(LOOK_AROUND, null, LookAroundCoroutine);
        stateMachine.SetCallbacks(GAMEPLAY, GameplayUpdate, null, GameplayBegin);
        stateMachine.SetCallbacks(COMPLETE, CompleteUpdate, null, CompleteBegin, CompleteEnd);
        Add(stateMachine);
    }

    public void StartTimer() => startedAtTime = TimeSpan.FromSeconds(level.RawTimeActive).Ticks;

    public void DemonsKilled(int count) {
        if (RemainingDemonCount == 0)
            return;

        RemainingDemonCount -= count;

        if (RemainingDemonCount <= 0) {
            RemainingDemonCount = 0;
            Util.PlaySound("event:/classic/sfx13", 2f);
        }
        else
            Util.PlaySound("event:/classic/sfx8", 2f);
        
        DemonKilled?.Invoke();
    }

    public void CompleteLevel() {
        completedAtTime = GetTime();
        level.Frozen = true;

        if (bestTime < 0 || completedAtTime < bestTime) {
            bestTime = completedAtTime;
            // HeavenRushModule.SaveData.BestTimes[level.Session.Level] = Time;
            newBest = true;
        }
        else
            newBest = false;
        
        if (completedAtTime <= berryObjectiveTime)
            level.Tracker.GetEntity<RushBerry>()?.OnCollect();
        
        LevelCompleted?.Invoke();
        stateMachine.State = COMPLETE;
    }

    private void AwaitingRespawnBegin() => overlayUi.ShowStart(LevelName, LevelNumber, bestTime, berryObjectiveTime);

    private int AwaitingRespawnUpdate() {
        var player = Scene.Tracker.GetEntity<Player>();

        if (player == null)
            return AWAITING_RESPAWN;

        if (player.Active)
            return GAMEPLAY;

        if (Input.MenuConfirm.Pressed) {
            player.Spawn();

            return GAMEPLAY;
        }
        
        if (Input.Talk.Pressed)
            return LOOK_AROUND;

        return AWAITING_RESPAWN;
    }

    private void AwaitingRespawnEnd() => overlayUi.Hide();

    private IEnumerator LookAroundCoroutine() {
        var camStart = level.Camera.Position;
        var camPosition = level.Camera.Position;
        var camSpeed = Vector2.Zero;

        Audio.Play(SFX.ui_game_lookout_on);

        while (!Input.MenuCancel.Pressed) {
            var aim = Input.Aim.Value;
            float accel = 800f * Engine.DeltaTime;

            camSpeed += accel * aim;

            if (aim.X == 0f)
                camSpeed.X = Calc.Approach(camSpeed.X, 0f, 2f * accel);
            
            if (aim.Y == 0f)
                camSpeed.Y = Calc.Approach(camSpeed.Y, 0f, 2f * accel);

            if (camSpeed.LengthSquared() > 57600f)
                camSpeed = camSpeed.SafeNormalize(240f);

            var bounds = level.Bounds;

            camPosition += camSpeed * Engine.DeltaTime;

            if (camPosition.X > bounds.Right - 320f) {
                camPosition.X = bounds.Right - 320f;
                camSpeed.X = 0;
            }
            else if (camPosition.X < bounds.Left) {
                camPosition.X = bounds.Left;
                camSpeed.X = 0;
            }
            
            if (camPosition.Y > bounds.Bottom - 180f) {
                camPosition.Y = bounds.Bottom - 180f;
                camSpeed.Y = 0;
            }
            else if (camPosition.Y < bounds.Top) {
                camPosition.Y = bounds.Top;
                camSpeed.Y = 0;
            }

            level.Camera.Position = camPosition;

            yield return null;
        }
        
        Audio.Play(SFX.ui_game_lookout_off);
        level.Camera.Position = camStart;
        stateMachine.State = AWAITING_RESPAWN;
    }

    private void GameplayBegin() => overlayUi.Hide();

    private int GameplayUpdate() {
        if (!berryFailed && GetTime() > berryObjectiveTime) {
            berryFailed = true;
            level.Tracker.GetEntity<RushBerry>()?.Pop();
        }

        if (HeavenRushModule.Settings.InstantRetry.Pressed) {
            Active = false;
            level.InstantRetry();
        }

        return GAMEPLAY;
    }

    private void CompleteBegin() => overlayUi.ShowComplete(completedAtTime, bestTime, berryObjectiveTime, newBest);

    private int CompleteUpdate() {
        if (Input.MenuConfirm.Pressed) {
            Active = false;
            level.GoToNextLevel();
        }
        else if (Input.MenuCancel.Pressed) {
            Active = false;
            level.ReplayLevel();
        }

        return COMPLETE;
    }

    private void CompleteEnd() => overlayUi.Hide();

    private long GetTime() => TimeSpan.FromSeconds(level.RawTimeActive).Ticks - startedAtTime;
}