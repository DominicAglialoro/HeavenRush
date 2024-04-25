using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.HeavenRush;

[CustomEntity("heavenRush/levelTitleMessage")]
public class LevelTitleMessage : Entity {
    private const float RADIUS = 50f;
    private const float ANIM_IN_DURATION = 0.8f;
    private const float ANIM_OUT_DURATION = 0.5f;
    private const float MAX_OFFSET = 100f;
    private const float VERTICAL_SPACING = 30f;

    private string subtitle;
    private string title;
    private Vector2 playerStartPosition;
    private float anim;
    private float maxAnim = ANIM_IN_DURATION;
    private bool animOut;

    public LevelTitleMessage(EntityData data, Vector2 offset) : base(data.Position + offset) {
        subtitle = data.Attr("subtitle");
        title = data.Attr("title");
        Tag = Tags.HUD;
    }

    public override void Awake(Scene scene) {
        base.Awake(scene);

        var respawnPoint = SceneAs<Level>().Session.RespawnPoint;
        var player = Scene.Tracker.GetEntity<Player>();

        playerStartPosition = respawnPoint ?? player?.Position ?? Vector2.Zero;
    }

    public override void Update() {
        base.Update();

        var player = Scene.Tracker.GetEntity<Player>();

        if (player == null)
            return;

        if (!animOut && (player.Position - playerStartPosition).LengthSquared() > RADIUS * RADIUS) {
            animOut = true;
            anim = 0f;
            maxAnim = ANIM_OUT_DURATION;
        }

        anim += Engine.DeltaTime;

        if (anim < maxAnim)
            return;

        if (animOut)
            RemoveSelf();
        else
            anim = maxAnim;
    }

    public override void Render() {
        if (Scene.Paused)
            return;

        float interp = anim / maxAnim;
        float ease = Ease.CubeOut(interp);
        float offset;
        float alpha;

        if (animOut) {
            offset = ease * MAX_OFFSET;
            alpha = 1f - ease;
        }
        else {
            offset = (ease - 1f) * MAX_OFFSET;
            alpha = ease;
        }

        var cameraPosition = SceneAs<Level>().Camera.Position;
        var drawPosition = 6f * (Position - cameraPosition);

        ActiveFont.DrawOutline(subtitle, drawPosition - new Vector2(offset, VERTICAL_SPACING), new Vector2(0.5f, 0.5f),
            0.5f * Vector2.One, Color.White * alpha, 2f, Color.Black * alpha);
        ActiveFont.DrawOutline(title, drawPosition + new Vector2(offset, VERTICAL_SPACING), new Vector2(0.5f, 0.5f),
            1.25f * Vector2.One, Color.White * alpha, 2f, Color.Black * alpha);
    }
}