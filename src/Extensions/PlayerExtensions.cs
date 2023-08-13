using System;
using System.Collections;
using ExtendedVariants.Module;
using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

namespace Celeste.Mod.HeavenRush;

public static class PlayerExtensions {
    private const float GROUND_BOOST_FRICTION = 0.65f;
    private const float GROUND_BOOST_SPEED = 325f;
    private const float GROUND_BOOST_ACCELERATION = 650f;
    private const float GROUND_BOOST_PARTICLE_INTERVAL = 0.004f;
    private static ParticleType GROUND_BOOST_PARTICLE;
    private const float MAX_USE_CARD_COOLDOWN = 0.1f;
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
    private const float BLUE_DASH_TRAIL_DURATION = 0.66f;
    private static readonly Vector2 BLUE_DASH_STRETCH = new(2f, 0.5f);
    private const float GREEN_DIVE_FALL_SPEED = 480f;
    private const float GREEN_DIVE_LAND_SPEED = 180f;
    private const float GREEN_DIVE_TRAIL_INTERVAL = 0.016f;
    private const float GREEN_DIVE_TRAIL_DURATION = 0.33f;
    private const float GREEN_DIVE_LAND_FREEZE = 0.05f;
    private static readonly Vector2 GREEN_DIVE_STRETCH = new(0.5f, 2f);
    private static readonly Vector2 GREEN_DIVE_LAND_SQUISH = new(1.5f, 0.75f);
    private const float RED_CARD_FREEZE = 0.05f;
    private static readonly Vector2 RED_BOOST_DASH_SPEED = new(280f, 240f);
    private const float RED_BOOST_DASH_DURATION = 0.15f;
    private const float RED_BOOST_DURATION = 0.8f;
    private const float RED_BOOST_TRAIL_INTERVAL = 0.016f;
    private const float RED_BOOST_TRAIL_DURATION = 0.33f;
    private const float WHITE_CARD_FREEZE = 0.05f;
    private const float WHITE_CARD_REDIRECT_FREEZE = 0.033f;
    private const float WHITE_DASH_SPEED = 280f;
    private const float WHITE_DASH_REDIRECT_ADD_SPEED = 40f;

    private static IDetour il_celeste_Player_orig_Update;
    
    public static void Load() {
        On.Celeste.Player.ctor += Player_ctor;
        IL.Celeste.Player.ctor += Player_ctor_il;
        On.Celeste.Player.Update += Player_Update;
        il_celeste_Player_orig_Update = new ILHook(typeof(Player).GetMethodUnconstrained("orig_Update"), Player_orig_Update_il);
        IL.Celeste.Player.OnCollideH += Player_OnCollideH_il;
        IL.Celeste.Player.OnCollideV += Player_OnCollideV_il;
        On.Celeste.Player.Jump += Player_Jump;
        On.Celeste.Player.NormalUpdate += Player_NormalUpdate;
        IL.Celeste.Player.NormalUpdate += Player_NormalUpdate_il;
        On.Celeste.Player.DashBegin += Player_DashBegin;
    }

    public static void Initialize() {
        GROUND_BOOST_PARTICLE = new ParticleType(Player.P_DashA) {
            Color = Color.Aquamarine,
            SpeedMin = 10f,
            SpeedMax = 20f,
            LifeMin = 0.15f,
            LifeMax = 0.25f,
            FadeMode = ParticleType.FadeModes.Linear
        };
    }

