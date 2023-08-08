using System;
using System.Collections;
using ExtendedVariants.Module;
using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod.Cil;
using MonoMod.Utils;

namespace Celeste.Mod.HeavenRush;

public static class PlayerExtensions {
    private const float GROUND_BOOST_FRICTION = 0.65f;
    private const float GROUND_BOOST_SPEED = 240f;
    private const float GROUND_BOOST_ACCELERATION = 1920f;
    private const int GROUND_BOOST_DOWN_CHECK = 4;
    private const float MAX_USE_CARD_COOLDOWN = 0.1f;
    private const float USE_CARD_AFTER_DASH_COOLDOWN = 0.083f;
    private const float YELLOW_CARD_FREEZE = 0.033f;
    private const float YELLOW_BOUNCE_MIN_X = 80f;
    private const float YELLOW_BOUNCE_ADD_X = 40f;
    private const float YELLOW_BOUNCE_MIN_Y = -315f;
    private const float YELLOW_BOUNCE_ADD_Y = -105f;
    private static readonly Vector2 YELLOW_BOUNCE_STRETCH = new(0.4f, 1.8f);
    private const float BLUE_CARD_FREEZE = 0.05f;
    private const float BLUE_DASH_SPEED = 960f;
    private const float BLUE_DASH_END_SPEED = 175f;
    private const float BLUE_DASH_DURATION = 0.1f;
    private const float BLUE_DASH_HYPER_GRACE_PERIOD = 0.033f;
    private const float BLUE_DASH_TRAIL_INTERVAL = 0.016f;
    private static readonly Vector2 BLUE_DASH_STRETCH = new(2f, 0.5f);
    private const float GREEN_DIVE_FALL_SPEED = 480f;
    private const float GREEN_DIVE_LAND_SPEED = 180f;
    private const float GREEN_DIVE_TRAIL_INTERVAL = 0.016f;
    private const float GREEN_DIVE_LAND_FREEZE = 0.05f;
    private static readonly Vector2 GREEN_DIVE_STRETCH = new(0.5f, 2f);
    private static readonly Vector2 GREEN_DIVE_LAND_SQUISH = new(1.5f, 0.75f);
    private const float RED_CARD_FREEZE = 0.05f;
    private static readonly Vector2 RED_BOOST_DASH_SPEED = new(325f, 240f);
    private const float RED_BOOST_DASH_DURATION = 0.15f;
    private const float RED_BOOST_DURATION = 0.65f;
    private const float RED_BOOST_TRAIL_INTERVAL = 0.016f;
    
    public static void Load() {
        On.Celeste.Player.ctor += Player_ctor;
        IL.Celeste.Player.ctor += Player_ctor_il;
        On.Celeste.Player.Update += Player_Update;
        On.Celeste.Player.OnCollideV += Player_OnCollideV;
        On.Celeste.Player.Jump += Player_Jump;
        On.Celeste.Player.NormalUpdate += Player_NormalUpdate;
        On.Celeste.Player.DashBegin += Player_DashBegin;
    }

    public static void Initialize() {
        Input.Grab.BufferTime = 0.08f;
    }

    public static void Unload() {
        On.Celeste.Player.ctor -= Player_ctor;
        IL.Celeste.Player.ctor -= Player_ctor_il;
        On.Celeste.Player.Update -= Player_Update;
        On.Celeste.Player.OnCollideV -= Player_OnCollideV;
        On.Celeste.Player.Jump -= Player_Jump;
        On.Celeste.Player.DashBegin -= Player_DashBegin;
        Input.Grab.BufferTime = 0f;
    }

    public static Data ExtData(this Player player) => Extension<Player, Data>.Of(player);

    private static void SetGroundBoost(this Player player, bool groundBoost) {
        var extData = player.ExtData();
        
        if (extData.GroundBoost == groundBoost)
            return;

        extData.GroundBoost = groundBoost;

        var variants = ExtendedVariantsModule.Session.OverriddenVariantsInRoom;
        
        if (groundBoost) {
            variants[ExtendedVariantsModule.Variant.Friction] = GROUND_BOOST_FRICTION;
            Audio.Play(SFX.char_mad_water_in);
        }
        else
            variants.Remove(ExtendedVariantsModule.Variant.Friction);
    }

