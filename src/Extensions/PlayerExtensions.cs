using System;
using System.Collections;
using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

namespace Celeste.Mod.HeavenRush;

public static class PlayerExtensions {
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
    private const float BLUE_DASH_ALLOW_JUMP_AT = 0.066f;
    private const float BLUE_DASH_HYPER_GRACE_PERIOD = 0.033f;
    private const float BLUE_DASH_TRAIL_INTERVAL = 0.016f;
    private const float BLUE_DASH_TRAIL_DURATION = 0.66f;
    private static readonly Vector2 BLUE_DASH_STRETCH = new(2f, 0.5f);
    private const float GREEN_DIVE_FALL_SPEED = 420f;
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
    private const float RED_BOOST_AIR_FRICTION = 0.325f;
    private const float RED_BOOST_TRAIL_INTERVAL = 0.016f;
    private const float RED_BOOST_TRAIL_DURATION = 0.33f;
    private const float WHITE_CARD_FREEZE = 0.05f;
    private const float WHITE_CARD_REDIRECT_FREEZE = 0.033f;
    private const float WHITE_DASH_SPEED = 325f;
    private const float WHITE_DASH_REDIRECT_ADD_SPEED = 40f;
    private const float WHITE_DASH_TRAIL_INTERVAL = 0.016f;
    private const float WHITE_DASH_TRAIL_DURATION = 0.33f;
    private static readonly Vector2 WHITE_DASH_STRETCH = new(2f, 0.5f);
    private const float GROUND_BOOST_FRICTION = 0.1f;
    private const float GROUND_BOOST_SPEED = 240f;
    private const float GROUND_BOOST_ACCELERATION = 650f;
    private const float GROUND_BOOST_PARTICLE_INTERVAL = 0.004f;
    private static readonly ParticleType GROUND_BOOST_PARTICLE = new() {
        Color = Color.Aquamarine,
        ColorMode = ParticleType.ColorModes.Static,
        FadeMode = ParticleType.FadeModes.Linear,
        LifeMin = 0.05f,
        LifeMax = 0.1f,
        Size = 1f,
        SpeedMin = 60f,
        SpeedMax = 120f,
        DirectionRange = 0.7f
    };
    private const float GROUND_BOOST_PARTICLE_ANGLE = 0.4f;

    private static IDetour Celeste_Player_get_DashAttacking;
    private static IDetour il_Celeste_Player_orig_Update;
    
    public static void Load() {
        On.Celeste.Player.ctor += Player_ctor;
        Celeste_Player_get_DashAttacking = new Hook(typeof(Player).GetPropertyUnconstrained("DashAttacking").GetGetMethod(), Player_get_DashAttacking);
        On.Celeste.Player.Update += Player_Update;
        il_Celeste_Player_orig_Update = new ILHook(typeof(Player).GetMethodUnconstrained("orig_Update"), Player_orig_Update_il);
        IL.Celeste.Player.OnCollideH += Player_OnCollideH_il;
        IL.Celeste.Player.OnCollideV += Player_OnCollideV_il;
        IL.Celeste.Player.BeforeDownTransition += Player_BeforeDownTransition_il;
        IL.Celeste.Player.BeforeUpTransition += Player_BeforeUpTransition_il;
        On.Celeste.Player.Jump += Player_Jump;
        On.Celeste.Player.NormalUpdate += Player_NormalUpdate;
        IL.Celeste.Player.NormalUpdate += Player_NormalUpdate_il;
        On.Celeste.Player.DashBegin += Player_DashBegin;
        On.Celeste.Player.UpdateSprite += Player_UpdateSprite;
    }

