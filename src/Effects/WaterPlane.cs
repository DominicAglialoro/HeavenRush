using System;
using Celeste.Mod.Backdrops;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace Celeste.Mod.HeavenRush.Effects; 

[CustomBackdrop("heavenRush/waterPlane")]
public class WaterPlane : Parallax {
    private float farScrollMult;
    
    public WaterPlane(BinaryPacker.Element data) : base(GFX.Game[data.Attr("texture")]) {
        farScrollMult = data.AttrFloat("farScrollMult");
        BlendState = BlendState.Additive;
    }

    public override void Render(Scene scene) {
        var cameraPosition = ((Level) scene).Camera.Position.Floor();
        int startX = (int) (Position.X - cameraPosition.X);
        int startY = (int) (Position.Y - Scroll.Y * cameraPosition.Y);
        
        for (int i = 0; i < Texture.Height; i++) {
            float scroll = Scroll.X * MathHelper.Lerp(farScrollMult, 1f, (float) i / (Texture.Height - 1));
            int x = ((int) (scroll * startX) % Texture.Width - Texture.Width) % Texture.Width;
        
            Draw.SpriteBatch.Draw(Texture.Texture.Texture_Safe, new Vector2(x, startY + i),
                new Rectangle(0, i, 320 - x, 1), Color);
        }
    }
}