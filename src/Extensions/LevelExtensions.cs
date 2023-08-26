namespace Celeste.Mod.HeavenRush; 

public static class LevelExtensions {
    public static void Load() {
        On.Celeste.Level.LoadCustomEntity += Level_LoadCustomEntity;
    }

    private static bool Level_LoadCustomEntity(On.Celeste.Level.orig_LoadCustomEntity loadCustomEntity, EntityData entityData, Level level) {
        if (!loadCustomEntity(entityData, level))
            return false;
        
        if (entityData.Name == "heavenRush/rushLevelController") {
            level.Add(new DemonCounter());
            level.Add(new RushOverlayUI());
        }

        return true;
    }

    public static void Unload() {
        On.Celeste.Level.LoadCustomEntity -= Level_LoadCustomEntity;
    }
}