    public static void Unload() {
        On.Celeste.Player.ctor -= Player_ctor;
        Celeste_Player_get_DashAttacking.Dispose();
        On.Celeste.Player.Update -= Player_Update;
        il_Celeste_Player_orig_Update.Dispose();
        IL.Celeste.Player.OnCollideH -= Player_OnCollideH_il;
        IL.Celeste.Player.OnCollideV -= Player_OnCollideV_il;
        IL.Celeste.Player.BeforeDownTransition -= Player_BeforeDownTransition_il;
        IL.Celeste.Player.BeforeUpTransition -= Player_BeforeUpTransition_il;
        On.Celeste.Player.Jump -= Player_Jump;
        On.Celeste.Player.NormalUpdate -= Player_NormalUpdate;
        IL.Celeste.Player.NormalUpdate -= Player_NormalUpdate_il;
        On.Celeste.Player.DashBegin -= Player_DashBegin;
        On.Celeste.Player.UpdateSprite -= Player_UpdateSprite;
    }

    public static Data ExtData(this Player player) => DynamicData.For(player).Get<Data>("heavenRushData");

    private static void GetData(this Player player, out DynamicData dynamicData, out Data extData) {
        dynamicData = DynamicData.For(player);
        extData = dynamicData.Get<Data>("heavenRushData");
    }

    private static bool ShouldUseCard(this Player player) {
        if (!Input.Grab.Pressed)
            return false;

        player.GetData(out _, out var extData);
        
        var cardInventory = extData.CardInventory;

        return cardInventory.CardCount > 0 && (player.StateMachine.State != 2 || cardInventory.CardType == AbilityCardType.Yellow); 
    }

    private static int UseCard(this Player player) {
        player.GetData(out _, out var extData);
        
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
        player.GetData(out _, out var extData);
        player.PrepareForCustomDash();
        player.Sprite.Play("dash");
        Util.PlaySound("event:/classic/sfx9", 2f, player.Position);
        Celeste.Freeze(BLUE_CARD_FREEZE);

        return extData.CustomStatesIndex + (int) CustomState.BlueDash;
    }

    private static int UseGreenCard(this Player player) {
        player.GetData(out _, out var extData);
        player.PrepareForCustomDash();
        player.Speed = new Vector2(0f, GREEN_DIVE_FALL_SPEED);
        extData.CustomTrailTimer = GREEN_DIVE_TRAIL_INTERVAL;
        player.Sprite.Play("fallFast");
        Audio.Play(SFX.game_05_crackedwall_vanish, player.Position);

        return extData.CustomStatesIndex + (int) CustomState.GreenDive;
    }

    private static int UseRedCard(this Player player) {
        player.GetData(out _, out var extData);
        player.PrepareForCustomDash();
        player.Sprite.Play("dash");
        Util.PlaySound("event:/classic/sfx3", 2f, player.Position);
        Celeste.Freeze(RED_CARD_FREEZE);

        return extData.CustomStatesIndex + (int) CustomState.RedBoostDash;
    }

    private static int UseWhiteCard(this Player player) {
        player.GetData(out _, out var extData);
        player.PrepareForCustomDash();
        player.Sprite.Scale = Vector2.One;
        player.Sprite.Play("dreamDashLoop");
        player.Hair.Visible = false;
        Audio.Play(SFX.char_bad_dash_red_right, player.Position);
        extData.WhiteDashSoundSource.Play(SFX.char_mad_dreamblock_travel);
        Celeste.Freeze(WHITE_CARD_FREEZE);

        return extData.CustomStatesIndex + (int) CustomState.WhiteDash;
    }

    private static void ResetStateValues(this Player player) {
        player.GetData(out var dynamicData, out var extData);
        
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
        player.GetData(out _, out var extData);
        extData.CustomTrailTimer -= Engine.DeltaTime;

        if (extData.CustomTrailTimer > 0f)
            return;
        
        extData.CustomTrailTimer = interval;
        TrailManager.Add(player.Position, player.Sprite, player.Hair.Visible ? player.Hair : null,
            new Vector2((float) player.Facing * Math.Abs(player.Sprite.Scale.X), player.Sprite.Scale.Y),
            color, player.Depth + 1, duration);
    }

