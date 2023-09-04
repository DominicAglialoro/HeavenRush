using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.HeavenRush; 

[Tracked]
public class DemonCounter : Entity {
    private const float TEXT_ANIM_DURATION = 0.25f;
    private const float EXIT_ANIM_DURATION = 0.5f;
    
    private Level level;
    private MTexture bg;
    private float textAnim = TEXT_ANIM_DURATION;
    private float exitAnim;
    private RushLevelController levelController;

    public DemonCounter() {
        bg = GFX.Gui["heavenRush/demonCounter/bg"];
        Depth = -101;
        Tag = Tags.HUD | Tags.FrozenUpdate;
    }

    public override void Awake(Scene scene) {
        level = (Level) scene;
        levelController = level.Tracker.GetEntity<RushLevelController>();
        levelController.DemonKilled += OnDemonKilled;

        if (scene.Tracker.CountEntities<Demon>() == 0)
            exitAnim = EXIT_ANIM_DURATION;
    }

    public override void Update() {
        base.Update();

        textAnim += Engine.DeltaTime;

        if (textAnim > TEXT_ANIM_DURATION)
            textAnim = TEXT_ANIM_DURATION;
        
        if (levelController.DemonCount > 0) {
            exitAnim = 0f;
            
            return;
        }

        exitAnim += Engine.DeltaTime;

        if (exitAnim > EXIT_ANIM_DURATION)
            exitAnim = EXIT_ANIM_DURATION;
    }

    public override void Render() {
        if (level.Paused || !levelController.RequireKillAllDemons)
            return;

        var position = new Vector2(MathHelper.Lerp(40f, -bg.Width, Ease.CubeIn(exitAnim / EXIT_ANIM_DURATION)), GetYPosition());

        bg.Draw(position);
        
        string text = levelController.DemonCount.ToString();
        var textPosition = position + new Vector2(180f, 42f);
        var textSize = MathHelper.Lerp(2.5f, 2f, Ease.QuadOut(textAnim / TEXT_ANIM_DURATION)) * Vector2.One;
        
        ActiveFont.Draw(text, textPosition - 8f * Vector2.One, 0.5f * Vector2.One, textSize, Color.Black);
        ActiveFont.DrawOutline(text, textPosition, 0.5f * Vector2.One, textSize, new Color(255, 0, 128), 2f, Color.Black);
    }

    private void OnDemonKilled() => textAnim = 0f;

    private float GetYPosition() {
        const float y = 50f;
        
        if (level.TimerHidden)
            return y;

        if (Settings.Instance.SpeedrunClock == SpeedrunType.Chapter)
            return y + 80f;
        
        if (Settings.Instance.SpeedrunClock == SpeedrunType.File)
            return y + 100f;
        
        return y;
    }
}