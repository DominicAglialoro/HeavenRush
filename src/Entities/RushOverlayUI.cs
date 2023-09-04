using System;
using System.Collections;
using System.Globalization;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.HeavenRush; 

[Tracked]
public class RushOverlayUI : Entity {
    private static readonly Vector2 SCREEN_CENTER = new(960f, 540f);
    private static readonly Vector2 SCREEN_BOTTOM_LEFT = new(0f, 1080f);
    private const float TIME_ANIM_DURATION = 0.7f;
    private const float FADE_ANIM_DURATION = 0.5f;

    private PixelFont font;
    private MTexture berryTexture;
    private bool showComplete;
    private string levelName;
    private string levelNumber;
    private long completionTime;
    private long bestTime;
    private long berryTime;
    private bool newBest;
    private int animPhase;
    private float timeAnim;
    private float fadeAnim;

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
            Text(font, GetTimeText(), right, new Vector2(1f, 0.5f), Vector2.One, newBest && animPhase >= 1 ? Color.LimeGreen : Color.White);
            
            if (animPhase == 0)
                return;

            float fade = Ease.CubeOut(fadeAnim / FADE_ANIM_DURATION);
            var offset = 80f * (1f - fade) * Vector2.UnitX;
            
            GetElementPositions(120f, out left, out right);
            Text(font, "Best:", left - offset, new Vector2(0f, 0.5f), Vector2.One, Color.White * fade);
            Text(font, ToTimeString(bestTime), right - offset, new Vector2(1f, 0.5f), Vector2.One, (newBest ? Color.LimeGreen : Color.White) * fade);
            
            GetElementPositions(200f, out left, out right);
            berryTexture.DrawJustified(left + 12f * Vector2.UnitX + offset, new Vector2(0f, 0.5f), Color.White * fade);
            Text(font, ToTimeString(berryTime), right + offset, new Vector2(1f, 0.5f), Vector2.One, (completionTime <= berryTime ? Color.LimeGreen : Color.White) * fade);
            
            var confirmTexture = Input.GuiButton(Input.MenuConfirm, Input.PrefixMode.Latest);
            var cancelTexture = Input.GuiButton(Input.MenuCancel, Input.PrefixMode.Latest);
            
            confirmTexture.DrawJustified(SCREEN_BOTTOM_LEFT + new Vector2(80f, -160f), new Vector2(0f, 0.5f), Color.White * fade);
            cancelTexture.DrawJustified(SCREEN_BOTTOM_LEFT + new Vector2(80f, -80f), new Vector2(0f, 0.5f), Color.White * fade);
            Text(ActiveFont.Font, "Continue", SCREEN_BOTTOM_LEFT + new Vector2(240f, -160f), new Vector2(0f, 0.5f), Vector2.One, Color.White * fade);
            Text(ActiveFont.Font, "Retry", SCREEN_BOTTOM_LEFT + new Vector2(240f, -80f), new Vector2(0f, 0.5f), Vector2.One, Color.White * fade);
        }
        else {
            Text(ActiveFont.Font, levelNumber, SCREEN_CENTER - 240f * Vector2.UnitY, new Vector2(0.5f, 0.5f), 0.8f * Vector2.One);
            Text(ActiveFont.Font, levelName, SCREEN_CENTER - 120f * Vector2.UnitY, new Vector2(0.5f, 0.5f), 2f * Vector2.One);

            Vector2 left;
            Vector2 right;
            float y = 80f;

            if (bestTime >= 0) {
                GetElementPositions(80f, out left, out right);
                Text(font, "Best:", left, new Vector2(0f, 0.5f), Vector2.One);
                Text(font, ToTimeString(bestTime), right, new Vector2(1f, 0.5f), Vector2.One);
                y += 80f;
            }

            GetElementPositions(y, out left, out right);
            berryTexture.DrawJustified(left + 12f * Vector2.UnitX, new Vector2(0f, 0.5f));
            Text(font, ToTimeString(berryTime), right, new Vector2(1f, 0.5f), Vector2.One);

            var confirmTexture = Input.GuiButton(Input.MenuConfirm, Input.PrefixMode.Latest);
            var talkTexture = Input.GuiButton(Input.Talk, Input.PrefixMode.Latest);
            
            confirmTexture.DrawJustified(SCREEN_BOTTOM_LEFT + new Vector2(80f, -160f), new Vector2(0f, 0.5f));
            talkTexture.DrawJustified(SCREEN_BOTTOM_LEFT + new Vector2(80f, -80f), new Vector2(0f, 0.5f));
            Text(ActiveFont.Font, "Start", SCREEN_BOTTOM_LEFT + new Vector2(240f, -160f), new Vector2(0f, 0.5f), Vector2.One);
            Text(ActiveFont.Font, "Look Around", SCREEN_BOTTOM_LEFT + new Vector2(240f, -80f), new Vector2(0f, 0.5f), Vector2.One);
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
        animPhase = 0;
        timeAnim = 0f;
        fadeAnim = 0f;
        Audio.Play(SFX.game_07_altitudecount);
        Add(new Coroutine(AnimCoroutine()));
    }

    public void Hide() {
        Active = false;
        Visible = false;
    }

    private string GetTimeText() {
        if (timeAnim >= TIME_ANIM_DURATION)
            return ToTimeString(completionTime);
        
        float normalized = timeAnim / TIME_ANIM_DURATION;
        long newTime;

        if (normalized >= 1f)
            newTime = completionTime;
        else
            newTime = (long) (normalized * completionTime);

        return ToTimeString(newTime);
    }

    private IEnumerator AnimCoroutine() {
        while (timeAnim < TIME_ANIM_DURATION) {
            timeAnim += Engine.RawDeltaTime;

            yield return null;
        }

        if (newBest)
            Audio.Play("event:/classic/sfx55");

        timeAnim = TIME_ANIM_DURATION;
        animPhase = 1;

        while (fadeAnim < FADE_ANIM_DURATION) {
            fadeAnim += Engine.RawDeltaTime;

            yield return null;
        }

        fadeAnim = FADE_ANIM_DURATION;
        animPhase = 2;
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