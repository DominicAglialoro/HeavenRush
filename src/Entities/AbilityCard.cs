using System;
using System.Collections;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.HeavenRush;

[CustomEntity("HeavenRush/AbilityCard"), Tracked]
public class AbilityCard : Entity {
    private AbilityCardType cardType;
    private Image texture;
    private Image outline;
    private SineWave sine;

    public AbilityCard(EntityData data, Vector2 offset) : base(data.Position + offset) {
        cardType = (AbilityCardType) data.Int("cardType", 0);

        var color = cardType switch {
            AbilityCardType.Yellow => Color.Yellow,
            AbilityCardType.Blue => Color.Blue,
            AbilityCardType.Green => Color.Green,
            AbilityCardType.Red => Color.Red,
            AbilityCardType.White => Color.White,
            _ => throw new ArgumentOutOfRangeException()
        };
        
        Collider = new Hitbox(16f, 16f, -8f, -8f);
        Add(texture = new Image(GFX.Game["AbilityCard/texture"]));
        Add(outline = new Image(GFX.Game["AbilityCard/outline"]));
        Add(new VertexLight(Color.Lerp(color, Color.White, 0.75f), 1f, 16, 48));
        Add(sine = new SineWave(0.6f));
        Add(new PlayerCollider(OnPlayer));
        texture.CenterOrigin();
        texture.SetColor(color);
        outline.CenterOrigin();
        sine.Randomize();
        UpdateY();
    }

    public override void Update() {
        base.Update();
        UpdateY();
    }

    private void OnPlayer(Player player) {
        if (!player.ExtData().CardInventory.TryAddCard(cardType))
            return;
        
        Collidable = false;
        Audio.Play("event:/game/general/diamond_touch", Position);
        Celeste.Freeze(0.05f);
        RemoveSelf();
    }
    
    private void UpdateY() => texture.Y = outline.Y = sine.Value;
}