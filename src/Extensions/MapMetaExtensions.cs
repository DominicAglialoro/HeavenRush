using On.Celeste.Mod.Meta;

namespace Celeste.Mod.HeavenRush; 

public static class MapMetaExtensions {
    public static void Load() => MapMeta.GetInventory += MapMeta_GetInventory;

    public static void Unload() => MapMeta.GetInventory += MapMeta_GetInventory;

    private static PlayerInventory? MapMeta_GetInventory(MapMeta.orig_GetInventory getInventory, string meta) {
        if (meta == "HeavenRush")
            return new PlayerInventory(1, true, false, true);

        return getInventory(meta);
    }
}