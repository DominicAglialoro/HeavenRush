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
    private const float BLUE_CARD_FREEZE = 0.05f;
    private const float BLUE_DASH_SPEED = 960f;
    private const float BLUE_DASH_END_SPEED = 260f;
    private const float BLUE_DASH_DURATION = 0.1f;
    private const float BLUE_DASH_TRAIL_INTERVAL = 0.016f;
    private static readonly Vector2 BLUE_DASH_STRETCH = new(4f, 0.125f);
    private const float GREEN_DASH_FALL_SPEED = 480f;
    private const float GREEN_DASH_LAND_SPEED = 180f;
    private const float GREEN_DASH_TRAIL_INTERVAL = 0.016f;
    private const float GREEN_DASH_LAND_FREEZE = 0.05f;
    private static readonly Vector2 GREEN_DASH_STRETCH = new(0.5f, 2f);
    private static readonly Vector2 GREEN_DASH_LAND_SQUISH = new(2.5f, 0.125f);
    private const float GREEN_DASH_LAND_SQUISH_DURATION = 0.16f;
    
    public static void Load() {
        On.Celeste.Player.ctor += Player_ctor;
        IL.Celeste.Player.ctor += Player_ctor_il;
        On.Celeste.Player.Update += Player_Update;
        On.Celeste.Player.OnCollideV += Player_OnCollideV;
        On.Celeste.Player.StartDash += Player_StartDash;
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
        On.Celeste.Player.StartDash -= Player_StartDash;
        On.Celeste.Player.GetCurrentTrailColor -= Player_GetCurrentTrailColor;
        Input.Grab.BufferTime = 0f;
    }

    private static void UseYellowCard(this Player player) {
        var dynamicData = DynamicData.For(player);
        
        Audio.Play(SFX.game_gen_thing_booped, player.Position);
        Celeste.Freeze(YELLOW_CARD_FREEZE);
        
        if (player.StateMachine.State == 4 && player.CurrentBooster != null) {
            player.CurrentBooster.PlayerReleased();
            player.CurrentBooster = null;
        }

        int aimX = Math.Sign(Input.Aim.Value.X);
        float newSpeedX = player.Speed.X + aimX * YELLOW_BOUNCE_ADD_X;

        if (aimX != 0 && aimX * newSpeedX < YELLOW_BOUNCE_MIN_X)
            newSpeedX = aimX * YELLOW_BOUNCE_MIN_X;

        float newSpeedY = player.Speed.Y + YELLOW_BOUNCE_ADD_Y;

        if (newSpeedY > YELLOW_BOUNCE_MIN_Y)
            newSpeedY = YELLOW_BOUNCE_MIN_Y;

        player.Speed.X = newSpeedX;
        player.Speed.Y = newSpeedY;
        player.Sprite.Scale = new Vector2(0.6f, 1.4f);
        dynamicData.Set("jumpGraceTimer", 0f);
        dynamicData.Set("dashAttackTimer", 0f);
        dynamicData.Set("varJumpTimer", 0f);
        dynamicData.Set("varJumpSpeed", 0f);
        dynamicData.Set("launched", false);
        player.StateMachine.State = 0;
    }

    private static void UseBlueCard(this Player player) {
        player.StateMachine.State = (int) CustomState.BlueDash;
    }

    private static void UseGreenCard(this Player player) {
        player.StateMachine.State = (int) CustomState.GreenDash;
    }
    
    private static bool TryUseCard(this Player player) {
        var extData = player.ExtData();
        var cardInventory = extData.CardInventory;
        
        if (cardInventory.CardCount == 0 || extData.BlueDashActive)
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
                break;
            case AbilityCardType.White:
                break;
            default:
                return false;
        }
        
        cardInventory.PopCard();

        return true;
    }

    private static void BlueDashBegin(this Player player) {
        var dynamicData = DynamicData.For(player);
        
        dynamicData.Set("launched", false);
        dynamicData.Set("dashTrailTimer", BLUE_DASH_TRAIL_INTERVAL);
        dynamicData.Set("dashTrailCounter", 0);
        dynamicData.Set("dashAttackTimer", 0f);
        dynamicData.Set("gliderBoostTimer", 0f);
        dynamicData.Set("jumpGraceTimer", 0f);
        dynamicData.Set("beforeDashSpeed", player.Speed);
        player.Speed = Vector2.Zero;
        player.DashDir = Vector2.Zero;
        player.Ducking = false;
        player.ExtData().BlueDashActive = true;
        Audio.Play("event:/classic/sfx3", player.Position);
        Celeste.Freeze(BLUE_CARD_FREEZE);
    }

    private static void BlueDashEnd(this Player player) {
        if (!player.ExtData().BlueDashActive)
            return;
        
        int aimX = Math.Sign(Input.Aim.Value.X);
        
        if (aimX == Math.Sign(player.DashDir.X))
            player.Speed.X = aimX * BLUE_DASH_END_SPEED;
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

        float timer = 0f;
        bool willHyper = false;

        while (timer < BLUE_DASH_DURATION) {
            player.Sprite.Scale = Vector2.Lerp(BLUE_DASH_STRETCH, Vector2.One, timer / BLUE_DASH_DURATION);
            
            float dashTrailTimer = dynamicData.Get<float>("dashTrailTimer");

            dashTrailTimer -= Engine.DeltaTime;

            if (dashTrailTimer <= 0f) {
                dynamicData.Invoke("CreateTrail");
                dashTrailTimer = BLUE_DASH_TRAIL_INTERVAL;
            }
        
            dynamicData.Set("dashTrailTimer", dashTrailTimer);

            if (Input.Jump.Pressed) {
                Input.Jump.ConsumeBuffer();
                willHyper = true;
            }
            
            timer += Engine.DeltaTime;
            
            yield return null;
        }

        player.Sprite.Scale = Vector2.One;
        aimX = Math.Sign(Input.Aim.Value.X);

        if ((willHyper || Input.Jump.Pressed) && player.OnGround()) {
            Input.Jump.ConsumeBuffer();
            player.Ducking = true;
            dynamicData.Invoke("SuperJump");
        }
        else if (aimX == Math.Sign(player.DashDir.X))
            player.Speed.X = aimX * BLUE_DASH_END_SPEED;
        else
            player.Speed.X = 0f;
        
        player.ExtData().BlueDashActive = false;
        player.StateMachine.State = 0;
    }

    private static void GreenDashBegin(this Player player) {
        var dynamicData = DynamicData.For(player);
        
        dynamicData.Set("launched", false);
        dynamicData.Set("dashTrailTimer", GREEN_DASH_TRAIL_INTERVAL);
        dynamicData.Set("dashTrailCounter", 0);
        dynamicData.Set("dashAttackTimer", 0f);
        dynamicData.Set("gliderBoostTimer", 0f);
        dynamicData.Set("jumpGraceTimer", 0f);
        player.Speed = new Vector2(0f, GREEN_DASH_FALL_SPEED);
        player.DashDir = Vector2.UnitY;
        player.Ducking = false;
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

    private static void GreenDashEnd(this Player player) {
        
    }

    private static IEnumerator GreenDashLandSquishCoroutine(this Player player) {
        float timer = 0f;

        while (timer < GREEN_DASH_LAND_SQUISH_DURATION) {
            player.Sprite.Scale = Vector2.Lerp(GREEN_DASH_LAND_SQUISH, Vector2.One, timer / GREEN_DASH_LAND_SQUISH_DURATION);
            timer += Engine.DeltaTime;

            yield return null;
        }
    }

    private static void Player_ctor(On.Celeste.Player.orig_ctor ctor, Player player, Vector2 position, PlayerSpriteMode spritemode) {
        ctor(player, position, spritemode);
        player.StateMachine.SetCallbacks((int) CustomState.BlueDash, null, player.BlueDashCoroutine, player.BlueDashBegin, player.BlueDashEnd);
        player.StateMachine.SetCallbacks((int) CustomState.GreenDash, player.GreenDashUpdate, null, player.GreenDashBegin, player.GreenDashEnd);
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

        update(player);
    }

    private static void Player_OnCollideV(On.Celeste.Player.orig_OnCollideV onCollideV, Player player, CollisionData data) {
        onCollideV(player, data);

        if (player.StateMachine.State != (int) CustomState.GreenDash)
            return;
        
        player.Speed.X = Math.Sign(Input.Aim.Value.X) * GREEN_DASH_LAND_SPEED;
        player.Sprite.Scale = GREEN_DASH_LAND_SQUISH;
        player.Add(new Coroutine(player.GreenDashLandSquishCoroutine()));
        Audio.Play(SFX.char_mad_mirrortemple_landing, player.Position);
        Celeste.Freeze(GREEN_DASH_LAND_FREEZE);
        player.StateMachine.State = 0;
    }
    
    private static int Player_StartDash(On.Celeste.Player.orig_StartDash startDash, Player player) {
        var extData = player.ExtData();

        if (extData.UseCardCooldown < USE_CARD_AFTER_DASH_COOLDOWN)
            extData.UseCardCooldown = USE_CARD_AFTER_DASH_COOLDOWN;

        return startDash(player);
    }

    private static Color Player_GetCurrentTrailColor(On.Celeste.Player.orig_GetCurrentTrailColor getCurrentTrailColor, Player player) => player.StateMachine.State switch {
        (int) CustomState.BlueDash => Color.Blue,
        (int) CustomState.GreenDash => Color.Green,
        _ => getCurrentTrailColor(player)
    };

    public static Data ExtData(this Player player) {
        return Extension<Player, Data>.Of(player);
    }
    
    public class Data {
        public CardInventory CardInventory { get; } = new();

        public float UseCardCooldown { get; set; }
        
        public bool BlueDashActive { get; set; }
    }
}