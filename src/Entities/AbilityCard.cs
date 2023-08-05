using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.HeavenRush;

[CustomEntity("AbilityCard")]
public class AbilityCard : Entity {
    private AbilityCardType type;
    private Sprite sprite;
    private Image outline;

    public AbilityCard(EntityData data, Vector2 offset) : base(data.Position + offset) {
        type = (AbilityCardType) data.Int("type", 0);
        Collider = new Hitbox(16f, 16f, -8f, -8f);
        Add(sprite = new Sprite(GFX.Game, data.Attr("texture")));
        Add(outline = new Image(GFX.Game[data.Attr("outline")]));
        Add(new PlayerCollider(OnPlayer));
    }

    private void OnPlayer(Player player) {
        Audio.Play("event:/game/general/diamond_touch", Position);
        Celeste.Freeze(0.05f);
        RemoveSelf();
    }
}