    public static void Unload() {
        On.Celeste.Player.ctor -= Player_ctor;
        IL.Celeste.Player.ctor -= Player_ctor_il;
        On.Celeste.Player.Update -= Player_Update;
        il_celeste_Player_orig_Update.Dispose();
        IL.Celeste.Player.OnCollideV -= Player_OnCollideV_il;
        On.Celeste.Player.Jump -= Player_Jump;
        On.Celeste.Player.DashBegin -= Player_DashBegin;
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
            extData.GroundBoostSoundSource.Play(SFX.env_loc_waterfall_big_main);
        }
        else {
            variants.Remove(ExtendedVariantsModule.Variant.Friction);
            extData.GroundBoostSoundSource.Stop();
        }
    }

    private static bool ShouldUseCard(this Player player) {
        if (!Input.Grab.Pressed)
            return false;

        var extData = player.ExtData();
        var cardInventory = extData.CardInventory;

        return cardInventory.CardCount > 0 && (player.StateMachine.State != 2 || cardInventory.CardType == AbilityCardType.Yellow); 
    }

    private static int UseCard(this Player player) {
        var extData = player.ExtData();
        var cardInventory = extData.CardInventory;
        
        Input.Grab.ConsumeBuffer();
        cardInventory.PopCard();
        extData.UseCardCooldown = MAX_USE_CARD_COOLDOWN;

        return cardInventory.CardType switch {
            AbilityCardType.Yellow => player.UseYellowCard(),
            AbilityCardType.Blue => player.UseBlueCard(),
            AbilityCardType.Green => player.UseGreenCard(),
            AbilityCardType.Red => player.UseRedCard(),
            AbilityCardType.White => player.UseWhiteCard(),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private static int UseYellowCard(this Player player) {
        var dynamicData = DynamicData.For(player);
        
        Audio.Play(SFX.game_gen_thing_booped, player.Position);
        Celeste.Freeze(YELLOW_CARD_FREEZE);
        dynamicData.Get<Level>("level").Add(Engine.Pooler.Create<SpeedRing>().Init(player.Center, 0.5f * (float) Math.PI, Color.White));

        int moveX = Input.MoveX.Value;
        float newSpeedX = player.Speed.X + moveX * YELLOW_BOUNCE_ADD_X;

        if (moveX != 0 && moveX * newSpeedX < YELLOW_BOUNCE_MIN_X)
            newSpeedX = moveX * YELLOW_BOUNCE_MIN_X;

        float newSpeedY = player.Speed.Y + YELLOW_BOUNCE_ADD_Y;

        if (newSpeedY > YELLOW_BOUNCE_MIN_Y)
            newSpeedY = YELLOW_BOUNCE_MIN_Y;

        player.ResetStateValues();
        player.Speed.X = newSpeedX;
        player.Speed.Y = newSpeedY;
        player.Sprite.Scale = YELLOW_BOUNCE_STRETCH;

        return 0;
    }

    private static int UseBlueCard(this Player player) {
        player.PrepareForCustomDash();
        Audio.Play("event:/classic/sfx3", player.Position);
        Celeste.Freeze(BLUE_CARD_FREEZE);

        return (int) CustomState.BlueDash;
    }

    private static int UseGreenCard(this Player player) {
        var extData = player.ExtData();
        
        player.PrepareForCustomDash();
        player.Speed = new Vector2(0f, GREEN_DIVE_FALL_SPEED);
        extData.CustomTrailTimer = GREEN_DIVE_TRAIL_INTERVAL;
        Audio.Play(SFX.game_05_crackedwall_vanish, player.Position);

        return (int) CustomState.GreenDive;
    }

    private static int UseRedCard(this Player player) {
        player.PrepareForCustomDash();
        Audio.Play("event:/classic/sfx3", player.Position);
        Celeste.Freeze(RED_CARD_FREEZE);

        return (int) CustomState.RedBoostDash;
    }

    private static int UseWhiteCard(this Player player) {
        var extData = player.ExtData();
        
        Audio.Play(SFX.char_bad_dash_red_right, player.Position);
        player.PrepareForCustomDash();
        extData.WhiteDashSoundSource.Play(SFX.char_mad_dreamblock_travel);
        Celeste.Freeze(WHITE_CARD_FREEZE);

        return (int) CustomState.WhiteDash;
    }

    private static void ResetStateValues(this Player player) {
        var extData = player.ExtData();
        var dynamicData = DynamicData.For(player);

        player.AutoJump = false;
        player.AutoJumpTimer = 0f;
        dynamicData.Set("dashAttackTimer", 0f);
        dynamicData.Set("dashTrailTimer", 0f);
        dynamicData.Set("dashTrailCounter", 0);
        dynamicData.Set("gliderBoostTimer", 0f);
        dynamicData.Set("jumpGraceTimer", 0f);
        dynamicData.Set("launched", false);
        dynamicData.Set("launchedTimer", 0f);
        dynamicData.Set("varJumpSpeed", 0f);
        dynamicData.Set("varJumpTimer", 0f);
        dynamicData.Set("wallBoostDir", 0);
        dynamicData.Set("wallBoostTimer", 0f);
        extData.BlueDashHyperGraceTimer = 0f;
    }

    private static void PrepareForCustomDash(this Player player) {
        var dynamicData = DynamicData.For(player);
        bool onGround = dynamicData.Get<bool>("onGround");

        player.ResetStateValues();
        dynamicData.Set("beforeDashSpeed", player.Speed);
        dynamicData.Set("dashStartedOnGround", onGround);
        player.Speed = Vector2.Zero;
        player.DashDir = Vector2.Zero;

        if (!onGround && player.Ducking && player.CanUnDuck)
            player.Ducking = false;
    }

    private static void UpdateTrail(this Player player, Color color, float interval, float duration) {
        var extData = player.ExtData();

        extData.CustomTrailTimer -= Engine.DeltaTime;

        if (extData.CustomTrailTimer > 0f)
            return;
        
        extData.CustomTrailTimer = interval;
        TrailManager.Add(player, new Vector2((float) player.Facing * Math.Abs(player.Sprite.Scale.X), player.Sprite.Scale.Y), color, duration);
    }

    private static bool IsCustomDash(this Player player) => player.StateMachine.State is
        (int) CustomState.BlueDash or
        (int) CustomState.GreenDive or
        (int) CustomState.RedBoostDash or
        (int) CustomState.WhiteDash;

    private static float GetUltraBoostSpeed(this Player player) {
        if (player.StateMachine.State != (int) CustomState.RedBoostDash)
            return player.Speed.X * 1.2f;
        
        if (Math.Abs(player.Speed.X) < GROUND_BOOST_SPEED)
            return player.DashDir.X * GROUND_BOOST_SPEED;

        return player.Speed.X;
    }

    private static void BeforeBaseUpdate(this Player player) {
        var extData = player.ExtData();
        var dynamicData = DynamicData.For(player);
        
        player.SetGroundBoost((extData.RedBoostTimer > 0f || extData.GroundBoostSources > 0) && player.Speed.Y >= 0f && dynamicData.Get<bool>("onGround"));
    }

    private static IEnumerator BlueDashCoroutine(this Player player) {
        yield return null;

        var dynamicData = DynamicData.For(player);
        var extData = player.ExtData();
        int aimX = Math.Sign(dynamicData.Get<Vector2>("lastAim").X);

        if (aimX == 0)
            aimX = (int) player.Facing;

        player.DashDir.X = aimX;
        player.DashDir.Y = 0f;
        player.Speed.X = aimX * BLUE_DASH_SPEED;
        player.Speed.Y = 0f;
        extData.CustomTrailTimer = BLUE_DASH_TRAIL_INTERVAL;
        dynamicData.Get<Level>("level").Displacement.AddBurst(player.Center, 0.4f, 8f, 64f, 0.5f, Ease.QuadOut, Ease.QuadOut);

        for (float timer = 0f; timer < BLUE_DASH_DURATION; timer += Engine.DeltaTime) {
            player.Sprite.Scale = Util.PreserveArea(Vector2.Lerp(BLUE_DASH_STRETCH, Vector2.One, timer / BLUE_DASH_DURATION));
            player.UpdateTrail(Color.Blue, BLUE_DASH_TRAIL_INTERVAL, BLUE_DASH_TRAIL_DURATION);
            
            yield return null;
        }

        extData.BlueDashHyperGraceTimer = BLUE_DASH_HYPER_GRACE_PERIOD;
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

    private static int GreenDiveUpdate(this Player player) {
        player.UpdateTrail(Color.Green, GREEN_DIVE_TRAIL_INTERVAL, GREEN_DIVE_TRAIL_DURATION);
        player.Sprite.Scale = GREEN_DIVE_STRETCH;
        
        return (int) CustomState.GreenDive;
    }

    private static IEnumerator RedBoostDashCoroutine(this Player player) {
        yield return null;
        
        var dynamicData = DynamicData.For(player);
        var extData = player.ExtData();
        
        dynamicData.Get<Level>("level").Displacement.AddBurst(player.Center, 0.4f, 8f, 64f, 0.5f, Ease.QuadOut, Ease.QuadOut);
        extData.RedBoostTimer = RED_BOOST_DURATION;

        var dashDir = dynamicData.Invoke<Vector2>("CorrectDashPrecision", dynamicData.Get<Vector2>("lastAim"));
        var newSpeed = RED_BOOST_DASH_SPEED * dashDir;
        
        if (dynamicData.Get<bool>("onGround") && dashDir.X != 0f && dashDir.Y >= 0f && player.Speed.Y >= 0f) {
            player.Ducking = dashDir.Y > 0f;
            dashDir.X = Math.Sign(dashDir.X);
            dashDir.Y = 0f;
            newSpeed.X = dashDir.X * GROUND_BOOST_SPEED;
            newSpeed.Y = 0f;
        }
        
        player.DashDir = dashDir;
        
        float beforeDashSpeed = dynamicData.Get<Vector2>("beforeDashSpeed").X;
        
        if (Math.Sign(newSpeed.X) == Math.Sign(beforeDashSpeed) && Math.Abs(newSpeed.X) < Math.Abs(beforeDashSpeed))
            newSpeed.X = beforeDashSpeed;

        player.Speed = newSpeed;

        for (float timer = 0f; timer < RED_BOOST_DASH_DURATION; timer += Engine.DeltaTime) {
            player.UpdateTrail(Color.Red, RED_BOOST_TRAIL_INTERVAL, RED_BOOST_TRAIL_DURATION);
            
            if (Input.Jump.Pressed && dynamicData.Get<float>("jumpGraceTimer") > 0f) {
                player.StateMachine.State = 0;
                player.Jump();
                
                yield break;
            }
            
            yield return null;
        }

        player.StateMachine.State = 0;
    }

    private static IEnumerator WhiteDashCoroutine(this Player player) {
        yield return null;
        
        var dynamicData = DynamicData.For(player);
        var extData = player.ExtData();
        
        player.DashDir = dynamicData.Invoke<Vector2>("CorrectDashPrecision", dynamicData.Get<Vector2>("lastAim"));

        float beforeDashSpeed = dynamicData.Get<Vector2>("beforeDashSpeed").X;
        float dashSpeed = WHITE_DASH_SPEED;

        if (player.DashDir.X != 0f && Math.Sign(player.DashDir.X) == Math.Sign(beforeDashSpeed) && Math.Abs(beforeDashSpeed) > dashSpeed)
            dashSpeed = Math.Abs(beforeDashSpeed);
        
        player.Speed = dashSpeed * player.DashDir;

        while (true) {
            var cardInventory = extData.CardInventory;
            
            if (player.ShouldUseCard() && cardInventory.CardType == AbilityCardType.White) {
                Input.Grab.ConsumeBuffer();
                cardInventory.PopCard();
                extData.UseCardCooldown = MAX_USE_CARD_COOLDOWN;

                float newSpeed = player.Speed.Length() + WHITE_DASH_REDIRECT_ADD_SPEED;

                if (newSpeed < WHITE_DASH_SPEED)
                    newSpeed = WHITE_DASH_SPEED;
                
                player.DashDir = Vector2.Zero;
                player.Speed = Vector2.Zero;
                Audio.Play(SFX.char_bad_dash_red_right, player.Position);
                Celeste.Freeze(WHITE_CARD_REDIRECT_FREEZE);

                yield return null;
                
                player.DashDir = dynamicData.Invoke<Vector2>("CorrectDashPrecision", dynamicData.Get<Vector2>("lastAim"));
                player.Speed = newSpeed * player.DashDir;
            }

            yield return null;
        }
    }

    private static void WhiteDashEnd(this Player player) {
        var extData = player.ExtData();

        extData.WhiteDashSoundSource.Stop();
        Audio.Play(SFX.char_bad_dreamblock_exit);
        Audio.Play(SFX.game_05_redbooster_end);
    }

    private static void OnTrueCollideH(this Player player) {
        if (player.StateMachine.State == (int) CustomState.WhiteDash)
            player.StateMachine.State = 0;
    }

    private static void OnTrueCollideV(this Player player) {
        var dynamicData = DynamicData.For(player);
        
        switch (player.StateMachine.State) {
            case (int) CustomState.GreenDive:
                player.Speed.X = Math.Sign(Input.MoveX.Value) * GREEN_DIVE_LAND_SPEED;
                player.Sprite.Scale = GREEN_DIVE_LAND_SQUISH;
                Audio.Play(SFX.game_gen_fallblock_impact, player.Position);
                Celeste.Freeze(GREEN_DIVE_LAND_FREEZE);
                player.StateMachine.State = 0;

                var level = dynamicData.Get<Level>("level");

                level.Particles.Emit(Player.P_SummitLandA, 12, player.BottomCenter, Vector2.UnitX * 3f, -1.5707964f);
                level.Particles.Emit(Player.P_SummitLandB, 8, player.BottomCenter - Vector2.UnitX * 2f, Vector2.UnitX * 2f, 3.403392f);
                level.Particles.Emit(Player.P_SummitLandB, 8, player.BottomCenter + Vector2.UnitX * 2f, Vector2.UnitX * 2f, -0.2617994f);
                level.Displacement.AddBurst(player.Center, 0.4f, 16f, 128f, 1f, Ease.QuadOut, Ease.QuadOut);
                break;
            case (int) CustomState.WhiteDash:
                player.StateMachine.State = 0;
                break;
        }
    }

    private static void Player_ctor(On.Celeste.Player.orig_ctor ctor, Player player, Vector2 position, PlayerSpriteMode spritemode) {
        ctor(player, position, spritemode);
        
        DynamicData.For(player);
        
        var extData = player.ExtData();
        
        player.Add(extData.GroundBoostSoundSource = new SoundSource());
        player.Add(extData.WhiteDashSoundSource = new SoundSource());
        player.StateMachine.SetCallbacks((int) CustomState.BlueDash, null, player.BlueDashCoroutine, null, player.BlueDashEnd);
        player.StateMachine.SetCallbacks((int) CustomState.GreenDive, player.GreenDiveUpdate);
        player.StateMachine.SetCallbacks((int) CustomState.RedBoostDash, null, player.RedBoostDashCoroutine);
        player.StateMachine.SetCallbacks((int) CustomState.WhiteDash, null, player.WhiteDashCoroutine, null, player.WhiteDashEnd);
        ExtendedVariantsModule.Session.OverriddenVariantsInRoom.Remove(ExtendedVariantsModule.Variant.Friction);
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
        var dynamicData = DynamicData.For(player);

        extData.UseCardCooldown -= Engine.DeltaTime;

        if (extData.UseCardCooldown < 0f)
            extData.UseCardCooldown = 0f;

        extData.BlueDashHyperGraceTimer -= Engine.DeltaTime;

        if (extData.BlueDashHyperGraceTimer < 0f)
            extData.BlueDashHyperGraceTimer = 0f;

        extData.RedBoostTimer -= Engine.DeltaTime;
        
        if (extData.RedBoostTimer < 0f)
            extData.RedBoostTimer = 0f;

        update(player);

        if (extData.GroundBoost && player.Speed.X != 0f) {
            extData.GroundBoostParticleTimer += Engine.DeltaTime;

            var level = dynamicData.Get<Level>("level");

            while (extData.GroundBoostParticleTimer > 0f) {
                level.ParticlesFG.Emit(GROUND_BOOST_PARTICLE, player.Position, (-player.Speed).Angle() + Calc.Random.Range(-1f, 1f));
                extData.GroundBoostParticleTimer -= GROUND_BOOST_PARTICLE_INTERVAL;
            }
        }
        else
            extData.GroundBoostParticleTimer = 0f;
    }

    private static void Player_orig_Update_il(ILContext il) {
        var cursor = new ILCursor(il);

        cursor.GotoNext(MoveType.AfterLabel,
            instr => instr.OpCode == OpCodes.Ldarg_0,
            instr => instr.MatchCall<Actor>("Update"));

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Call, typeof(PlayerExtensions).GetMethodUnconstrained(nameof(BeforeBaseUpdate)));
    }

    private static void Player_OnCollideH_il(ILContext il) {
        var cursor = new ILCursor(il);

        cursor.GotoNext(MoveType.After,
            instr => instr.OpCode == OpCodes.Ldarg_0,
            instr => instr.MatchLdfld<Player>("StateMachine"),
            instr => instr.MatchCallvirt<StateMachine>("get_State"),
            instr => instr.OpCode == OpCodes.Ldc_I4_2,
            instr => instr.OpCode == OpCodes.Beq_S);

        object branch = cursor.Prev.Operand;

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Call, typeof(PlayerExtensions).GetMethodUnconstrained(nameof(IsCustomDash)));
        cursor.Emit(OpCodes.Brtrue_S, branch);
        
        cursor.Index = -1;
        cursor.GotoPrev(MoveType.After,
            instr => instr.OpCode == OpCodes.Ldarg_0,
            instr => instr.MatchLdcR4(0f),
            instr => instr.MatchStfld<Player>("gliderBoostTimer"));

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Call, typeof(PlayerExtensions).GetMethodUnconstrained(nameof(OnTrueCollideH)));
    }

    private static void Player_OnCollideV_il(ILContext il) {
        var cursor = new ILCursor(il);
        
        cursor.GotoNext(MoveType.After,
            instr => instr.OpCode == OpCodes.Ldarg_0,
            instr => instr.MatchLdfld<Player>("StateMachine"),
            instr => instr.MatchCallvirt<StateMachine>("get_State"),
            instr => instr.OpCode == OpCodes.Ldc_I4_2,
            instr => instr.OpCode == OpCodes.Beq_S);

        object branch = cursor.Prev.Operand;

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Call, typeof(PlayerExtensions).GetMethodUnconstrained(nameof(IsCustomDash)));
        cursor.Emit(OpCodes.Brtrue_S, branch);

        cursor.GotoNext(
            instr => instr.OpCode == OpCodes.Dup,
            instr => instr.OpCode == OpCodes.Ldind_R4,
            instr => instr.MatchLdcR4(1.2f),
            instr => instr.OpCode == OpCodes.Mul);

        cursor.RemoveRange(4);
        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Call, typeof(PlayerExtensions).GetMethodUnconstrained(nameof(GetUltraBoostSpeed)));

        cursor.Index = -1;
        cursor.GotoPrev(MoveType.After,
            instr => instr.OpCode == OpCodes.Ldarg_0,
            instr => instr.MatchLdflda<Player>("Speed"),
            instr => instr.MatchLdcR4(0f),
            instr => instr.MatchStfld<Vector2>("Y"));

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Call, typeof(PlayerExtensions).GetMethodUnconstrained(nameof(OnTrueCollideV)));
    }

    private static void Player_Jump(On.Celeste.Player.orig_Jump jump, Player player, bool particles, bool playsfx) {
        var dynamicData = DynamicData.For(player);
        var extData = player.ExtData();

        if (extData.BlueDashHyperGraceTimer > 0f && dynamicData.Get<bool>("onGround")) {
            player.Ducking = true;
            dynamicData.Invoke("SuperJump");

            return;
        }
        
        if (extData.GroundBoost)
            Audio.Play(SFX.char_mad_water_out);
            
        jump(player, particles, playsfx);
    }
    
    private static int Player_NormalUpdate(On.Celeste.Player.orig_NormalUpdate normalUpdate, Player player) {
        var extData = player.ExtData();
        
        if (extData.RedBoostTimer > 0f)
            player.UpdateTrail(Color.Red, RED_BOOST_TRAIL_INTERVAL, RED_BOOST_TRAIL_DURATION);
        
        int moveX = Input.MoveX.Value;
        
        if (extData.GroundBoost && !player.Ducking && moveX * player.Speed.X < GROUND_BOOST_SPEED)
            player.Speed.X = Calc.Approach(player.Speed.X, moveX * GROUND_BOOST_SPEED, Engine.DeltaTime * GROUND_BOOST_ACCELERATION);

        return normalUpdate(player);
    }

    private static void Player_NormalUpdate_il(ILContext il) {
        var cursor = new ILCursor(il);
        ILLabel label = null;

        cursor.GotoNext(MoveType.After,
            instr => instr.OpCode == OpCodes.Ldarg_0,
            instr => instr.MatchCallvirt<Player>("get_CanDash"),
            instr => instr.MatchBrfalse(out label));
        cursor.GotoLabel(label);

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Call, typeof(PlayerExtensions).GetMethodUnconstrained(nameof(ShouldUseCard)));
        
        var newLabel = cursor.DefineLabel();
        
        cursor.Emit(OpCodes.Brfalse_S, newLabel);
        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Call, typeof(PlayerExtensions).GetMethodUnconstrained(nameof(UseCard)));
        cursor.Emit(OpCodes.Ret);
        cursor.MarkLabel(newLabel);
    }

    private static void Player_DashBegin(On.Celeste.Player.orig_DashBegin dashBegin, Player player) {
        var extData = player.ExtData();

        extData.BlueDashHyperGraceTimer = 0f;
        dashBegin(player);
    }

    public class Data {
        public readonly CardInventory CardInventory = new();
        public float UseCardCooldown;
        public float BlueDashHyperGraceTimer;
        public float RedBoostTimer;
        public SoundSource WhiteDashSoundSource;
        public float CustomTrailTimer;
        public bool GroundBoost;
        public int GroundBoostSources;
        public SoundSource GroundBoostSoundSource;
        public float GroundBoostParticleTimer;
    }
}