    private static bool IsInCustomDash(this Player player) {
        player.GetData(out _, out var extData);

        return (CustomState) (player.StateMachine.State - extData.CustomStatesIndex) is
            CustomState.BlueDash or
            CustomState.GreenDive or
            CustomState.RedBoostDash or
            CustomState.WhiteDash;
    }

    private static IEnumerator BlueDashCoroutine(this Player player) {
        yield return null;

        player.GetData(out var dynamicData, out var extData);
        
        int aimX = Math.Sign(dynamicData.Get<Vector2>("lastAim").X);

        if (aimX == 0)
            aimX = (int) player.Facing;

        player.DashDir.X = aimX;
        player.DashDir.Y = 0f;
        player.Speed.X = aimX * BLUE_DASH_SPEED;
        player.Speed.Y = 0f;
        extData.BlueDash = true;
        extData.CustomTrailTimer = BLUE_DASH_TRAIL_INTERVAL;
        dynamicData.Get<Level>("level").Displacement.AddBurst(player.Center, 0.4f, 8f, 64f, 0.5f, Ease.QuadOut, Ease.QuadOut);

        for (float timer = 0f; timer < BLUE_DASH_DURATION; timer += Engine.DeltaTime) {
            player.Sprite.Scale = Util.PreserveArea(Vector2.Lerp(BLUE_DASH_STRETCH, Vector2.One, timer / BLUE_DASH_DURATION));
            player.UpdateTrail(Color.Blue, BLUE_DASH_TRAIL_INTERVAL, BLUE_DASH_TRAIL_DURATION);
            
            if (timer < BLUE_DASH_ALLOW_JUMP_AT)
                dynamicData.Set("jumpGraceTimer", 0f);
            else if (Input.Jump.Pressed && dynamicData.Get<bool>("onGround")) {
                extData.BlueDash = false;
                player.Ducking = true;
                dynamicData.Invoke("SuperJump");
                player.StateMachine.State = 0;

                yield break;
            }
            
            foreach (var jumpThru in player.Scene.Tracker.GetEntities<JumpThru>()) {
                if (player.CollideCheck(jumpThru) && player.Bottom - jumpThru.Top <= 6f && !dynamicData.Invoke<bool>("DashCorrectCheck", Vector2.UnitY * (jumpThru.Top - player.Bottom)))
                    player.MoveVExact((int) (jumpThru.Top - player.Bottom));
            }

            yield return null;
        }
        
        int facing = (int) player.Facing;

        if (facing == Math.Sign(player.DashDir.X))
            player.Speed.X = facing * BLUE_DASH_END_SPEED;
        else
            player.Speed.X = 0f;

        extData.BlueDash = false;
        extData.BlueDashHyperGraceTimer = BLUE_DASH_HYPER_GRACE_PERIOD;
        player.StateMachine.State = 0;
    }

    private static void BlueDashEnd(this Player player) {
        player.GetData(out _, out var extData);
        
        if (extData.BlueDash) {
            int facing = (int) player.Facing;

            if (facing == Math.Sign(player.DashDir.X))
                player.Speed.X = facing * BLUE_DASH_END_SPEED;
            else
                player.Speed.X = 0f;
        }

        extData.BlueDash = false;
        player.Sprite.Scale = Vector2.One;
    }

    private static int GreenDiveUpdate(this Player player) {
        player.GetData(out _, out var extData);
        
        if (player.CanDash) {
            player.Sprite.Scale = Vector2.One;
            
            return player.StartDash();
        }
        
        player.UpdateTrail(Color.Green, GREEN_DIVE_TRAIL_INTERVAL, GREEN_DIVE_TRAIL_DURATION);
        player.Sprite.Scale = GREEN_DIVE_STRETCH;
        
        return extData.CustomStatesIndex + (int) CustomState.GreenDive;
    }
    
