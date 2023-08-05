namespace Celeste.Mod.HeavenRush;

public static class PlayerExtensions {
    public static Data Ext(this Player player) {
        return Extension<Player, Data>.Of(player);
    }
    
    public class Data {
        
    }
}