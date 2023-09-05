using System;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.HeavenRush; 

public static class LevelExtensions {
    public static void Load() => On.Celeste.Level.LoadCustomEntity += Level_LoadCustomEntity;

    private static bool Level_LoadCustomEntity(On.Celeste.Level.orig_LoadCustomEntity loadCustomEntity, EntityData entityData, Level level) {
        if (!loadCustomEntity(entityData, level))
            return false;
        
        if (entityData.Name == "heavenRush/rushLevelController") {
            level.Add(new DemonCounter());
            level.Add(new RushOverlayUI());
        }

        return true;
    }

    public static void Unload() => On.Celeste.Level.LoadCustomEntity -= Level_LoadCustomEntity;

    public static void InstantRetry(this Level level) {
        level.OnEndOfFrame += () => {
            var session = level.Session;
            
            session.Deaths++;
            session.DeathsInCurrentLevel++;
            SaveData.Instance.AddDeath(session.Area);
            level.LoadLevel(null);
            level.Wipe?.Cancel();
        };
    }

    public static void ReplayLevel(this Level level)
        => level.DoScreenWipe(false, () => level.LoadLevel(null));

    public static void GoToNextLevel(this Level level)
        => level.DoScreenWipe(false, () => level.LoadLevel(level.GetNextLevel()));

    private static void LoadLevel(this Level level, string levelName) {
        level.Entities.UpdateLists();
        level.Displacement.Clear();
        level.ParticlesBG.Clear();
        level.Particles.Clear();
        level.ParticlesFG.Clear();
        TrailManager.Clear();
        level.Wipe?.Cancel();
        
        foreach (var player in level.Tracker.GetEntitiesCopy<Player>())
            player.RemoveSelf();
        
        level.UnloadLevel();
        GC.Collect();
        GC.WaitForPendingFinalizers();

        var session = level.Session;

        if (levelName != null)
            session.Level = levelName;
        
        session.RespawnPoint = level.GetSpawnPoint(new Vector2(level.Bounds.Left, level.Bounds.Top));
        level.LoadLevel(Player.IntroTypes.Respawn);
        level.Entities.UpdateLists();

        var newPlayer = level.Tracker.GetEntity<Player>();

        if (newPlayer != null)
            level.Camera.Position = newPlayer.CameraTarget;
        
        level.Update();
    }

    private static string GetNextLevel(this Level level) {
        var session = level.Session;
        var levels = session.MapData.Levels;
        int index = levels.IndexOf(session.LevelData);

        if (index < levels.Count - 1)
            index++;

        return levels[index].Name;
    }
}