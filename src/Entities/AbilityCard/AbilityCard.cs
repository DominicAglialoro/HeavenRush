using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.HeavenRush;

[CustomEntity("HeavenRush/AbilityCard")]
public class AbilityCard : Entity {
    private AbilityCardType cardType;
    private Image image;

    public AbilityCard(EntityData data, Vector2 offset) : base(data.Position + offset) {
        cardType = (AbilityCardType) data.Int("cardType", 0);
        Collider = new Hitbox(16f, 16f, -8f, -8f);
        Add(image = new Image(GFX.Game[$"AbilityCard/Card{cardType}"]));
        Add(new PlayerCollider(OnPlayer));
        image.CenterOrigin();
    }

    private void OnPlayer(Player player) {
        if (!player.ExtData().CardInventory.TryAddCard(cardType))
            return;
        
        Audio.Play("event:/game/general/diamond_touch", Position);
        Celeste.Freeze(0.05f);
        RemoveSelf();
    }
}