    private static void UseYellowCard(this Player player) {
        var dynamicData = DynamicData.For(player);
        
        Audio.Play(SFX.game_gen_thing_booped, player.Position);
        Celeste.Freeze(YELLOW_CARD_FREEZE);
        dynamicData.Get<Level>("level").Add(Engine.Pooler.Create<SpeedRing>().Init(player.Center, 0.5f * (float) Math.PI, Color.White));

        int aimX = Math.Sign(Input.Aim.Value.X);
        float newSpeedX = player.Speed.X + aimX * YELLOW_BOUNCE_ADD_X;

        if (aimX != 0 && aimX * newSpeedX < YELLOW_BOUNCE_MIN_X)
            newSpeedX = aimX * YELLOW_BOUNCE_MIN_X;

        float newSpeedY = player.Speed.Y + YELLOW_BOUNCE_ADD_Y;

        if (newSpeedY > YELLOW_BOUNCE_MIN_Y)
            newSpeedY = YELLOW_BOUNCE_MIN_Y;

        player.ResetStateValues();
        player.Speed.X = newSpeedX;
        player.Speed.Y = newSpeedY;
        player.Sprite.Scale = YELLOW_BOUNCE_STRETCH;
        player.StateMachine.State = 0;
    }

    private static void UseBlueCard(this Player player) => player.StateMachine.State = (int) CustomState.BlueDash;

    private static void UseGreenCard(this Player player) => player.StateMachine.State = (int) CustomState.GreenDive;

    private static void UseRedCard(this Player player) => player.StateMachine.State = (int) CustomState.RedBoostDash;

    private static bool TryUseCard(this Player player) {
        var extData = player.ExtData();
        var cardInventory = extData.CardInventory;
        int state = player.StateMachine.State;
        
        if (cardInventory.CardCount == 0 || state == (int) CustomState.BlueDash)
            return false;

        switch (cardInventory.CardType) {
            case AbilityCardType.Yellow:
                player.UseYellowCard();
                break;
            case AbilityCardType.Blue when state != 2:
                player.UseBlueCard();
                break;
            case AbilityCardType.Green when state is not ((int) CustomState.GreenDive or 2):
                player.UseGreenCard();
                break;
            case AbilityCardType.Red when state is not ((int) CustomState.RedBoostDash or 2):
                player.UseRedCard();
                break;
            case AbilityCardType.White:
                break;
            default:
                return false;
        }
        
        cardInventory.PopCard();

        return true;
    }

    private static void ResetStateValues(this Player player) {
        var dynamicData = DynamicData.For(player);

        player.AutoJump = false;
        player.AutoJumpTimer = 0f;
        dynamicData.Set("dashAttackTimer", 0f);
        dynamicData.Set("dashTrailTimer", 0f);
        dynamicData.Set("dashTrailCounter", 0);
        dynamicData.Set("gliderBoostTimer", 0f);
        dynamicData.Set("jumpGraceTimer", 0f);
        dynamicData.Set("varJumpSpeed", 0f);
        dynamicData.Set("varJumpTimer", 0f);
        dynamicData.Set("wallBoostDir", 0);
        dynamicData.Set("wallBoostTimer", 0f);
        player.ExtData().BlueDashHyperGraceTimer = 0f;
    }

    private static void UpdateTrail(this Player player, float interval) {
        var extData = player.ExtData();

        extData.CustomTrailTimer -= Engine.DeltaTime;

        if (extData.CustomTrailTimer > 0f)
            return;
        
        extData.CustomTrailTimer = interval;
        TrailManager.Add(player, new Vector2((float) player.Facing * Math.Abs(player.Sprite.Scale.X), player.Sprite.Scale.Y), player.GetCustomTrailColor());
    }

    private static Color GetCustomTrailColor(this Player player) {
        if (player.StateMachine.State == 0 && player.ExtData().RedBoostTimer > 0f)
            return Color.Red;
        
        return player.StateMachine.State switch {
            (int) CustomState.BlueDash => Color.Blue,
            (int) CustomState.GreenDive => Color.Green,
            (int) CustomState.RedBoostDash => Color.Red,
            _ => Color.White
        };
    }

    private static void BlueDashBegin(this Player player) {
        player.ResetStateValues();
        player.Speed = Vector2.Zero;
        player.DashDir = Vector2.Zero;
        player.Ducking = false;
        Audio.Play("event:/classic/sfx3", player.Position);
        Celeste.Freeze(BLUE_CARD_FREEZE);
    }

    private static int BlueDashUpdate(this Player player) {
        player.UpdateTrail(BLUE_DASH_TRAIL_INTERVAL);

        return (int) CustomState.BlueDash;
    }