    private static IEnumerator RedBoostDashCoroutine(this Player player) {
        yield return null;
        
        player.GetData(out var dynamicData, out var extData);
        dynamicData.Get<Level>("level").Displacement.AddBurst(player.Center, 0.4f, 8f, 64f, 0.5f, Ease.QuadOut, Ease.QuadOut);
        extData.RedBoostTimer = RED_BOOST_DURATION;
        extData.CustomTrailTimer = RED_BOOST_TRAIL_INTERVAL;

        var dashDir = dynamicData.Invoke<Vector2>("CorrectDashPrecision", dynamicData.Get<Vector2>("lastAim"));
        
        if (dynamicData.Get<bool>("onGround") && dashDir.X != 0f && dashDir.Y > 0f) {
            dashDir.X = Math.Sign(dashDir.X);
            dashDir.Y = 0f;
            player.Ducking = true;
        }
        
        player.DashDir = dashDir;
        
        var newSpeed = RED_BOOST_DASH_SPEED * dashDir;
        float beforeDashSpeed = dynamicData.Get<Vector2>("beforeDashSpeed").X;
        
        if (Math.Sign(newSpeed.X) == Math.Sign(beforeDashSpeed) && Math.Abs(newSpeed.X) < Math.Abs(beforeDashSpeed))
            newSpeed.X = beforeDashSpeed;

        player.Speed = newSpeed;

        for (float timer = 0f; timer < RED_BOOST_DASH_DURATION; timer += Engine.DeltaTime) {
            player.UpdateTrail(Color.Red, RED_BOOST_TRAIL_INTERVAL, RED_BOOST_TRAIL_DURATION);
            
            if (Input.Jump.Pressed && dynamicData.Get<float>("jumpGraceTimer") > 0f) {
                player.Jump();
                player.StateMachine.State = 0;
                
                yield break;
            }

            if (player.DashDir.Y == 0f) {
                foreach (var jumpThru in player.Scene.Tracker.GetEntities<JumpThru>()) {
                    if (player.CollideCheck(jumpThru) && player.Bottom - jumpThru.Top <= 6f && !dynamicData.Invoke<bool>("DashCorrectCheck", Vector2.UnitY * (jumpThru.Top - player.Bottom)))
                        player.MoveVExact((int) (jumpThru.Top - player.Bottom));
                }
            }
            
            yield return null;
        }

        player.StateMachine.State = 0;
    }

    private static IEnumerator WhiteDashCoroutine(this Player player) {
        yield return null;
        
        player.GetData(out var dynamicData, out var extData);
        dynamicData.Get<Level>("level").Displacement.AddBurst(player.Center, 0.4f, 8f, 64f, 0.5f, Ease.QuadOut, Ease.QuadOut);
        player.DashDir = dynamicData.Invoke<Vector2>("CorrectDashPrecision", dynamicData.Get<Vector2>("lastAim"));

        if (player.DashDir.X == 0f) {
            player.Sprite.Scale = new Vector2(WHITE_DASH_STRETCH.Y, WHITE_DASH_STRETCH.X);
            player.Sprite.Rotation = 0f;
        }
        else {
            player.Sprite.Scale = WHITE_DASH_STRETCH;
            player.Sprite.Rotation = (Math.Sign(player.DashDir.X) * player.DashDir).Angle();
        }

        player.Sprite.Origin.Y = 27f;
        player.Sprite.Position.Y = -6f;
        
        float beforeDashSpeed = dynamicData.Get<Vector2>("beforeDashSpeed").X;
        float dashSpeed = WHITE_DASH_SPEED;

        if (player.DashDir.X != 0f && Math.Sign(player.DashDir.X) == Math.Sign(beforeDashSpeed) && Math.Abs(beforeDashSpeed) > dashSpeed)
            dashSpeed = Math.Abs(beforeDashSpeed);
        
        player.Speed = dashSpeed * player.DashDir;
        extData.CustomTrailTimer = WHITE_DASH_TRAIL_INTERVAL;

        while (true) {
            if (player.CanDash) {
                player.StateMachine.State = player.StartDash();

                yield break;
            }
            
            player.UpdateTrail(Color.White, WHITE_DASH_TRAIL_INTERVAL, WHITE_DASH_TRAIL_DURATION);

            var cardInventory = extData.CardInventory;
            
            if (cardInventory.CardType != AbilityCardType.White || !player.ShouldUseCard()) {
                yield return null;
                
                continue;
            }
            
            Input.Grab.ConsumeBuffer();
            cardInventory.PopCard();
            extData.UseCardCooldown = MAX_USE_CARD_COOLDOWN;

            float newSpeed = player.Speed.Length() + WHITE_DASH_REDIRECT_ADD_SPEED;

            if (newSpeed < WHITE_DASH_SPEED)
                newSpeed = WHITE_DASH_SPEED;

            player.DashDir = Vector2.Zero;
            player.Speed = Vector2.Zero;
            player.Sprite.Scale = Vector2.One;
            Audio.Play(SFX.char_bad_dash_red_right, player.Position);
            Celeste.Freeze(WHITE_CARD_REDIRECT_FREEZE);

            yield return null;

            dynamicData.Get<Level>("level").Displacement.AddBurst(player.Center, 0.4f, 8f, 64f, 0.5f, Ease.QuadOut, Ease.QuadOut);
            player.DashDir = dynamicData.Invoke<Vector2>("CorrectDashPrecision", dynamicData.Get<Vector2>("lastAim"));
            player.Speed = newSpeed * player.DashDir;

            if (player.DashDir.X == 0f) {
                player.Sprite.Scale = new Vector2(WHITE_DASH_STRETCH.Y, WHITE_DASH_STRETCH.X);
                player.Sprite.Rotation = 0f;
            }
            else {
                player.Sprite.Scale = WHITE_DASH_STRETCH;
                player.Sprite.Rotation = (Math.Sign(player.DashDir.X) * player.DashDir).Angle();
            }

            yield return null;
        }
    }

