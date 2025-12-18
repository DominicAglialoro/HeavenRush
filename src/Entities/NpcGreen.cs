using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.HeavenRush;

[CustomEntity("heavenRush/npcGreen")]
public class NpcGreen : Entity {
    private readonly Vector2[] nodes;

    private BadelineDummy dummy;
    private SoundSource moveSfx;
    private int nextNode;

    public NpcGreen(EntityData data, Vector2 offset) : base(data.Position + offset) {
        nodes = data.NodesOffset(offset);
        Add(moveSfx = new SoundSource());
    }

    public override void Awake(Scene scene) {
        base.Awake(scene);
        scene.Add(dummy = new BadelineDummy(Position));
        dummy.Hair.Color = Color.LightGray;
        dummy.Depth = -1000000;
        dummy.Light.Color = Color.White;
    }

    public override void Update() {
        base.Update();

        for (int i = nodes.Length - 1; i >= nextNode; i--) {
            if (!SceneAs<Level>().Session.GetFlag($"npcGreen_node{i}"))
                continue;

            MoveToNode(i);

            break;
        }
    }

    private void MoveToNode(int index) {
        var start = dummy.Position;
        var end = nodes[index];
        var tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.CubeInOut, 0.5f, true);

        moveSfx.Play(SFX.char_bad_temple_move_chats);
        tween.OnUpdate = tween => {
            dummy.Position = Vector2.Lerp(start, end, tween.Eased);

            if (Scene.OnInterval(0.03f))
                SceneAs<Level>().ParticlesFG.Emit(BadelineOldsite.P_Vanish, 2, dummy.Position + new Vector2(0f, -6f), 2f * Vector2.One);

            if (tween.Eased >= 0.1f && tween.Eased <= 0.9f && Scene.OnInterval(0.05f))
                TrailManager.Add(dummy, Color.Green, 0.5f, false);
        };

        Add(tween);
        nextNode = index + 1;
    }
}