    private static IEnumerator BlueDashCoroutine(this Player player) {
        yield return null;

        var dynamicData = DynamicData.For(player);
        int aimX = Math.Sign(Input.GetAimVector(player.Facing).X);

        player.Facing = (Facings) aimX;
        player.DashDir.X = aimX;
        player.DashDir.Y = 0f;
        player.Speed.X = aimX * BLUE_DASH_SPEED;
        player.Speed.Y = 0f;
        player.ExtData().CustomTrailTimer = BLUE_DASH_TRAIL_INTERVAL;
        dynamicData.Get<Level>("level").Displacement.AddBurst(player.Center, 0.4f, 8f, 64f, 0.5f, Ease.QuadOut, Ease.QuadOut);

        for (float timer = 0f; timer < BLUE_DASH_DURATION; timer += Engine.DeltaTime) {
            player.Sprite.Scale = Util.PreserveArea(Vector2.Lerp(BLUE_DASH_STRETCH, Vector2.One, timer / BLUE_DASH_DURATION));
            
            yield return null;
        }

        player.ExtData().BlueDashHyperGraceTimer = BLUE_DASH_HYPER_GRACE_PERIOD;
        player.StateMachine.State = 0;
    }

    private static void BlueDashEnd(this Player player) {
        int facing = (int) player.Facing;
        
        if (facing == Math.Sign(player.DashDir.X))
            player.Speed.X = facing * BLUE_DASH_END_SPEED;
        else
            player.Speed.X = 0f;
        
        player.Sprite.Scale = Vector2.One;
    }

    private static void GreenDiveBegin(this Player player) {
        player.ResetStateValues();
        player.Speed = new Vector2(0f, GREEN_DIVE_FALL_SPEED);
        player.DashDir = Vector2.UnitY;
        player.Ducking = false;
        player.ExtData().CustomTrailTimer = GREEN_DIVE_TRAIL_INTERVAL;
        Audio.Play(SFX.char_bad_disappear, player.Position);
    }

    private static int GreenDiveUpdate(this Player player) {
        player.UpdateTrail(GREEN_DIVE_TRAIL_INTERVAL);
        player.Sprite.Scale = GREEN_DIVE_STRETCH;
        
        return (int) CustomState.GreenDive;
    }

    private static void RedBoostBegin(this Player player) {
        var dynamicData = DynamicData.For(player);
        
        player.ResetStateValues();
        dynamicData.Set("beforeDashSpeed", player.Speed);
        player.Speed = Vector2.Zero;
        player.DashDir = Vector2.Zero;
        player.Ducking = false;
        Audio.Play("event:/classic/sfx3", player.Position);
        Celeste.Freeze(RED_CARD_FREEZE);
    }

    private static int RedBoostUpdate(this Player player) {
        var dynamicData = DynamicData.For(player);
        
        player.UpdateTrail(RED_BOOST_TRAIL_INTERVAL);

        if (Input.Jump.Pressed && dynamicData.Get<float>("jumpGraceTimer") > 0) {
            player.Jump();

            return 0;
        }

        return (int) CustomState.RedBoostDash;
    }

    private static IEnumerator RedBoostCoroutine(this Player player) {
        yield return null;
        
        var dynamicData = DynamicData.For(player);
        var extData = player.ExtData();

        player.DashDir = Input.GetAimVector(player.Facing);
        
        var newSpeed = RED_BOOST_DASH_SPEED * player.DashDir;
        var beforeDashSpeed = dynamicData.Get<Vector2>("beforeDashSpeed");
        
        if (Math.Sign(newSpeed.X) == Math.Sign(beforeDashSpeed.X) && Math.Abs(newSpeed.X) < Math.Abs(beforeDashSpeed.X))
            newSpeed.X = beforeDashSpeed.X;

        player.Speed = newSpeed;
        dynamicData.Get<Level>("level").Displacement.AddBurst(player.Center, 0.4f, 8f, 64f, 0.5f, Ease.QuadOut, Ease.QuadOut);
        extData.RedBoostTimer = RED_BOOST_DURATION;

        yield return RED_BOOST_DASH_DURATION;

        player.StateMachine.State = 0;
    }

    private static void Player_ctor(On.Celeste.Player.orig_ctor ctor, Player player, Vector2 position, PlayerSpriteMode spritemode) {
        ctor(player, position, spritemode);
        DynamicData.For(player);
        player.ExtData();
        player.StateMachine.SetCallbacks((int) CustomState.BlueDash, player.BlueDashUpdate, player.BlueDashCoroutine, player.BlueDashBegin, player.BlueDashEnd);
        player.StateMachine.SetCallbacks((int) CustomState.GreenDive, player.GreenDiveUpdate, null, player.GreenDiveBegin);
        player.StateMachine.SetCallbacks((int) CustomState.RedBoostDash, player.RedBoostUpdate, player.RedBoostCoroutine, player.RedBoostBegin);
    }

    private static void Player_ctor_il(ILContext il) {
        var cursor = new ILCursor(il);
    
        cursor.GotoNext(
            instr => instr.OpCode == OpCodes.Ldc_I4_S,
            instr => instr.MatchNewobj<StateMachine>());
        cursor.Next.Operand = (int) CustomState.Count;
    }

