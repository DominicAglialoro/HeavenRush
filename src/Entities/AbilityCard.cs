using System;
using System.Collections;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.HeavenRush;

[CustomEntity("HeavenRush/AbilityCard")]
public class AbilityCard : Entity {
    private AbilityCardType cardType;
    private Image texture;
    private Image outline;
    private Sprite flash;
    private SineWave sine;

    public AbilityCard(EntityData data, Vector2 offset) : base(data.Position + offset) {
        cardType = data.Enum<AbilityCardType>("cardType");

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
        texture.CenterOrigin();
        texture.SetColor(color);
        Add(outline = new Image(GFX.Game["AbilityCard/outline"]));
        outline.CenterOrigin();
        Add(flash = new Sprite(GFX.Game, "AbilityCard/flash"));
        flash.Add("flash", "", 0.1f);
        flash.OnFinish = _ => flash.Visible = false;
        flash.CenterOrigin();
        Add(new VertexLight(Color.Lerp(color, Color.White, 0.75f), 1f, 16, 48));
        Add(sine = new SineWave(0.6f));
        sine.Randomize();
        Add(new PlayerCollider(OnPlayer));
        UpdateY();
    }

    public override void Update() {
        base.Update();
        UpdateY();

        if (Scene.OnInterval(4f)) {
            flash.Visible = true;
            flash.Play("flash", true);
        }
    }

    private void OnPlayer(Player player) {
        if (!player.ExtData().CardInventory.TryAddCard(cardType))
            return;
        
        Audio.Play(SFX.ui_world_journal_page_cover_forward, Position);
        Celeste.Freeze(0.033f);
        RemoveSelf();
    }
    
    private void UpdateY() => texture.Y = outline.Y = flash.Y = sine.Value;
}