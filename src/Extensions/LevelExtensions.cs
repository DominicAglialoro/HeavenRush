using System;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.HeavenRush; 

public static class LevelExtensions {
    public static void WarpToNextLevel(this Level level) {
        level.OnEndOfFrame += () => {
            var player = level.Tracker.GetEntity<Player>();
            var facing = player.Facing;
            
            level.TeleportTo(player, level.GetNextLevel(), Player.IntroTypes.Transition);
            player.Speed = Vector2.Zero;
            player.StateMachine.State = 0;
            player.Dashes = 1;
            player.ClearRushData();
            player.Facing = facing;

            var tween = Tween.Create(Tween.TweenMode.Oneshot, null, 0.3f, true);

            tween.OnUpdate = tween => Glitch.Value = 0.5f * (1f - tween.Eased);
            player.Add(tween);
            Audio.Play(SFX.game_10_glitch_short);
        };
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