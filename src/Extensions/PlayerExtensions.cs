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
    private const float YELLOW_BOUNCE_MIN_X = 80f;
    private const float YELLOW_BOUNCE_ADD_X = 40f;
    private const float YELLOW_BOUNCE_MIN_Y = -315f;
    private const float YELLOW_BOUNCE_ADD_Y = -105f;
    private const float BLUE_DASH_SPEED = 960f;
    private const float BLUE_DASH_END_SPEED = 240f;
    private const float BLUE_DASH_DURATION = 0.1f;
    private const float BLUE_DASH_ALLOW_JUMP_AT = 0.066f;
    private const float BLUE_DASH_HYPER_GRACE_PERIOD = 0.05f;
    private const float GREEN_DIVE_FALL_SPEED = 420f;
    private const float GREEN_DIVE_LAND_SPEED = 180f;
    private const float GREEN_DIVE_LAND_KILL_RADIUS = 48f;
    private static readonly Vector2 RED_BOOST_DASH_SPEED = new(280f, 240f);
    private const float RED_BOOST_DASH_DURATION = 0.15f;
    private const float RED_BOOST_DURATION = 0.8f;
    private const float RED_BOOST_AIR_FRICTION = 0.325f;
    private const float WHITE_DASH_SPEED = 325f;
    private const float WHITE_DASH_REDIRECT_ADD_SPEED = 40f;
    private const float WHITE_DASH_SUPER_GRACE_PERIOD = 0.083f;
    private const float GROUND_BOOST_FRICTION = 0.1f;
    private const float GROUND_BOOST_SPEED = 240f;
    private const float GROUND_BOOST_ACCELERATION = 650f;

    private static readonly ParticleType RED_BOOST_PARTICLE = new() {
        Color = Color.Red,
        Color2 = Color.Orange,
        ColorMode = ParticleType.ColorModes.Choose,
        FadeMode = ParticleType.FadeModes.Late,
        LifeMin = 0.1f,
        LifeMax = 0.3f,
        Size = 1f,
        SpeedMin = 10f,
        SpeedMax = 20f,
        DirectionRange = MathHelper.TwoPi
    };

    private static readonly ParticleType SURF_PARTICLE = new() {
        Color = Color.Aquamarine,
        ColorMode = ParticleType.ColorModes.Static,
        FadeMode = ParticleType.FadeModes.Late,
        LifeMin = 0.03f,
        LifeMax = 0.06f,
        Size = 1f,
        SpeedMin = 60f,
        SpeedMax = 120f,
        DirectionRange = 0.7f
    };

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

    public static bool HitDemon(this Player player) {
        player.GetData(out var dynamicData, out var extData);

        int state = player.StateMachine.State;
        
        if (!player.DashAttacking && extData.RedBoostTimer == 0f && state != extData.WhiteDashIndex)
            return false;

        if (state == extData.BlueDashIndex)
            extData.KilledInBlueDash = true;
        else if (state == extData.WhiteDashIndex) {
            dynamicData.Set("jumpGraceTimer", WHITE_DASH_SUPER_GRACE_PERIOD);
            extData.WhiteDashSuperGraceTimer = WHITE_DASH_SUPER_GRACE_PERIOD;
            player.StateMachine.State = 0;
        }

        return true;
    }

    private static void GetData(this Player player, out DynamicData dynamicData, out Data extData) {
        dynamicData = DynamicData.For(player);
        extData = dynamicData.Get<Data>("heavenRushData");
    }

    private static bool ShouldUseCard(this Player player) {
        if (!HeavenRushModule.Session.HeavenRushModeEnabled || !Input.Grab.Pressed)
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
        Celeste.Freeze(0.033f);
        dynamicData.Get<Level>("level").Add(Engine.Pooler.Create<SpeedRing>().Init(player.Center, MathHelper.PiOver2, Color.White));
        player.ResetStateValues();
        player.Sprite.Scale = new Vector2(0.4f, 1.8f);

        var previousSpeed = player.Speed;
        
        player.Speed = Vector2.Zero;

        player.Add(new Coroutine(Util.AfterFrame(() => {
            int moveX = Input.MoveX.Value;
            float newSpeedX = previousSpeed.X + moveX * YELLOW_BOUNCE_ADD_X;

            if (moveX != 0 && moveX * newSpeedX < YELLOW_BOUNCE_MIN_X)
                newSpeedX = moveX * YELLOW_BOUNCE_MIN_X;

            float newSpeedY = previousSpeed.Y + YELLOW_BOUNCE_ADD_Y;

            if (newSpeedY > YELLOW_BOUNCE_MIN_Y)
                newSpeedY = YELLOW_BOUNCE_MIN_Y;
            
            player.Speed.X = newSpeedX;
            player.Speed.Y = newSpeedY;
        })));

        return 0;
    }

    private static int UseBlueCard(this Player player) {
        player.GetData(out _, out var extData);
        player.PrepareForCustomDash();
        extData.KilledInBlueDash = false;
        player.Sprite.Play("dash");
        Audio.Play("event:/heavenRush/game/blue_dash", player.Position);
        Celeste.Freeze(0.05f);

        return extData.BlueDashIndex;
    }

    private static int UseGreenCard(this Player player) {
        player.GetData(out _, out var extData);
        player.PrepareForCustomDash();
        player.Speed = new Vector2(0f, GREEN_DIVE_FALL_SPEED);
        extData.CustomTrailTimer = 0.016f;
        player.Sprite.Play("fallFast");
        Audio.Play(SFX.game_05_crackedwall_vanish, player.Position);

        return extData.GreenDiveIndex;
    }

    private static int UseRedCard(this Player player) {
        player.GetData(out _, out var extData);
        player.PrepareForCustomDash();
        player.Sprite.Play("dash");
        Audio.Play("event:/heavenRush/game/red_boost_dash", player.Position);
        Celeste.Freeze(0.05f);

        return extData.RedBoostDashIndex;
    }

    private static int UseWhiteCard(this Player player) {
        player.GetData(out _, out var extData);
        player.PrepareForCustomDash();
        player.Sprite.Scale = Vector2.One;
        player.Sprite.Play("dreamDashLoop");
        player.Hair.Visible = false;
        Audio.Play(SFX.char_bad_dash_red_right, player.Position);
        extData.WhiteDashSoundSource.Play(SFX.char_mad_dreamblock_travel);
        extData.WhiteDashSoundSource.DisposeOnTransition = false;
        Celeste.Freeze(0.05f);

        return extData.WhiteDashIndex;
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
        if (!HeavenRushModule.Session.HeavenRushModeEnabled)
            return false;
        
        player.GetData(out _, out var extData);

        int state = player.StateMachine.State;

        return state == extData.BlueDashIndex
               || state == extData.GreenDiveIndex
               || state == extData.RedBoostDashIndex
               || state == extData.WhiteDashIndex;
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
        extData.CustomTrailTimer = 0.016f;
        dynamicData.Get<Level>("level").Displacement.AddBurst(player.Center, 0.4f, 8f, 64f, 0.5f, Ease.QuadOut, Ease.QuadOut);

        for (float timer = 0f; timer < BLUE_DASH_DURATION; timer += Engine.DeltaTime) {
            player.Sprite.Scale = Util.PreserveArea(Vector2.Lerp(new Vector2(2f, 0.5f), Vector2.One, timer / BLUE_DASH_DURATION));
            player.UpdateTrail(Color.Blue, 0.016f, 0.66f);
            
            if (timer < BLUE_DASH_ALLOW_JUMP_AT)
                dynamicData.Set("jumpGraceTimer", 0f);
            else if (Input.Jump.Pressed && (extData.KilledInBlueDash || dynamicData.Get<bool>("onGround"))) {
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
        
        if (extData.KilledInBlueDash)
            dynamicData.Set("jumpGraceTimer", BLUE_DASH_HYPER_GRACE_PERIOD);
        
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
        
        player.UpdateTrail(Color.Green, 0.016f, 0.33f);
        player.Sprite.Scale = new Vector2(0.5f, 2f);
        
        return extData.GreenDiveIndex;
    }
    
    private static IEnumerator RedBoostDashCoroutine(this Player player) {
        yield return null;
        
        player.GetData(out var dynamicData, out var extData);
        dynamicData.Get<Level>("level").Displacement.AddBurst(player.Center, 0.4f, 8f, 64f, 0.5f, Ease.QuadOut, Ease.QuadOut);
        extData.RedBoostTimer = RED_BOOST_DURATION;
        extData.CustomTrailTimer = 0.016f;
        extData.RedBoostSoundSource.Play("event:/heavenRush/game/red_boost_sustain");
        extData.RedBoostSoundSource.DisposeOnTransition = false;

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
            player.UpdateTrail(Color.Red, 0.016f, 0.5f);
            
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

        Vector2 whiteDashStretch = new(2f, 0.5f);
        if (player.DashDir.X == 0f) {
            player.Sprite.Scale = new Vector2(whiteDashStretch.Y, whiteDashStretch.X);
            player.Sprite.Rotation = 0f;
        }
        else {
            player.Sprite.Scale = whiteDashStretch;
            player.Sprite.Rotation = (Math.Sign(player.DashDir.X) * player.DashDir).Angle();
        }

        player.Sprite.Origin.Y = 27f;
        player.Sprite.Position.Y = -6f;
        
        float beforeDashSpeed = dynamicData.Get<Vector2>("beforeDashSpeed").X;
        float dashSpeed = WHITE_DASH_SPEED;

        if (player.DashDir.X != 0f && Math.Sign(player.DashDir.X) == Math.Sign(beforeDashSpeed) && Math.Abs(beforeDashSpeed) > dashSpeed)
            dashSpeed = Math.Abs(beforeDashSpeed);
        
        player.Speed = dashSpeed * player.DashDir;
        extData.CustomTrailTimer = 0.016f;

        while (true) {
            if (player.CanDash) {
                player.StateMachine.State = player.StartDash();

                yield break;
            }
            
            player.UpdateTrail(Color.White, 0.016f, 0.33f);

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
            Celeste.Freeze(0.033f);

            yield return null;

            dynamicData.Get<Level>("level").Displacement.AddBurst(player.Center, 0.4f, 8f, 64f, 0.5f, Ease.QuadOut, Ease.QuadOut);
            player.DashDir = dynamicData.Invoke<Vector2>("CorrectDashPrecision", dynamicData.Get<Vector2>("lastAim"));
            player.Speed = newSpeed * player.DashDir;

            if (player.DashDir.X == 0f) {
                player.Sprite.Scale = new Vector2(whiteDashStretch.Y, whiteDashStretch.X);
                player.Sprite.Rotation = 0f;
            }
            else {
                player.Sprite.Scale = whiteDashStretch;
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
        
        if (groundBoost)
            Audio.Play(SFX.char_mad_water_in);
    }

    private static void OnTrueCollideH(Player player) {
        if (!HeavenRushModule.Session.HeavenRushModeEnabled)
            return;
        
        player.GetData(out _, out var extData);
        
        if (player.StateMachine.State == extData.WhiteDashIndex)
            player.StateMachine.State = 0;
    }

    private static void OnTrueCollideV(Player player) {
        if (!HeavenRushModule.Session.HeavenRushModeEnabled)
            return;
        
        player.GetData(out var dynamicData, out var extData);

        int state = player.StateMachine.State;

        if (state == extData.GreenDiveIndex) {
            player.Speed.X = Math.Sign(Input.MoveX.Value) * GREEN_DIVE_LAND_SPEED;
            player.Sprite.Scale = new Vector2(1.5f, 0.75f);
            Audio.Play(SFX.game_gen_fallblock_impact, player.Position);
            Celeste.Freeze(0.05f);
            player.StateMachine.State = 0;

            var level = dynamicData.Get<Level>("level");

            level.Particles.Emit(Player.P_SummitLandA, 12, player.BottomCenter, Vector2.UnitX * 3f, -1.5707964f);
            level.Particles.Emit(Player.P_SummitLandB, 8, player.BottomCenter - Vector2.UnitX * 2f, Vector2.UnitX * 2f, 3.403392f);
            level.Particles.Emit(Player.P_SummitLandB, 8, player.BottomCenter + Vector2.UnitX * 2f, Vector2.UnitX * 2f, -0.2617994f);
            level.Displacement.AddBurst(player.Center, 0.4f, 16f, 128f, 1f, Ease.QuadOut, Ease.QuadOut);

            bool anyKilled = false;
            
            foreach (var entity in player.Scene.Tracker.GetEntities<Demon>()) {
                if (Vector2.DistanceSquared(player.Position, entity.Position) > GREEN_DIVE_LAND_KILL_RADIUS * GREEN_DIVE_LAND_KILL_RADIUS)
                    continue;
                
                ((Demon) entity).Die((entity.Position - player.Position).Angle());
                anyKilled = true;
            }

            if (anyKilled) {
                player.RefillDash();
                Audio.Play(SFX.game_09_iceball_break, player.Position);
            }
        }
        else if (state == extData.WhiteDashIndex)
            player.StateMachine.State = 0;
        
        if ((extData.RedBoostTimer > 0f || extData.GroundBoostSources > 0f) && player.OnGround())
            Util.PlaySound(SFX.char_mad_water_in, 2f);
    }

    private static bool IsInTransitionableState(Player player) {
        if (!HeavenRushModule.Session.HeavenRushModeEnabled)
            return false;
        
        player.GetData(out _, out var extData);

        int state = player.StateMachine.State;

        return state == extData.GreenDiveIndex || state == extData.WhiteDashIndex;
    }
    
    private static float GetUltraBoostSpeed(float defaultSpeed, Player player) {
        if (!HeavenRushModule.Session.HeavenRushModeEnabled)
            return defaultSpeed;
        
        player.GetData(out _, out var extData);

        int state = player.StateMachine.State;

        if (state == extData.RedBoostDashIndex) {
            if (Math.Abs(player.Speed.X) < RED_BOOST_DASH_SPEED.X)
                return player.DashDir.X * RED_BOOST_DASH_SPEED.X;

            return player.Speed.X;
        }

        if (state == extData.WhiteDashIndex)
            return player.DashDir.X * player.Speed.Length();

        return defaultSpeed;
    }

    private static float GetAirFrictionMultiplier(float defaultMultiplier, Player player) {
        if (!HeavenRushModule.Session.HeavenRushModeEnabled)
            return defaultMultiplier;
        
        player.GetData(out _, out var extData);

        return extData.RedBoostTimer > 0f ? RED_BOOST_AIR_FRICTION : defaultMultiplier;
    }

    private static float GetGroundFrictionMultiplier(float defaultMultiplier, Player player) {
        if (!HeavenRushModule.Session.HeavenRushModeEnabled)
            return defaultMultiplier;
        
        player.GetData(out _, out var extData);

        return extData.GroundBoost ? GROUND_BOOST_FRICTION : defaultMultiplier;
    }

    private static void Player_ctor(On.Celeste.Player.orig_ctor ctor, Player player, Vector2 position, PlayerSpriteMode spritemode) {
        ctor(player, position, spritemode);
        
        var dynamicData = DynamicData.For(player);
        var extData = new Data();
        
        dynamicData.Set("heavenRushData", extData);
        
        player.Add(extData.RedBoostSoundSource = new SoundSource());
        player.Add(extData.WhiteDashSoundSource = new SoundSource());

        var stateMachine = player.StateMachine;
        
        extData.BlueDashIndex = stateMachine.AddState(null, player.BlueDashCoroutine, null, player.BlueDashEnd);
        extData.GreenDiveIndex = stateMachine.AddState(player.GreenDiveUpdate);
        extData.RedBoostDashIndex = stateMachine.AddState(null, player.RedBoostDashCoroutine);
        extData.WhiteDashIndex = stateMachine.AddState(null, player.WhiteDashCoroutine, null, player.WhiteDashEnd);
    }
    
    private static bool Player_get_DashAttacking(Func<Player, bool> dashAttacking, Player player) {
        if (dashAttacking(player))
            return true;

        if (!HeavenRushModule.Session.HeavenRushModeEnabled)
            return false;
        
        var dynamicData = DynamicData.For(player);
        var extData = dynamicData.Get<Data>("heavenRushData");
        int state = player.StateMachine.State;

        return state == extData.BlueDashIndex || state == extData.RedBoostDashIndex;
    }
    
    private static void Player_Update(On.Celeste.Player.orig_Update update, Player player) {
        if (!HeavenRushModule.Session.HeavenRushModeEnabled) {
            update(player);
            
            return;
        }
        
        player.GetData(out var dynamicData, out var extData);
        extData.UseCardCooldown -= Engine.DeltaTime;

        if (extData.UseCardCooldown < 0f)
            extData.UseCardCooldown = 0f;

        extData.BlueDashHyperGraceTimer -= Engine.DeltaTime;

        if (extData.BlueDashHyperGraceTimer < 0f)
            extData.BlueDashHyperGraceTimer = 0f;

        extData.RedBoostTimer -= Engine.DeltaTime;
        
        if (extData.RedBoostTimer <= 0f) {
            extData.RedBoostTimer = 0f;

            if (extData.RedBoostSoundSource.Playing)
                extData.RedBoostSoundSource.Stop();
        }

        extData.WhiteDashSuperGraceTimer -= Engine.DeltaTime;

        if (extData.WhiteDashSuperGraceTimer < 0f)
            extData.WhiteDashSuperGraceTimer = 0f;

        update(player);
        
        var level = dynamicData.Get<Level>("level");

        if (extData.GroundBoostSources > 0f && dynamicData.Get<bool>("onGround") && Math.Abs(player.Speed.X) > 64f) {
            float interval = 0.008f / Math.Min(Math.Abs(player.Speed.X) / GROUND_BOOST_SPEED, 1f);
            var offset = -4f * Math.Sign(player.Speed.X) * Vector2.UnitX;
            float angle = player.Speed.X > 0f ? MathHelper.Pi + 0.4f : -0.4f;

            foreach (var position in Util.TemporalLerp(ref extData.SurfParticleTimer, interval, player.PreviousPosition, player.Position, Engine.DeltaTime))
                level.ParticlesBG.Emit(SURF_PARTICLE, position + offset, angle);
        }
        else
            extData.SurfParticleTimer = 0f;

        if (extData.RedBoostTimer > 0f && player.Speed.Length() > 64f) {
            float interval = 0.008f / Math.Min(Math.Abs(player.Speed.Length()) / RED_BOOST_DASH_SPEED.X, 1f);
            var offset = -6f * Vector2.UnitY;
            
            foreach (var position in Util.TemporalLerp(ref extData.RedBoostParticleTimer, interval, player.PreviousPosition, player.Position, Engine.DeltaTime))
                level.ParticlesBG.Emit(RED_BOOST_PARTICLE, 1, position + offset, 6f * Vector2.One);
        }
        else
            extData.RedBoostParticleTimer = 0f;
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
        if (!HeavenRushModule.Session.HeavenRushModeEnabled) {
            jump(player, particles, playsfx);
            
            return;
        }
        
        player.GetData(out var dynamicData, out var extData);

        if (extData.BlueDashHyperGraceTimer > 0f) {
            player.Ducking = true;
            dynamicData.Invoke("SuperJump");
            player.StateMachine.State = 0;

            return;
        }

        if (extData.WhiteDashSuperGraceTimer > 0f) {
            float speedBefore = Math.Abs(player.Speed.X);
            
            player.Ducking = false;
            dynamicData.Invoke("SuperJump");

            if (Math.Abs(player.Speed.X) < speedBefore)
                player.Speed.X = speedBefore * Math.Sign(player.Speed.X);

            return;
        }
        
        if (extData.GroundBoost)
            Util.PlaySound(SFX.char_mad_water_out, 2f);
            
        jump(player, particles, playsfx);
    }
    
    private static int Player_NormalUpdate(On.Celeste.Player.orig_NormalUpdate normalUpdate, Player player) {
        if (!HeavenRushModule.Session.HeavenRushModeEnabled)
            return normalUpdate(player);

        player.GetData(out _, out var extData);
        
        if (extData.RedBoostTimer > 0f)
            player.UpdateTrail(Color.Red * Math.Min(4f * extData.RedBoostTimer, 1f), 0.016f, 0.16f);
        
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
        if (!HeavenRushModule.Session.HeavenRushModeEnabled) {
            dashBegin(player);
            
            return;
        }
        
        player.GetData(out _, out var extData);
        extData.BlueDashHyperGraceTimer = 0f;
        extData.WhiteDashSuperGraceTimer = 0f;
        dashBegin(player);
    }

    private static void Player_UpdateSprite(On.Celeste.Player.orig_UpdateSprite updateSprite, Player player) {
        if (!HeavenRushModule.Session.HeavenRushModeEnabled || !player.IsInCustomDash()) {
            updateSprite(player);

            return;
        }
        
        player.GetData(out _, out var extData);
        
        if (player.StateMachine.State == extData.RedBoostDashIndex) {
            if (player.Ducking)
                player.Sprite.Play("duck");
            else
                player.Sprite.Play("dash");
        }
    }

    public class Data {
        public int BlueDashIndex;
        public int GreenDiveIndex;
        public int RedBoostDashIndex;
        public int WhiteDashIndex;
        public readonly CardInventory CardInventory = new();
        public float UseCardCooldown;
        public bool BlueDash;
        public bool KilledInBlueDash;
        public float BlueDashHyperGraceTimer;
        public float RedBoostTimer;
        public float RedBoostParticleTimer;
        public SoundSource RedBoostSoundSource;
        public float WhiteDashSuperGraceTimer;
        public SoundSource WhiteDashSoundSource;
        public float CustomTrailTimer;
        public bool GroundBoost;
        public int GroundBoostSources;
        public float SurfParticleTimer;
    }
}