using System;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.HeavenRush; 

[CustomEntity("heavenRush/rushLevelController"), Tracked]
public class RushLevelController : Entity {
    public string LevelName { get; }
    
    public bool RequireKillAllDemons { get; }
    
    public long BestTime { get; private set; }
    
    public long BerryObjectiveTime { get; }

    public long Time => startedAtTime >= 0 ? level.Session.Time - startedAtTime : 0;
    
    public int DemonCount { get; private set; }

    public event Action LevelCleared;

    public event Action DemonKilled;

    private StateMachine stateMachine;
    private Level level;
    private RushOverlayUI overlayUi;
    private long startedAtTime = -1;
    private bool berryFailed;

    public RushLevelController(EntityData data, Vector2 offset) : base(data.Position + offset) {
        LevelName = data.Attr("levelName");
        RequireKillAllDemons = data.Bool("requireKillAllDemons");
        BerryObjectiveTime = 10000 * data.Int("berryObjectiveTime");
        
        Add(stateMachine = new StateMachine());
        stateMachine.SetCallbacks(0, AwaitingRespawnUpdate);
        stateMachine.SetCallbacks(1, GameplayUpdate);
        stateMachine.SetCallbacks(2, CompleteUpdate);
        Tag = Tags.FrozenUpdate;
    }

    public override void Awake(Scene scene) {
        base.Awake(scene);
        level = (Level) scene;

        if (HeavenRushModule.SaveData.BestTimes.TryGetValue(level.Session.Level, out long bestTime))
            BestTime = bestTime;
        else
            BestTime = -1;
        
        DemonCount = level.Tracker.CountEntities<Demon>();
    }

    public void Init(RushOverlayUI overlayUi) {
        this.overlayUi = overlayUi;
        overlayUi.ShowStart(LevelName, BestTime, BerryObjectiveTime);
    }

    public void RespawnCompleted() => startedAtTime = level.Session.Time;

    public void DemonsKilled(int count) {
        if (DemonCount == 0)
            return;

        DemonCount -= count;

        if (DemonCount == 0)
            Util.PlaySound("event:/classic/sfx13", 2f);
        else
            Util.PlaySound("event:/classic/sfx8", 2f);
        
        DemonKilled?.Invoke();
    }

    public void GoalReached() {
        level.Frozen = true;
        stateMachine.State = 2;

        bool newBest = false;

        if (BestTime < 0 || Time < BestTime) {
            BestTime = Time;
            HeavenRushModule.SaveData.BestTimes[level.Session.Level] = Time;
            newBest = true;
        }

        level.Tracker.GetEntity<RushOverlayUI>().ShowComplete(Time, BestTime, BerryObjectiveTime, newBest);
        
        if (Time <= BerryObjectiveTime)
            level.Tracker.GetEntity<RushBerry>()?.OnCollect();
        
        LevelCleared?.Invoke();
    }

    private int AwaitingRespawnUpdate() {
        var player = Scene.Tracker.GetEntity<Player>();

        if (player == null)
            return 0;

        if (player.Active)
            return 1;
        
        if (!Input.MenuConfirm.Pressed)
            return 0;
        
        player.Spawn();
        overlayUi.Hide();

        return 1;
    }

    private int GameplayUpdate() {
        if (!berryFailed && Time > BerryObjectiveTime) {
            berryFailed = true;
            level.Tracker.GetEntity<RushBerry>()?.Pop();
        }
        
        if (!HeavenRushModule.Settings.InstantRetry.Pressed)
            return 1;

        level.OnEndOfFrame += () => {
            var session = level.Session;
            
            session.Deaths++;
            session.DeathsInCurrentLevel++;
            SaveData.Instance.AddDeath(session.Area);
            
            foreach (var player in level.Tracker.GetEntitiesCopy<Player>())
                player.RemoveSelf();
            
            level.Reload();
            level.Wipe.Cancel();
        };

        Active = false;

        return 1;
    }

    private int CompleteUpdate() {
        if (Input.MenuConfirm.Pressed) {
            level.DoScreenWipe(false, () => {
                foreach (var player in level.Tracker.GetEntitiesCopy<Player>())
                    player.RemoveSelf();
                
                var session = level.Session;
                var levels = session.MapData.Levels;
                int index = levels.IndexOf(session.LevelData) + 1;

                if (index >= levels.Count) {
                    level.Reload();
                    
                    return;
                }

                level.UnloadLevel();
                session.Level = levels[index].Name;
                session.RespawnPoint = level.GetSpawnPoint(new Vector2(level.Bounds.Left, level.Bounds.Top));
                level.LoadLevel(Player.IntroTypes.Respawn);
            });
        }
        else if (Input.MenuCancel.Pressed) {
            level.DoScreenWipe(false, () => {
                foreach (var player in level.Tracker.GetEntitiesCopy<Player>())
                    player.RemoveSelf();
                
                level.Reload();
            });
        }
        else
            return 2;

        Active = false;

        return 2;
    }
}