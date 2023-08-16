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
    private AbilityCardType previousCardType;
    private int previousCardCount;
    private float animTimer = ANIM_DURATION;
    
    public CardInventoryIndicator() {
        texture = GFX.Game["AbilityCardIndicator/texture"];
        outline = GFX.Game["AbilityCardIndicator/outline"];
        Depth = -13001;
        AddTag(Tags.Persistent);
    }

    public override void Update() {
        base.Update();

        animTimer += Engine.DeltaTime;

        if (animTimer > ANIM_DURATION)
            animTimer = ANIM_DURATION;
        
        var player = Scene.Tracker.GetEntity<Player>();
        
        if (player == null)
            return;
        
        var cardInventory = player.ExtData().CardInventory;

        if (cardInventory.CardType != previousCardType || cardInventory.CardCount > previousCardCount)
            animTimer = 0f;

        previousCardType = cardInventory.CardType;
        previousCardCount = cardInventory.CardCount;
    }

    public override void Render() {
        base.Render();

        var player = Scene.Tracker.GetEntity<Player>();
        
        if (player == null)
            return;

        var cardInventory = player.ExtData().CardInventory;
        
        if (cardInventory.CardCount == 0)
            return;

        var position = player.Position + OFFSET;

        if (cardInventory.CardCount == 3)
            position.X++;
        
        var color = cardInventory.CardType switch {
            AbilityCardType.Yellow => Color.Yellow,
            AbilityCardType.Blue => Color.Blue,
            AbilityCardType.Green => Color.Green,
            AbilityCardType.Red => Color.Red,
            AbilityCardType.White => Color.White,
            _ => throw new ArgumentOutOfRangeException()
        };

        float anim = animTimer / ANIM_DURATION;
        
        for (int i = cardInventory.CardCount - 1; i >= 0; i--) {
            var drawPosition = position - i * Vector2.One;
            var drawColor = Color.White;
        
            if (i == 0) {
                drawPosition.Y -= (1f - anim) * ANIM_OFFSET;
                drawColor *= anim;
            }
            
            outline.DrawJustified(drawPosition, new Vector2(0.5f, 1f), drawColor);
        }

        for (int i = cardInventory.CardCount - 1; i >= 0; i--) {
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
}