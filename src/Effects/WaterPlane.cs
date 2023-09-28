using System;
using Celeste.Mod.Backdrops;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.HeavenRush; 

[CustomBackdrop("heavenRush/waterPlane")]
public class WaterPlane : Backdrop {
    private MTexture texture;
    private Wave[] waves;
    private int nearY;
    private int farY;
    private float nearScrollY;
    private float farScrollY;
    private float time;

    public WaterPlane(BinaryPacker.Element data) {
        texture = GFX.Game[data.Attr("texture")];
        nearY = data.AttrInt("nearY");
        farY = data.AttrInt("farY");
        nearScrollY = data.AttrFloat("nearScrollY");
        farScrollY = data.AttrFloat("farScrollY");

        float waveNearDensity = data.AttrFloat("waveNearDensity");
        float waveFarDensity = data.AttrFloat("waveFarDensity");
        float waveNearScroll = data.AttrFloat("waveNearScroll");
        float waveFarScroll = data.AttrFloat("waveFarScroll");
        float waveNearSpeed = data.AttrFloat("waveNearSpeed");
        float waveFarSpeed = data.AttrFloat("waveFarSpeed");
        int waveNearWidth = data.AttrInt("waveNearWidth");
        int waveFarWidth = data.AttrInt("waveFarWidth");
        var waveNearColor = Calc.HexToColor(data.Attr("waveNearColor")) with { A = 0 };
        var waveFarColor = Calc.HexToColor(data.Attr("waveFarColor")) with { A = 0 };
        float waveSpeedRandom = data.AttrFloat("waveSpeedRandom");
        float waveWidthRandom = data.AttrFloat("waveWidthRandom");
        float waveAlphaRandom = data.AttrFloat("waveAlphaRandom");

        bool flat = Math.Abs(waveFarDensity - waveNearDensity) < 0.001f;
        float a = (waveFarDensity + waveNearDensity) * (waveFarDensity - waveNearDensity);
        float b = waveNearDensity * waveNearDensity;
        float c = flat ? 1f : 1f / (waveFarDensity - waveNearDensity);

        waves = new Wave[(int) (waveNearDensity + waveFarDensity) / 2];

        for (int i = 0; i < waves.Length; i++) {
            float rand = Calc.Random.NextFloat();
            float depth = flat ? rand : MathHelper.Clamp(((float) Math.Sqrt(a * rand + b) - waveNearDensity) * c, 0f, 1f);

            float scroll = MathHelper.Lerp(waveNearScroll, waveFarScroll, depth);
            float speed = MathHelper.Lerp(waveNearSpeed, waveFarSpeed, depth) * Calc.Random.Range(1f - waveSpeedRandom, 1f + waveSpeedRandom);
            int width = (int) Math.Round(MathHelper.Lerp(waveNearWidth, waveFarWidth, depth) * Calc.Random.Range(1f - waveWidthRandom, 1f + waveWidthRandom));
            var color = Color.Lerp(waveNearColor, waveFarColor, depth) * Calc.Random.Range(1f - waveAlphaRandom, 1f);

            waves[i] = new Wave(depth, Calc.Random.Range(0f, 320f + width), scroll, speed, width, color);
        }
    }

    public override void Update(Scene scene) {
        base.Update(scene);
        time += Engine.DeltaTime;
    }

    public override void Render(Scene scene) {
        var cameraPosition = ((Level) scene).Camera.Position.Floor();

        float startY = farY - (int) (cameraPosition.Y * farScrollY);
        float endY = nearY - (int) (cameraPosition.Y * nearScrollY);
        
        Draw.SpriteBatch.Draw(texture.Texture.Texture_Safe, new Vector2(0f, startY), null, Color.White);
        Draw.SpriteBatch.End();
        Draw.SpriteBatch.Begin();
        
        foreach (var wave in waves) {
            var projectedPosition = new Vector2(wave.XOffset - cameraPosition.X * wave.Scroll + wave.Speed * time, MathHelper.Lerp(endY, startY, wave.Depth));
            float range = 320f + wave.Width;

            projectedPosition.X = (projectedPosition.X % range + range) % range - wave.Width;
            Draw.Rect(projectedPosition, wave.Width, 1f, wave.Color);
        }
        
        Draw.SpriteBatch.End();
        Draw.SpriteBatch.Begin();
    }

    private struct Wave {
        public readonly float Depth;
        public readonly float XOffset;
        public readonly float Scroll;
        public readonly float Speed;
        public readonly float Width;
        public readonly Color Color;

        public Wave(float depth, float xOffset, float scroll, float speed, float width, Color color) {
            Depth = depth;
            XOffset = xOffset;
            Speed = speed;
            Scroll = scroll;
            Color = color;
            Width = width;
        }
    }
}