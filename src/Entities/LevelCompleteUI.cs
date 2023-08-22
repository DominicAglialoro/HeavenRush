using System;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.HeavenRush; 

[Tracked]
public class LevelCompleteUI : Entity {
    private static readonly Vector2 SCREEN_CENTER = new(960f, 540f);
    private const float BOX_BORDER_WIDTH = 5f;
    private const float TEXT_PADDING = 16f;
    private const float ANIM_DURATION = 0.7f;

    private long time;
    private Vector2 headerBoxSize;
    private Vector2 timeBoxSize;
    private float anim;

    public LevelCompleteUI() => Tag = Tags.HUD | Tags.FrozenUpdate;

    public override void Added(Scene scene) {
        base.Added(scene);
        Active = false;
        Visible = false;
    }

    public override void Update() {
        base.Update();
        anim += Engine.DeltaTime;

        if (anim > ANIM_DURATION)
            anim = ANIM_DURATION;
    }

    public override void Render() {
        if (Scene.Paused)
            return;

        var headerBoxPosition = SCREEN_CENTER - 120f * Vector2.UnitY - 0.5f * headerBoxSize;
        var timeBoxPosition = SCREEN_CENTER + 120f * Vector2.UnitY - 0.5f * timeBoxSize;

        Draw.Rect(Vector2.Zero, 1920f, 1080f, Color.Black * 0.5f);
        Box(headerBoxPosition, headerBoxSize);
        Text("Level Complete!", headerBoxPosition, Vector2.Zero);
        Box(timeBoxPosition, timeBoxSize);
        Text("Time:", timeBoxPosition, Vector2.Zero);
        Text(GetTimeText(), timeBoxPosition + timeBoxSize.X * Vector2.UnitX, Vector2.UnitX);
    }

    public void Play(long time) {
        this.time = time;
        
        string finalTimeText = TimeSpan.FromTicks(time).ToString("mm\\:ss\\.fff");

        headerBoxSize = SizeOfText("Level Complete!");
        timeBoxSize = new Vector2(440f, SizeOfText($"Time: {finalTimeText}").Y);
        Active = true;
        Visible = true;
        anim = 0f;
        Audio.Play(SFX.game_07_altitudecount);
    }

    private string GetTimeText() {
        float normalized = anim / ANIM_DURATION;
        long newTime;

        if (normalized >= 1f)
            newTime = time;
        else
            newTime = (long) (normalized * time);

        return TimeSpan.FromTicks(newTime).ToString("mm\\:ss\\.fff");
    }

    private static void Box(Vector2 position, Vector2 size) {
        Draw.Rect(position, size.X, size.Y, Color.White);
        Draw.Rect(position + BOX_BORDER_WIDTH * Vector2.One, size.X - 2f * BOX_BORDER_WIDTH, size.Y - 2f * BOX_BORDER_WIDTH, Color.Black);
    }

    private static void Text(string text, Vector2 position, Vector2 justify) {
        ActiveFont.Draw(text, position + (Vector2.One - 2f * justify) * TEXT_PADDING, justify, Vector2.One, Color.White);
    }

    private static Vector2 SizeOfText(string text) => ActiveFont.Measure(text) + 2f * TEXT_PADDING * Vector2.One;
}