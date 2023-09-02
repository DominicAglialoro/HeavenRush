using System;
using System.Collections;
using System.Globalization;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.HeavenRush; 

[Tracked]
public class RushOverlayUI : Entity {
    private static readonly Vector2 SCREEN_CENTER = new(960f, 540f);
    private const float ANIM_DURATION = 0.7f;

    private PixelFont font;
    private MTexture berryTexture;
    private bool showComplete;
    private string levelName;
    private string levelNumber;
    private long completionTime;
    private long bestTime;
    private long berryTime;
    private bool newBest;
    private float timeAnim;
    private bool animComplete;

    public RushOverlayUI() {
        Logger.Log(LogLevel.Warn, "", ActiveFont.BaseSize.ToString(CultureInfo.InvariantCulture));
        font = Fonts.Load("Monofonto");
        berryTexture = GFX.Gui["collectables/strawberry"];
        Tag = Tags.HUD | Tags.FrozenUpdate;
        Active = false;
        Visible = false;
    }

    public override void Render() {
        if (Scene.Paused)
            return;
        
        Draw.Rect(Vector2.Zero, 1920f, 1080f, Color.Black * 0.5f);

        if (showComplete) {
            Text(ActiveFont.Font, "Level Complete!", SCREEN_CENTER - 120f * Vector2.UnitY, new Vector2(0.5f, 0.5f), 2f * Vector2.One);
            
            GetElementPositions(40f, out var left, out var right);
            Text(font, "Time:", left, new Vector2(0f, 0.5f), Vector2.One);
            Text(font, GetTimeText(), right, new Vector2(1f, 0.5f), Vector2.One, newBest && animComplete ? Color.LimeGreen : Color.White);
            
            if (!animComplete)
                return;
            
            GetElementPositions(120f, out left, out right);
            Text(font, "Best:", left, new Vector2(0f, 0.5f), Vector2.One);
            Text(font, ToTimeString(bestTime), right, new Vector2(1f, 0.5f), Vector2.One, newBest ? Color.LimeGreen : Color.White);
            
            GetElementPositions(200f, out left, out right);
            berryTexture.DrawJustified(left + 12f * Vector2.UnitX, new Vector2(0f, 0.5f));
            Text(font, ToTimeString(berryTime), right, new Vector2(1f, 0.5f), Vector2.One, completionTime <= berryTime ? Color.LimeGreen : Color.White);
        }
        else {
            Text(ActiveFont.Font, levelNumber, SCREEN_CENTER - 240f * Vector2.UnitY, new Vector2(0.5f, 0.5f), 0.8f * Vector2.One);
            Text(ActiveFont.Font, levelName, SCREEN_CENTER - 120f * Vector2.UnitY, new Vector2(0.5f, 0.5f), 2f * Vector2.One);
            
            GetElementPositions(80f, out var left, out var right);
            Text(font, "Best:", left, new Vector2(0f, 0.5f), Vector2.One);
            Text(font, ToTimeString(bestTime), right, new Vector2(1f, 0.5f), Vector2.One);

            GetElementPositions(160f, out left, out right);
            berryTexture.DrawJustified(left + 12f * Vector2.UnitX, new Vector2(0f, 0.5f));
            Text(font, ToTimeString(berryTime), right, new Vector2(1f, 0.5f), Vector2.One);
        }
    }

    public void ShowStart(string levelName, string levelNumber, long bestTime, long berryTime) {
        this.levelName = levelName;
        this.levelNumber = levelNumber;
        this.bestTime = bestTime;
        this.berryTime = berryTime;
        Active = true;
        Visible = true;
    }

    public void ShowComplete(long completionTime, long bestTime, long berryTime, bool newBest) {
        showComplete = true;
        this.completionTime = completionTime;
        this.bestTime = bestTime;
        this.berryTime = berryTime;
        this.newBest = newBest;
        Active = true;
        Visible = true;
        timeAnim = 0f;
        animComplete = false;
        Audio.Play(SFX.game_07_altitudecount);
        Add(new Coroutine(AnimCoroutine()));
    }

    public void Hide() {
        Active = false;
        Visible = false;
    }

    private string GetTimeText() {
        if (animComplete)
            return ToTimeString(completionTime);
        
        float normalized = timeAnim / ANIM_DURATION;
        long newTime;

        if (normalized >= 1f)
            newTime = completionTime;
        else
            newTime = (long) (normalized * completionTime);

        return ToTimeString(newTime);
    }

    private IEnumerator AnimCoroutine() {
        while (timeAnim < ANIM_DURATION) {
            timeAnim += Engine.RawDeltaTime;

            yield return null;
        }

        if (newBest)
            Audio.Play("event:/classic/sfx55");

        timeAnim = ANIM_DURATION;
        animComplete = true;
    }

    private static void GetElementPositions(float y, out Vector2 left, out Vector2 right) {
        left = SCREEN_CENTER + new Vector2(-220f, y);
        right = SCREEN_CENTER + new Vector2(220f, y);
    }

    private static void Text(PixelFont font, string text, Vector2 position, Vector2 justify, Vector2 scale)
        => font.Draw(64f, text, position, justify, scale, Color.White);
    
    private static void Text(PixelFont font, string text, Vector2 position, Vector2 justify, Vector2 scale, Color color)
        => font.Draw(64f, text, position, justify, scale, color);

    private static string ToTimeString(long time) => TimeSpan.FromTicks(time).ToString("m\\:ss\\.fff");
}