using System;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.HeavenRush; 

[Tracked]
public class CardInventoryIndicator : Entity {
    private static readonly Vector2 OFFSET = new(0f, -16f);
    private static readonly float ANIM_DURATION = 0.16f;
    private static readonly float ANIM_OFFSET = 3f;

    private MTexture texture;
    private MTexture outline;
    private AbilityCardType cardType;
    private int cardCount;
    private float animTimer = ANIM_DURATION;
    
    public CardInventoryIndicator() {
        texture = GFX.Game["objects/heavenRush/abilityCardIndicator/texture"];
        outline = GFX.Game["objects/heavenRush/abilityCardIndicator/outline"];
        Depth = -13001;
    }

    public override void Update() {
        base.Update();
        animTimer += Engine.DeltaTime;

        if (animTimer > ANIM_DURATION)
            animTimer = ANIM_DURATION;
    }

    public override void Render() {
        base.Render();

        var player = Scene.Tracker.GetEntity<Player>();
        
        if (player == null)
            return;

        if (cardCount == 0)
            return;

        var position = player.Position + OFFSET;

        if (cardCount == 3)
            position.X++;
        
        var color = cardType switch {
            AbilityCardType.Yellow => Color.Yellow,
            AbilityCardType.Blue => Color.Blue,
            AbilityCardType.Green => Color.Green,
            AbilityCardType.Red => Color.Red,
            AbilityCardType.White => Color.White,
            _ => throw new ArgumentOutOfRangeException()
        };

        float anim = animTimer / ANIM_DURATION;
        
        for (int i = cardCount - 1; i >= 0; i--) {
            var drawPosition = position - i * Vector2.One;
            var drawColor = Color.White;
        
            if (i == 0) {
                drawPosition.Y -= (1f - anim) * ANIM_OFFSET;
                drawColor *= anim;
            }
            
            outline.DrawJustified(drawPosition, new Vector2(0.5f, 1f), drawColor);
        }

        for (int i = cardCount - 1; i >= 0; i--) {
            var drawPosition = position - i * Vector2.One;
            var drawColor = color;

            drawColor *= 1f - 0.2f * i;
            drawColor.A = 255;

            if (i == 0) {
                drawPosition.Y -= (1f - anim) * ANIM_OFFSET;
                drawColor *= anim;
            }
            
            texture.DrawJustified(drawPosition, new Vector2(0.5f, 1f), drawColor);
        }
    }

    public void UpdateInventory(AbilityCardType cardType, int cardCount) {
        this.cardType = cardType;
        this.cardCount = cardCount;
    }

    public void PlayAnimation() => animTimer = 0f;

    public void StopAnimation() => animTimer = ANIM_DURATION;
}