    private static void Player_Update(On.Celeste.Player.orig_Update update, Player player) {
        var extData = player.ExtData();

        extData.UseCardCooldown -= Engine.DeltaTime;

        if (extData.UseCardCooldown <= 0f) {
            if (Input.Grab.Pressed && player.TryUseCard()) {
                Input.Grab.ConsumeBuffer();
                extData.UseCardCooldown = MAX_USE_CARD_COOLDOWN;
            }
            else
                extData.UseCardCooldown = 0f;
        }

        extData.BlueDashHyperGraceTimer -= Engine.DeltaTime;

        if (extData.BlueDashHyperGraceTimer < 0f)
            extData.BlueDashHyperGraceTimer = 0f;

        extData.RedBoostTimer -= Engine.DeltaTime;
        
        if (extData.RedBoostTimer <= 0f)
            extData.RedBoostTimer = 0f;
        
        update(player);
        player.SetGroundBoost((extData.RedBoostTimer > 0f || extData.GroundBoostSources > 0) && player.OnGround(GROUND_BOOST_DOWN_CHECK));
    }

    private static void Player_OnCollideV(On.Celeste.Player.orig_OnCollideV onCollideV, Player player, CollisionData data) {
        onCollideV(player, data);

        if (player.StateMachine.State != (int) CustomState.GreenDive)
            return;
        
        player.Speed.X = Math.Sign(Input.Aim.Value.X) * GREEN_DIVE_LAND_SPEED;
        player.Sprite.Scale = GREEN_DIVE_LAND_SQUISH;
        player.Sprite.Play("fallPose");
        Audio.Play(SFX.char_mad_mirrortemple_landing, player.Position);
        Celeste.Freeze(GREEN_DIVE_LAND_FREEZE);
        player.StateMachine.State = 0;

        var level = DynamicData.For(player).Get<Level>("level");
        
        level.Particles.Emit(Player.P_SummitLandA, 12, player.BottomCenter, Vector2.UnitX * 3f, -1.5707964f);
        level.Particles.Emit(Player.P_SummitLandB, 8, player.BottomCenter - Vector2.UnitX * 2f, Vector2.UnitX * 2f, 3.403392f);
        level.Particles.Emit(Player.P_SummitLandB, 8, player.BottomCenter + Vector2.UnitX * 2f, Vector2.UnitX * 2f, -0.2617994f);
        level.Displacement.AddBurst(player.Center, 0.4f, 16f, 128f, 1f, Ease.QuadOut, Ease.QuadOut);
    }

    private static void Player_Jump(On.Celeste.Player.orig_Jump jump, Player player, bool particles, bool playsfx) {
        var dynamicData = DynamicData.For(player);
        
        if (player.ExtData().BlueDashHyperGraceTimer <= 0f || !dynamicData.Get<bool>("onGround")) {
            jump(player, particles, playsfx);
            
            return;
        }

        player.Ducking = true;
        dynamicData.Invoke("SuperJump");
    }
    
    private static int Player_NormalUpdate(On.Celeste.Player.orig_NormalUpdate normalUpdate, Player player) {
        var extData = player.ExtData();
        
        if (extData.RedBoostTimer > 0f)
            player.UpdateTrail(RED_BOOST_TRAIL_INTERVAL);
        
        int nextState = normalUpdate(player);

        if (nextState != 0)
            return nextState;
        
        int aimX = Math.Sign(Input.Aim.Value.X);
        
        if (extData.GroundBoost && aimX * player.Speed.X < GROUND_BOOST_SPEED)
            player.Speed.X = Calc.Approach(player.Speed.X, aimX * GROUND_BOOST_SPEED, Engine.DeltaTime * GROUND_BOOST_ACCELERATION);

        return 0;
    }

    private static void Player_DashBegin(On.Celeste.Player.orig_DashBegin dashBegin, Player player) {
        var extData = player.ExtData();

        if (extData.UseCardCooldown < USE_CARD_AFTER_DASH_COOLDOWN)
            extData.UseCardCooldown = USE_CARD_AFTER_DASH_COOLDOWN;

        extData.BlueDashHyperGraceTimer = 0f;
        dashBegin(player);
    }

    public class Data {
        public CardInventory CardInventory { get; } = new();

        public float UseCardCooldown { get; set; }
        
        public float BlueDashHyperGraceTimer { get; set; }
        
        public float RedBoostTimer { get; set; }
        
        public float CustomTrailTimer { get; set; }
        
        public bool GroundBoost { get; set; }
        
        public int GroundBoostSources { get; set; }
    }
}