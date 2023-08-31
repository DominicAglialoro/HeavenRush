using System;
using Celeste.Mod.Backdrops;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace Celeste.Mod.HeavenRush; 

[CustomBackdrop("heavenRush/waterPlane")]
public class WaterPlane : Backdrop {
    private MTexture texture;
    private Vector2[] points;
    private float[] pointSpeedMults;
    private float nearY;
    private float nearScrollMult;
    private float farPointAlpha;
    private float nearPointAlpha;
    private float time;

    public WaterPlane(BinaryPacker.Element data) {
        texture = GFX.Game[data.Attr("texture")];
        nearY = data.AttrFloat("nearY");
        nearScrollMult = data.AttrFloat("nearScrollMult");
        farPointAlpha = data.AttrFloat("farPointAlpha");
        nearPointAlpha = data.AttrFloat("nearPointAlpha");
        points = new Vector2[(int) data.AttrFloat("pointCount")];

        for (int i = 0; i < points.Length; i++) {
            points[i] = Calc.Random.Range(Vector2.Zero, new Vector2(360f, 1f));
            points[i].Y *= points[i].Y;
        }

        pointSpeedMults = new float[points.Length];

        for (int i = 0; i < pointSpeedMults.Length; i++)
            pointSpeedMults[i] = Calc.Random.Range(0.5f, 1.5f);
    }

    public override void Update(Scene scene) {
        base.Update(scene);
        time += Engine.DeltaTime;
    }

    public override void Render(Scene scene) {
        var cameraPosition = ((Level) scene).Camera.Position.Floor();
        var start = (Position - cameraPosition * Scroll).Floor();
        var end = (new Vector2(Position.X * nearScrollMult, nearY) - cameraPosition * Scroll * nearScrollMult).Floor();
        
        Draw.SpriteBatch.Draw(texture.Texture.Texture_Safe, new Vector2(0f, start.Y), null, Color, 0f, Vector2.Zero, new Vector2(1f, (end.Y - start.Y) / texture.Height), SpriteEffects.None, 0);

        for (int i = 0; i < points.Length; i++) {
            var point = points[i];
            var projectedPosition = new Vector2(point.X, 0f) + Vector2.Lerp(start, end, point.Y);
            float width = MathHelper.Lerp(8f, 20f, point.Y);

            projectedPosition.X += Speed.X * pointSpeedMults[i] * MathHelper.Lerp(1f, nearScrollMult, point.Y) * time;
            projectedPosition.X = (projectedPosition.X % 360f + 360f) % 360f - 20f;
            Draw.Line(projectedPosition - width * Vector2.UnitX, projectedPosition + width * Vector2.UnitX, (Color.White * MathHelper.Lerp(farPointAlpha, nearPointAlpha, point.Y)) with { A = 0 });
        }
    }
}