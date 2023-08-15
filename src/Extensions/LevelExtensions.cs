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
        level.Add(new CardInventoryIndicator());
        level.Entities.UpdateLists();
    }
}