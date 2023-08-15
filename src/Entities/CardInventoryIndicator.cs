using System;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.HeavenRush; 

[Tracked]
public class CardInventoryIndicator : Entity {
    private static readonly Vector2 OFFSET = new(0f, -16f);
    
    public CardInventoryIndicator() {
        Depth = -13001;
        AddTag(Tags.Persistent);
    }

    public override void Render() {
        base.Render();

        var player = Scene.Tracker.GetEntity<Player>();
        
        if (player == null)
            return;

        var cardInventory = player.ExtData().CardInventory;
        
        if (cardInventory.CardCount == 0)
            return;

        var texture = GFX.Game["AbilityCardIndicator/texture"];
        var outline = GFX.Game["AbilityCardIndicator/outline"];
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
        
        for (int i = cardInventory.CardCount - 1; i >= 0; i--)
            outline.DrawJustified(position - i * Vector2.One, new Vector2(0.5f, 1f));

        for (int i = cardInventory.CardCount - 1; i >= 0; i--)
            texture.DrawJustified(position - i * Vector2.One, new Vector2(0.5f, 1f), (color * (1f - 0.2f * i)) with { A = 255 });
    }
}