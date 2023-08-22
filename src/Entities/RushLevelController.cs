using System;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.HeavenRush; 

[CustomEntity("heavenRush/rushLevelController"), Tracked]
public class RushLevelController : Entity {
    public int DemonCount { get; private set; }

    public event Action DemonKilled;
    
    private StateMachine stateMachine;
    private long startedAtTime;
    private Level level;

    public override void Added(Scene scene) {
        base.Added(scene);

        Add(stateMachine = new StateMachine());
        stateMachine.SetCallbacks(0, AwaitingRespawnUpdate);
        stateMachine.SetCallbacks(1, GameplayUpdate);
        stateMachine.SetCallbacks(2, CompleteUpdate);

        Tag = Tags.FrozenUpdate;
        
        DemonCount = scene.Tracker.CountEntities<Demon>();
        level = scene as Level;
    }

    public void RespawnCompleted() => startedAtTime = level.Session.Time;

    public void DemonsKilled(int count) {
        if (DemonCount == 0)
            return;

        DemonCount -= count;

        if (DemonCount == 0) {
            Util.PlaySound("event:/classic/sfx13", 2f);
            Scene.Tracker.GetEntity<RushGoal>()?.Open();
        }
        else
            Util.PlaySound("event:/classic/sfx8", 2f);
        
        DemonKilled?.Invoke();
    }

    public void GoalReached() {
        level.Frozen = true;
        stateMachine.State = 2;
        Scene.Tracker.GetEntity<LevelCompleteUI>().Play(level.Session.Time - startedAtTime);
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

        return 1;
    }

    private int GameplayUpdate() => 1;

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