    private static void WhiteDashEnd(this Player player) {
        player.GetData(out _, out var extData);
        player.Sprite.Scale = Vector2.One;
        player.Sprite.Rotation = 0f;
        player.Sprite.Origin.Y = 32f;
        player.Sprite.Position.Y = 0f;
        player.Hair.Visible = true;
        extData.WhiteDashSoundSource.Stop();
        Audio.Play(SFX.char_bad_dreamblock_exit);
        Audio.Play(SFX.game_05_redbooster_end);
    }

    private static void BeforeBaseUpdate(Player player) {
        player.GetData(out var dynamicData, out var extData);

        bool groundBoost = (extData.RedBoostTimer > 0f || extData.GroundBoostSources > 0) && player.Speed.Y >= 0f && dynamicData.Get<bool>("onGround");
        
        if (extData.GroundBoost == groundBoost)
            return;

        extData.GroundBoost = groundBoost;
        
        if (groundBoost) {
            Audio.Play(SFX.char_mad_water_in);
            extData.GroundBoostSoundSource.Play(SFX.env_loc_waterfall_big_main);
        }
        else
            extData.GroundBoostSoundSource.Stop();
    }

    private static void OnTrueCollideH(Player player) {
        player.GetData(out _, out var extData);
        
        if (player.StateMachine.State == extData.CustomStatesIndex + (int) CustomState.WhiteDash)
            player.StateMachine.State = 0;
    }

    private static void OnTrueCollideV(Player player) {
        player.GetData(out var dynamicData, out var extData);
        
        switch ((CustomState) (player.StateMachine.State - extData.CustomStatesIndex)) {
            case CustomState.GreenDive:
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
            case CustomState.WhiteDash:
                player.StateMachine.State = 0;
                break;
        }
    }

    private static bool IsInTransitionableState(Player player) {
        player.GetData(out _, out var extData);

        return (CustomState) (player.StateMachine.State - extData.CustomStatesIndex) is CustomState.GreenDive or CustomState.WhiteDash;
    }
    
