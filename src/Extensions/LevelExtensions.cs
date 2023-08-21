namespace Celeste.Mod.HeavenRush; 

public static class LevelExtensions {
    public static void Load() {
        On.Celeste.Level.LoadLevel += Level_LoadLevel;
    }

    public static void Unload() {
        On.Celeste.Level.LoadLevel -= Level_LoadLevel;
    }

    private static void Level_LoadLevel(On.Celeste.Level.orig_LoadLevel loadLevel, Level level, Player.IntroTypes playerintro, bool isfromloader) {
        loadLevel(level, playerintro, isfromloader);
        
        Input.Grab.BufferTime = HeavenRushModule.Session.HeavenRushModeEnabled ? 0.08f : 0f;

        if (level.Tracker.CountEntities<AbilityCard>() > 0 && level.Tracker.CountEntities<CardInventoryIndicator>() == 0)
            level.Add(new CardInventoryIndicator());

        if (level.Tracker.CountEntities<RushLevelController>() > 0)
            level.Add(new LevelCompleteUI());

        level.Entities.UpdateLists();
    }
}