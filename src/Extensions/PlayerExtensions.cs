using System;
using System.Collections;
using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod.Cil;
using MonoMod.Utils;

namespace Celeste.Mod.HeavenRush;

public static class PlayerExtensions {
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
    private static readonly Vector2 BLUE_DASH_STRETCH = new(4f, 0.25f);
    private const float GREEN_DASH_FALL_SPEED = 480f;
    private const float GREEN_DASH_LAND_SPEED = 180f;
    private const float GREEN_DASH_TRAIL_INTERVAL = 0.016f;
    private const float GREEN_DASH_LAND_FREEZE = 0.05f;
    private static readonly Vector2 GREEN_DASH_STRETCH = new(0.5f, 2f);
    private static readonly Vector2 GREEN_DASH_LAND_SQUISH = new(1.5f, 0.75f);
    
    public static void Load() {
        On.Celeste.Player.ctor += Player_ctor;
        IL.Celeste.Player.ctor += Player_ctor_il;
        On.Celeste.Player.Update += Player_Update;
        On.Celeste.Player.OnCollideV += Player_OnCollideV;
        On.Celeste.Player.Jump += Player_Jump;
        On.Celeste.Player.DashBegin += Player_DashBegin;
        On.Celeste.Player.GetCurrentTrailColor += Player_GetCurrentTrailColor;
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
        On.Celeste.Player.GetCurrentTrailColor -= Player_GetCurrentTrailColor;
        Input.Grab.BufferTime = 0f;
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

    private static void UseGreenCard(this Player player) => player.StateMachine.State = (int) CustomState.GreenDash;

    private static void UseRedCard(this Player player) {
        
    }

    private static bool TryUseCard(this Player player) {
        var extData = player.ExtData();
        var cardInventory = extData.CardInventory;
        
        if (cardInventory.CardCount == 0 || player.StateMachine.State == (int) CustomState.BlueDash)
            return false;

        switch (cardInventory.CardType) {
            case AbilityCardType.Yellow:
                player.UseYellowCard();
                break;
            case AbilityCardType.Blue:
                if (player.StateMachine.State == (int) CustomState.BlueDash)
                    return false;
                
                player.UseBlueCard();
                break;
            case AbilityCardType.Green:
                if (player.StateMachine.State == (int) CustomState.GreenDash)
                    return false;
                
                player.UseGreenCard();
                break;
            case AbilityCardType.Red:
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

    private static void ResetStateValues(this Player player,
        Vector2? beforeDashSpeed = null,
        bool autoJump = false,
        float autoJumpTimer = 0f,
        float dashAttackTimer = 0f,
        int dashTrailCounter = 0,
        float dashTrailTimer = 0f,
        float gliderBoostTimer = 0f,
        float jumpGraceTimer = 0f,
        bool launched = false,
        float launchedTimer = 0f,
        float varJumpSpeed = 0f,
        float varJumpTimer = 0f,
        int wallBoostDir = 0,
        float wallBoostTimer = 0f) {
        var dynamicData = DynamicData.For(player);

        player.AutoJump = false;
        player.AutoJumpTimer = 0f;
        dynamicData.Set("beforeDashSpeed", beforeDashSpeed ?? Vector2.Zero);
        dynamicData.Set("dashAttackTimer", 0f);
        dynamicData.Set("dashTrailCounter", 0);
        dynamicData.Set("dashTrailTimer", 0f);
        dynamicData.Set("gliderBoostTimer", 0f);
        dynamicData.Set("jumpGraceTimer", 0f);
        dynamicData.Set("launched", false);
        dynamicData.Set("launchedTimer", 0f);
        dynamicData.Set("varJumpSpeed", 0f);
        dynamicData.Set("varJumpTimer", 0f);
        dynamicData.Set("wallBoostDir", 0);
        dynamicData.Set("wallBoostTimer", 0f);
    }

    private static void BlueDashBegin(this Player player) {
        var dynamicData = DynamicData.For(player);
        
        player.ResetStateValues();
        player.Speed = Vector2.Zero;
        player.DashDir = Vector2.Zero;
        player.Ducking = false;
        Audio.Play("event:/classic/sfx3", player.Position);
        Celeste.Freeze(BLUE_CARD_FREEZE);
        player.Sprite.Play("dash");
        dynamicData.Get<Level>("level").Displacement.AddBurst(player.Center, 0.4f, 8f, 64f, 0.5f, Ease.QuadOut, Ease.QuadOut);
    }

    private static void BlueDashEnd(this Player player) {
        int facing = (int) player.Facing;
        
        if (facing == Math.Sign(player.DashDir.X))
            player.Speed.X = facing * BLUE_DASH_END_SPEED;
        else
            player.Speed.X = 0f;
    }

    private static IEnumerator BlueDashCoroutine(this Player player) {
        var dynamicData = DynamicData.For(player);

        yield return null;
        
        int aimX = Math.Sign(Input.Aim.Value.X);

        if (aimX == 0)
            aimX = (int) player.Facing;

        player.Facing = (Facings) aimX;
        player.DashDir.X = aimX;
        player.DashDir.Y = 0f;
        player.Speed.X = aimX * BLUE_DASH_SPEED;
        player.Speed.Y = 0f;
        dynamicData.Set("dashTrailTimer", BLUE_DASH_TRAIL_INTERVAL);

        float timer = 0f;

        while (timer < BLUE_DASH_DURATION) {
            player.Sprite.Scale = Util.PreserveArea(Vector2.Lerp(BLUE_DASH_STRETCH, Vector2.One, timer / BLUE_DASH_DURATION));
            
            float dashTrailTimer = dynamicData.Get<float>("dashTrailTimer");

            dashTrailTimer -= Engine.DeltaTime;

            if (dashTrailTimer <= 0f) {
                dynamicData.Invoke("CreateTrail");
                dashTrailTimer = BLUE_DASH_TRAIL_INTERVAL;
            }
        
            dynamicData.Set("dashTrailTimer", dashTrailTimer);
            
            timer += Engine.DeltaTime;
            
            yield return null;
        }

        player.ExtData().BlueDashHyperGraceTimer = BLUE_DASH_HYPER_GRACE_PERIOD;
        player.Sprite.Scale = Vector2.One;
        player.StateMachine.State = 0;
    }

    private static void GreenDashBegin(this Player player) {
        player.ResetStateValues(dashTrailTimer: GREEN_DASH_TRAIL_INTERVAL);
        player.Speed = new Vector2(0f, GREEN_DASH_FALL_SPEED);
        player.DashDir = Vector2.UnitY;
        player.Ducking = false;
        Audio.Play(SFX.char_bad_disappear, player.Position);
        player.Sprite.Play("fallFast");
    }

    private static int GreenDashUpdate(this Player player) {
        var dynamicData = DynamicData.For(player);
        float dashTrailTimer = dynamicData.Get<float>("dashTrailTimer");

        dashTrailTimer -= Engine.DeltaTime;

        if (dashTrailTimer <= 0f) {
            dynamicData.Invoke("CreateTrail");
            dashTrailTimer = GREEN_DASH_TRAIL_INTERVAL;
        }
        
        dynamicData.Set("dashTrailTimer", dashTrailTimer);
        player.Sprite.Scale = GREEN_DASH_STRETCH;
        
        return (int) CustomState.GreenDash;
    }

    private static void Player_ctor(On.Celeste.Player.orig_ctor ctor, Player player, Vector2 position, PlayerSpriteMode spritemode) {
        ctor(player, position, spritemode);
        DynamicData.For(player);
        player.ExtData();
        player.StateMachine.SetCallbacks((int) CustomState.BlueDash, null, player.BlueDashCoroutine, player.BlueDashBegin, player.BlueDashEnd);
        player.StateMachine.SetCallbacks((int) CustomState.GreenDash, player.GreenDashUpdate, null, player.GreenDashBegin);
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

        if (extData.BlueDashHyperGraceTimer <= 0f)
            extData.BlueDashHyperGraceTimer = 0f;
        
        update(player);
    }

    private static void Player_OnCollideV(On.Celeste.Player.orig_OnCollideV onCollideV, Player player, CollisionData data) {
        onCollideV(player, data);

        if (player.StateMachine.State != (int) CustomState.GreenDash)
            return;
        
        player.Speed.X = Math.Sign(Input.Aim.Value.X) * GREEN_DASH_LAND_SPEED;
        player.Sprite.Scale = GREEN_DASH_LAND_SQUISH;
        player.Sprite.Play("fallPose");
        Audio.Play(SFX.char_mad_mirrortemple_landing, player.Position);
        Celeste.Freeze(GREEN_DASH_LAND_FREEZE);
        player.StateMachine.State = 0;

        var level = DynamicData.For(player).Get<Level>("level");
        
        level.Particles.Emit(Player.P_SummitLandA, 12, player.BottomCenter, Vector2.UnitX * 3f, -1.5707964f);
        level.Particles.Emit(Player.P_SummitLandB, 8, player.BottomCenter - Vector2.UnitX * 2f, Vector2.UnitX * 2f, 3.403392f);
        level.Particles.Emit(Player.P_SummitLandB, 8, player.BottomCenter + Vector2.UnitX * 2f, Vector2.UnitX * 2f, -0.2617994f);
        level.Displacement.AddBurst(player.Center, 0.4f, 16f, 128f, 1f, Ease.QuadOut, Ease.QuadOut);
    }

    private static void Player_Jump(On.Celeste.Player.orig_Jump jump, Player player, bool particles, bool playsfx) {
        if (player.ExtData().BlueDashHyperGraceTimer <= 0f || !player.OnGround()) {
            jump(player, particles, playsfx);
            
            return;
        }

        player.Ducking = true;
        DynamicData.For(player).Invoke("SuperJump");
    }

    private static void Player_DashBegin(On.Celeste.Player.orig_DashBegin dashBegin, Player player) {
        var extData = player.ExtData();

        if (extData.UseCardCooldown < USE_CARD_AFTER_DASH_COOLDOWN)
            extData.UseCardCooldown = USE_CARD_AFTER_DASH_COOLDOWN;

        extData.BlueDashHyperGraceTimer = 0f;
        dashBegin(player);
    }

    private static Color Player_GetCurrentTrailColor(On.Celeste.Player.orig_GetCurrentTrailColor getCurrentTrailColor, Player player) => player.StateMachine.State switch {
        (int) CustomState.BlueDash => Color.Blue,
        (int) CustomState.GreenDash => Color.Green,
        _ => getCurrentTrailColor(player)
    };

    public static Data ExtData(this Player player) => Extension<Player, Data>.Of(player);

    public class Data {
        public CardInventory CardInventory { get; } = new();

        public float UseCardCooldown { get; set; }
        
        public float BlueDashHyperGraceTimer { get; set; }
    }
}