    private static float GetUltraBoostSpeed(float defaultSpeed, Player player) {
        player.GetData(out _, out var extData);
        
        return (CustomState) (player.StateMachine.State - extData.CustomStatesIndex) switch {
            CustomState.RedBoostDash when Math.Abs(player.Speed.X) < RED_BOOST_DASH_SPEED.X => player.DashDir.X * RED_BOOST_DASH_SPEED.X,
            CustomState.RedBoostDash => player.Speed.X,
            CustomState.WhiteDash => player.DashDir.X * player.Speed.Length(),
            _ => defaultSpeed
        };
    }

    private static float GetAirFrictionMultiplier(float defaultMultiplier, Player player) {
        player.GetData(out _, out var extData);

        return extData.RedBoostTimer > 0f ? RED_BOOST_AIR_FRICTION : defaultMultiplier;
    }

    private static float GetGroundFrictionMultiplier(float defaultMultiplier, Player player) {
        player.GetData(out _, out var extData);

        return extData.GroundBoost ? GROUND_BOOST_FRICTION : defaultMultiplier;
    }

    private static void Player_ctor(On.Celeste.Player.orig_ctor ctor, Player player, Vector2 position, PlayerSpriteMode spritemode) {
        ctor(player, position, spritemode);
        
        var dynamicData = DynamicData.For(player);
        var extData = new Data();
        
        dynamicData.Set("heavenRushData", extData);
        
        player.Add(extData.GroundBoostSoundSource = new SoundSource());
        player.Add(extData.WhiteDashSoundSource = new SoundSource());

        var stateMachine = player.StateMachine;
        
        extData.CustomStatesIndex = stateMachine.AddState(null, player.BlueDashCoroutine, null, player.BlueDashEnd);
        stateMachine.AddState(player.GreenDiveUpdate);
        stateMachine.AddState(null, player.RedBoostDashCoroutine);
        stateMachine.AddState(null, player.WhiteDashCoroutine, null, player.WhiteDashEnd);
    }
    
    private static bool Player_get_DashAttacking(Func<Player, bool> dashAttacking, Player player) {
        if (dashAttacking(player))
            return true;
        
        var dynamicData = DynamicData.For(player);
        var extData = dynamicData.Get<Data>("heavenRushData");
        
        return (CustomState) (player.StateMachine.State - extData.CustomStatesIndex) is CustomState.BlueDash or CustomState.RedBoostDash;
    }
    
    private static void Player_Update(On.Celeste.Player.orig_Update update, Player player) {
        player.GetData(out var dynamicData, out var extData);
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
            var level = dynamicData.Get<Level>("level");

            while (extData.GroundBoostParticleTimer < Engine.DeltaTime) {
                level.ParticlesFG.Emit(GROUND_BOOST_PARTICLE,
                    Vector2.Lerp(player.PreviousPosition, player.Position, extData.GroundBoostParticleTimer / Engine.DeltaTime),
                    player.Speed.X > 0f ? (float) Math.PI + GROUND_BOOST_PARTICLE_ANGLE : -GROUND_BOOST_PARTICLE_ANGLE);
                extData.GroundBoostParticleTimer += GROUND_BOOST_PARTICLE_INTERVAL;
            }

            extData.GroundBoostParticleTimer -= Engine.DeltaTime;
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
        cursor.Emit(OpCodes.Call, typeof(PlayerExtensions).GetMethodUnconstrained(nameof(IsInCustomDash)));
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
        cursor.Emit(OpCodes.Call, typeof(PlayerExtensions).GetMethodUnconstrained(nameof(IsInCustomDash)));
        cursor.Emit(OpCodes.Brtrue_S, branch);

        cursor.GotoNext(instr => instr.MatchLdcR4(1.2f));
        cursor.GotoNext(MoveType.After, instr => instr.OpCode == OpCodes.Mul);
        
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

    private static void Player_BeforeDownTransition_il(ILContext il) {
        var cursor = new ILCursor(il);
        
        cursor.GotoNext(MoveType.After,
            instr => instr.OpCode == OpCodes.Ldarg_0,
            instr => instr.MatchLdfld<Player>("StateMachine"),
            instr => instr.MatchCallvirt<StateMachine>("get_State"),
            instr => instr.OpCode == OpCodes.Ldc_I4_5,
            instr => instr.OpCode == OpCodes.Beq_S);

        object branch = cursor.Prev.Operand;

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Call, typeof(PlayerExtensions).GetMethodUnconstrained(nameof(IsInTransitionableState)));
        cursor.Emit(OpCodes.Brtrue_S, branch);
    }
    
