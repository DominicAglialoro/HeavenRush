using System;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.HeavenRush;

[CustomEntity("heavenRush/abilityCard"), Tracked]
public class AbilityCard : Entity {
    private AbilityCardType cardType;
    private Image image;
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
        Depth = -100;
        
        Add(image = new Image(GFX.Game["heavenRush/objects/abilityCard/texture"]));
        image.CenterOrigin();
        image.SetColor(color);
        
        Add(outline = new Image(GFX.Game["heavenRush/objects/abilityCard/outline"]));
        outline.CenterOrigin();
        
        Add(flash = new Sprite(GFX.Game, "heavenRush/objects/abilityCard/flash"));
        flash.Add("flash", "", 0.1f);
        flash.OnFinish = _ => flash.Visible = false;
        flash.Color = Color.White * 0.5f;
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
        if (!player.GiveCard(cardType))
            return;
        
        Audio.Play(SFX.ui_world_journal_page_cover_forward, Position);
        Celeste.Freeze(0.016f);
        RemoveSelf();
    }
    
    private void UpdateY() => image.Y = outline.Y = flash.Y = sine.Value;
}