    private static void Player_BeforeUpTransition_il(ILContext il) {
        var cursor = new ILCursor(il);
        
        cursor.GotoNext(MoveType.After,
            instr => instr.OpCode == OpCodes.Ldarg_0,
            instr => instr.MatchLdfld<Player>("StateMachine"),
            instr => instr.MatchCallvirt<StateMachine>("get_State"),
            instr => instr.OpCode == OpCodes.Ldc_I4_5,
            instr => instr.OpCode == OpCodes.Beq_S);

        object branch = cursor.Prev.Operand;

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Call, typeof(PlayerExtensions).GetMethodUnconstrained(nameof(IsInTransitionableState)));
        cursor.Emit(OpCodes.Brtrue_S, branch);
        
        cursor.GotoNext(MoveType.After,
            instr => instr.OpCode == OpCodes.Ldarg_0,
            instr => instr.MatchLdfld<Player>("StateMachine"),
            instr => instr.MatchCallvirt<StateMachine>("get_State"),
            instr => instr.OpCode == OpCodes.Ldc_I4_5,
            instr => instr.OpCode == OpCodes.Beq);

        branch = cursor.Prev.Operand;

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Call, typeof(PlayerExtensions).GetMethodUnconstrained(nameof(IsInTransitionableState)));
        cursor.Emit(OpCodes.Brtrue_S, branch);
    }

    private static void Player_Jump(On.Celeste.Player.orig_Jump jump, Player player, bool particles, bool playsfx) {
        player.GetData(out var dynamicData, out var extData);

        if (extData.BlueDashHyperGraceTimer > 0f) {
            player.Ducking = true;
            dynamicData.Invoke("SuperJump");
            player.StateMachine.State = 0;

            return;
        }
        
        if (extData.GroundBoost)
            Audio.Play(SFX.char_mad_water_out);
            
        jump(player, particles, playsfx);
    }
    
    private static int Player_NormalUpdate(On.Celeste.Player.orig_NormalUpdate normalUpdate, Player player) {
        player.GetData(out _, out var extData);
        
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

        cursor.GotoNext(MoveType.After,
            instr => instr.OpCode == OpCodes.Ldarg_0,
            instr => instr.MatchLdfld<Player>("onGround"),
            instr => instr.OpCode == OpCodes.Brtrue_S,
            instr => instr.MatchLdcR4(0.65f));

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Call, typeof(PlayerExtensions).GetMethodUnconstrained(nameof(GetAirFrictionMultiplier)));

        cursor.GotoNext(MoveType.After, instr => instr.MatchLdcR4(1f));

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Call, typeof(PlayerExtensions).GetMethodUnconstrained(nameof(GetGroundFrictionMultiplier)));
    }

    private static void Player_DashBegin(On.Celeste.Player.orig_DashBegin dashBegin, Player player) {
        player.GetData(out _, out var extData);
        extData.BlueDashHyperGraceTimer = 0f;
        dashBegin(player);
    }

    private static void Player_UpdateSprite(On.Celeste.Player.orig_UpdateSprite updateSprite, Player player) {
        if (!player.IsInCustomDash()) {
            updateSprite(player);

            return;
        }
        
        player.GetData(out _, out var extData);
        
        if (player.StateMachine.State == extData.CustomStatesIndex + (int) CustomState.RedBoostDash) {
            if (player.Ducking)
                player.Sprite.Play("duck");
            else
                player.Sprite.Play("dash");
        }
    }

    public class Data {
        public int CustomStatesIndex;
        public readonly CardInventory CardInventory = new();
        public float UseCardCooldown;
        public bool BlueDash;
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