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
    private const float USE_CARD_COOLDOWN = 0.1f;
    private const float YELLOW_BOUNCE_MIN_X = 80f;
    private const float YELLOW_BOUNCE_ADD_X = 40f;
    private const float YELLOW_BOUNCE_MIN_Y = -315f;
    private const float YELLOW_BOUNCE_ADD_Y = -105f;
    private const float BLUE_DASH_SPEED = 1080f;
    private const float BLUE_DASH_END_SPEED = 240f;
    private const float BLUE_DASH_DURATION = 0.1f;
    private const float BLUE_DASH_ALLOW_JUMP_AT = 0.066f;
    private const float BLUE_DASH_HYPER_GRACE_PERIOD = 0.05f;
    private const float GREEN_DIVE_FALL_SPEED = 420f;
    private const float GREEN_DIVE_LAND_SPEED = 180f;
    private const float GREEN_DIVE_LAND_KILL_RADIUS = 48f;
    private static readonly Vector2 RED_BOOST_DASH_SPEED = new(280f, 240f);
    private const float RED_BOOST_DASH_DURATION = 0.15f;
    private const float RED_BOOST_DURATION = 1f;
    private const float RED_BOOST_AIR_FRICTION = 0.4f;
    private const float RED_BOOST_STORED_SPEED_DURATION = 0.083f;
    private const float WHITE_DASH_SPEED = 325f;
    private const float WHITE_DASH_REDIRECT_ADD_SPEED = 40f;
    private const float WHITE_DASH_SUPER_GRACE_PERIOD = 0.083f;
    private const float GROUND_BOOST_FRICTION = 0.05f;
    private const float GROUND_BOOST_SPEED = 280f;
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
        LifeMin = 0.016f,
        LifeMax = 0.033f,
        Size = 1f,
        SpeedMin = 200f,
        SpeedMax = 250f,
        DirectionRange = 0.7f
    };

    private static IDetour On_Celeste_Player_get_CanRetry;
    private static IDetour IL_Celeste_Player_orig_Added;
    private static IDetour IL_Celeste_Player_orig_Update;
    
    public static void Load() {
        On.Celeste.Player.ctor += Player_ctor;
        On_Celeste_Player_get_CanRetry =new Hook(typeof(Player).GetPropertyUnconstrained("CanRetry").GetGetMethod(), Player_get_CanRetry);
        IL_Celeste_Player_orig_Added = new ILHook(typeof(Player).GetMethodUnconstrained("orig_Added"), Player_orig_Added_il);
        On.Celeste.Player.Update += Player_Update;
        IL_Celeste_Player_orig_Update = new ILHook(typeof(Player).GetMethodUnconstrained("orig_Update"), Player_orig_Update_il);
        On.Celeste.Player.OnCollideH += Player_OnCollideH;
        IL.Celeste.Player.OnCollideH += Player_OnCollideH_il;
        On.Celeste.Player.OnCollideV += Player_OnCollideV;
        IL.Celeste.Player.OnCollideV += Player_OnCollideV_il;
        IL.Celeste.Player.BeforeDownTransition += Player_BeforeDownTransition_il;
        IL.Celeste.Player.BeforeUpTransition += Player_BeforeUpTransition_il;
        On.Celeste.Player.Jump += Player_Jump;
        On.Celeste.Player.WallJump += Player_WallJump;
        On.Celeste.Player.NormalUpdate += Player_NormalUpdate;
        IL.Celeste.Player.NormalUpdate += Player_NormalUpdate_il;
        On.Celeste.Player.DashBegin += Player_DashBegin;
        On.Celeste.Player.IntroRespawnEnd += Player_IntroRespawnEnd;
        On.Celeste.Player.UpdateSprite += Player_UpdateSprite;
    }

    public static void Unload() {
        On.Celeste.Player.ctor -= Player_ctor;
        On_Celeste_Player_get_CanRetry.Dispose();
        IL_Celeste_Player_orig_Added.Dispose();
        On.Celeste.Player.Update -= Player_Update;
        IL_Celeste_Player_orig_Update.Dispose();
        IL.Celeste.Player.OnCollideH -= Player_OnCollideH_il;
        IL.Celeste.Player.OnCollideV -= Player_OnCollideV_il;
        IL.Celeste.Player.BeforeDownTransition -= Player_BeforeDownTransition_il;
        IL.Celeste.Player.BeforeUpTransition -= Player_BeforeUpTransition_il;
        On.Celeste.Player.Jump -= Player_Jump;
        On.Celeste.Player.WallJump -= Player_WallJump;
        IL.Celeste.Player.NormalUpdate -= Player_NormalUpdate_il;
        On.Celeste.Player.DashBegin -= Player_DashBegin;
        On.Celeste.Player.IntroRespawnEnd -= Player_IntroRespawnEnd;
        On.Celeste.Player.UpdateSprite -= Player_UpdateSprite;
    }

    public static void Spawn(this Player player) {
        player.Active = true;
        player.Visible = true;
        player.StateMachine.State = 14;
    }

    public static bool GiveCard(this Player player, AbilityCardType cardType) {
        player.GetData(out _, out var extData);

        var cardInventory = extData.CardInventory;

        if (!cardInventory.TryAddCard(cardType))
            return false;

        extData.CardInventoryIndicator ??= player.Scene.Tracker.GetEntity<CardInventoryIndicator>();

        if (extData.CardInventoryIndicator == null)
            player.Scene.Add(extData.CardInventoryIndicator = new CardInventoryIndicator());
        
        extData.CardInventoryIndicator.UpdateInventory(cardInventory.CardType, cardInventory.CardCount);
        extData.CardInventoryIndicator.PlayAnimation();
        Input.Grab.BufferTime = 0.08f;

        return true;
    }

    public static bool HitDemon(this Player player) {
        player.GetData(out _, out var extData);

        int state = player.StateMachine.State;
        
        if (!player.DashAttacking
            && extData.RedBoostTimer == 0f
            && state != 2
            && state != extData.BlueDashIndex
            && state != extData.GreenDiveIndex
            && state != extData.WhiteDashIndex)
            return false;

        if (state == extData.BlueDashIndex)
            extData.KilledInBlueDash = true;
        else if (state == extData.WhiteDashIndex) {
            extData.WhiteDashSuperGraceTimer = WHITE_DASH_SUPER_GRACE_PERIOD;
            player.StateMachine.State = 0;
        }

        return true;
    }

    private static void GetData(this Player player, out DynamicData dynamicData, out Data extData) {
        dynamicData = DynamicData.For(player);
        extData = dynamicData.Get<Data>("heavenRushData");
    }

    private static bool TryPopCard(this Player player) {
        if (!Input.Grab.Pressed)
            return false;
        
        player.GetData(out _, out var extData);
        
        var cardInventory = extData.CardInventory;

        if (cardInventory.CardCount == 0)
            return false;
        
        Input.Grab.ConsumeBuffer();
        cardInventory.PopCard();
        extData.UseCardCooldown = USE_CARD_COOLDOWN;
        extData.CardInventoryIndicator.UpdateInventory(cardInventory.CardType, cardInventory.CardCount);
        extData.CardInventoryIndicator.StopAnimation();
   
        if (cardInventory.CardCount == 0)
            Input.Grab.BufferTime = 0f;

        return true;
    }

    private static int UseYellowCard(this Player player) {
        Audio.Play(SFX.game_gen_thing_booped, player.Position);
        Celeste.Freeze(0.016f);
        player.Scene.Add(Engine.Pooler.Create<SpeedRing>().Init(player.Center, MathHelper.PiOver2, Color.White));
        player.ResetStateValues();
        player.Sprite.Scale = new Vector2(0.4f, 1.8f);

        var previousSpeed = player.Speed;
        
        player.Speed = Vector2.Zero;

        player.Add(new Coroutine(Util.NextFrame(() => {
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
        
        int aimX = Math.Sign(dynamicData.Invoke<Vector2>("CorrectDashPrecision", dynamicData.Get<Vector2>("lastAim")).X);

        if (aimX == 0)
            aimX = (int) player.Facing;

        player.DashDir.X = aimX;
        player.DashDir.Y = 0f;
        player.Speed.X = aimX * BLUE_DASH_SPEED;
        player.Speed.Y = 0f;
        extData.CustomTrailTimer = 0.016f;
        dynamicData.Get<Level>("level").Displacement.AddBurst(player.Center, 0.4f, 8f, 64f, 0.5f, Ease.QuadOut, Ease.QuadOut);
        SlashFx.Burst(player.Center, player.DashDir.Angle());

        for (float timer = 0f; timer < BLUE_DASH_DURATION; timer += Engine.DeltaTime) {
            player.Sprite.Scale = Util.PreserveArea(Vector2.Lerp(new Vector2(2f, 0.5f), Vector2.One, timer / BLUE_DASH_DURATION));
            player.UpdateTrail(Color.Blue, 0.016f, 0.66f);
            
            if (Input.Jump.Pressed && timer >= BLUE_DASH_ALLOW_JUMP_AT && (extData.KilledInBlueDash || dynamicData.Get<bool>("onGround"))) {
                player.StateMachine.State = 0;
                player.Ducking = true;
                dynamicData.Invoke("SuperJump");

                yield break;
            }
            
            foreach (var jumpThru in player.Scene.Tracker.GetEntities<JumpThru>()) {
                if (player.CollideCheck(jumpThru) && player.Bottom - jumpThru.Top <= 6f && !dynamicData.Invoke<bool>("DashCorrectCheck", Vector2.UnitY * (jumpThru.Top - player.Bottom)))
                    player.MoveVExact((int) (jumpThru.Top - player.Bottom));
            }

            yield return null;
        }
        
        if (extData.KilledInBlueDash || dynamicData.Get<bool>("onGround"))
            extData.BlueDashHyperGraceTimer = BLUE_DASH_HYPER_GRACE_PERIOD;
        
        player.StateMachine.State = 0;
    }

    private static void BlueDashEnd(this Player player) {
        player.GetData(out var dynamicData, out _);
        dynamicData.Set("jumpGraceTimer", 0f);
        
        int facing = Math.Sign(Input.MoveX.Value);

        if (facing == Math.Sign(player.DashDir.X))
            player.Speed.X = facing * BLUE_DASH_END_SPEED;
        else
            player.Speed.X = 0f;

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
        SlashFx.Burst(player.Center, player.DashDir.Angle());
        
        var newSpeed = RED_BOOST_DASH_SPEED * dashDir;
        var beforeDashSpeed = dynamicData.Get<Vector2>("beforeDashSpeed");
        
        if (Math.Sign(newSpeed.X) == Math.Sign(beforeDashSpeed.X) && Math.Abs(newSpeed.X) < Math.Abs(beforeDashSpeed.X))
            newSpeed.X = beforeDashSpeed.X;
        
        if (Math.Sign(newSpeed.Y) == Math.Sign(beforeDashSpeed.Y) && Math.Abs(newSpeed.Y) < Math.Abs(beforeDashSpeed.Y))
            newSpeed.Y = beforeDashSpeed.Y;

        player.Speed = newSpeed;

        for (float timer = 0f; timer < RED_BOOST_DASH_DURATION; timer += Engine.DeltaTime) {
            if (Input.Jump.Pressed) {
                if (player.DashDir.Y >= 0f && dynamicData.Get<float>("jumpGraceTimer") > 0f) {
                    player.Jump();
                    player.StateMachine.State = 0;
                    
                    yield break;
                }

                if (dynamicData.Invoke<bool>("WallJumpCheck", 1)) {
                    dynamicData.Invoke("WallJump", -1);
                    player.StateMachine.State = 0;
                    
                    yield break;
                }
                
                if (dynamicData.Invoke<bool>("WallJumpCheck", -1)) {
                    dynamicData.Invoke("WallJump", 1);
                    player.StateMachine.State = 0;
                    
                    yield break;
                }
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
            
            if (cardInventory.CardType != AbilityCardType.White || !player.TryPopCard()) {
                yield return null;
                
                continue;
            }

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

    private static bool TryInterceptRespawn(Player player) {
        var levelController = player.Scene.Tracker.GetEntity<RushLevelController>();

        if (levelController == null)
            return false;
        
        player.Active = false;
        player.Visible = false;
        
        return true;
    }
    
    private static void BeforeBaseUpdate(Player player) {
        player.GetData(out _, out var extData);

        bool check = player.CollideCheck<SurfPlatform>(player.Position + Vector2.UnitY);

        if (check && !extData.Surfing) {
            Audio.Play(SFX.char_mad_water_in);
            extData.SurfSoundSource.Play("event:/heavenRush/game/surf", "fade", MathHelper.Min(Math.Abs(player.Speed.X) / GROUND_BOOST_SPEED, 1f));
        }
        else if (!check && extData.SurfSoundSource.Playing)
            extData.SurfSoundSource.Stop();

        extData.Surfing = check;
    }

    private static void OnTrueCollideH(Player player) {
        player.GetData(out _, out var extData);
        
        if (player.StateMachine.State == extData.WhiteDashIndex)
            player.StateMachine.State = 0;

        if (extData.RedBoostTimer > 0f) {
            extData.RedBoostStoredSpeed = -player.Speed.X;
            extData.RedBoostStoredSpeedTimer = RED_BOOST_STORED_SPEED_DURATION;
        }
    }

    private static void OnTrueCollideV(Player player) {
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

            if (Demon.KillInRadius(player.Scene, player.Center, GREEN_DIVE_LAND_KILL_RADIUS))
                player.RefillDash();
        }
        else if (state == extData.WhiteDashIndex)
            player.StateMachine.State = 0;
    }

    private static bool IsInTransitionableState(Player player) {
        player.GetData(out _, out var extData);

        int state = player.StateMachine.State;

        return state == extData.GreenDiveIndex || state == extData.WhiteDashIndex;
    }
    
    private static float GetUltraBoostSpeed(float defaultSpeed, Player player) {
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

    private static int UseCard(Player player) {
        player.GetData(out _, out var extData);

        return extData.CardInventory.CardType switch {
            AbilityCardType.Yellow => player.UseYellowCard(),
            AbilityCardType.Blue => player.UseBlueCard(),
            AbilityCardType.Green => player.UseGreenCard(),
            AbilityCardType.Red => player.UseRedCard(),
            AbilityCardType.White => player.UseWhiteCard(),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private static float GetAirFrictionMultiplier(float defaultMultiplier, Player player) {
        player.GetData(out _, out var extData);

        return extData.RedBoostTimer > 0f ? RED_BOOST_AIR_FRICTION : defaultMultiplier;
    }

    private static float GetGroundFrictionMultiplier(float defaultMultiplier, Player player) {
        player.GetData(out _, out var extData);

        return extData.RedBoostTimer > 0f || extData.Surfing ? GROUND_BOOST_FRICTION : defaultMultiplier;
    }

    private static bool TryDoCustomJump(Player player) {
        player.GetData(out var dynamicData, out var extData);

        if (extData.BlueDashHyperGraceTimer > 0f) {
            player.Ducking = true;
            dynamicData.Invoke("SuperJump");

            return true;
        }

        if (extData.WhiteDashSuperGraceTimer > 0f) {
            float speedBefore = Math.Abs(player.Speed.X);
            
            player.Ducking = false;
            dynamicData.Invoke("SuperJump");

            if (Math.Abs(player.Speed.X) < speedBefore)
                player.Speed.X = speedBefore * Math.Sign(player.Speed.X);

            return true;
        }

        return false;
    }

    private static void Player_ctor(On.Celeste.Player.orig_ctor ctor, Player player, Vector2 position, PlayerSpriteMode spritemode) {
        ctor(player, position, spritemode);
        
        var dynamicData = DynamicData.For(player);
        var extData = new Data();
        
        dynamicData.Set("heavenRushData", extData);
        
        player.Add(extData.RedBoostSoundSource = new SoundSource());
        player.Add(extData.WhiteDashSoundSource = new SoundSource());
        player.Add(extData.SurfSoundSource = new SoundSource());

        var stateMachine = player.StateMachine;

        extData.BlueDashIndex = stateMachine.AddState(null, player.BlueDashCoroutine, null, player.BlueDashEnd);
        extData.GreenDiveIndex = stateMachine.AddState(player.GreenDiveUpdate);
        extData.RedBoostDashIndex = stateMachine.AddState(null, player.RedBoostDashCoroutine);
        extData.WhiteDashIndex = stateMachine.AddState(null, player.WhiteDashCoroutine, null, player.WhiteDashEnd);
    }

    private static bool Player_get_CanRetry(Func<Player, bool> canRetry, Player player) => player.Active && canRetry(player);

    private static void Player_orig_Added_il(ILContext il) {
        var cursor = new ILCursor(il);

        cursor.GotoNext(MoveType.Before,
            instr => instr.OpCode == OpCodes.Ldarg_0,
            instr => instr.MatchLdfld<Player>("StateMachine"),
            instr => instr.MatchLdcI4(14),
            instr => instr.MatchCallvirt<StateMachine>("set_State"));

        int index = cursor.Index;
        ILLabel label = null;

        cursor.GotoNext(instr => instr.MatchBr(out label));

        cursor.Index = index;
        cursor.MoveAfterLabels();

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Call, typeof(PlayerExtensions).GetMethodUnconstrained(nameof(TryInterceptRespawn)));
        cursor.Emit(OpCodes.Brtrue, label);
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

        extData.RedBoostStoredSpeedTimer -= Engine.DeltaTime;
        
        if (extData.RedBoostStoredSpeedTimer < 0f)
            extData.RedBoostStoredSpeedTimer = 0f;

        extData.WhiteDashSuperGraceTimer -= Engine.DeltaTime;

        if (extData.WhiteDashSuperGraceTimer < 0f)
            extData.WhiteDashSuperGraceTimer = 0f;

        update(player);

        var level = dynamicData.Get<Level>("level");

        if (extData.Surfing && dynamicData.Get<bool>("onGround") && Math.Abs(player.Speed.X) > 64f) {
            float interval = 0.008f / Math.Min(Math.Abs(player.Speed.X) / GROUND_BOOST_SPEED, 1f);
            var offset = new Vector2(-4f * Math.Sign(player.Speed.X), -2f);
            float angle = -MathHelper.PiOver2 - 0.4f * Math.Sign(player.Speed.X);

            foreach (var position in Util.TemporalLerp(ref extData.SurfParticleTimer, interval, player.PreviousPosition, player.Position, Engine.DeltaTime))
                level.ParticlesBG.Emit(SURF_PARTICLE, 1, position + offset, 2f * Vector2.One, angle);
        }
        else
            extData.SurfParticleTimer = 0f;
        
        if (extData.RedBoostTimer > 0f)
            player.UpdateTrail(Color.Red * Math.Min(4f * extData.RedBoostTimer, 1f), 0.016f, 0.16f);
        else if (extData.RedBoostSoundSource.Playing)
            extData.RedBoostSoundSource.Stop();
        
        if (extData.RedBoostTimer > 0f && player.Speed.Length() > 64f) {
            float interval = 0.008f / Math.Min(Math.Abs(player.Speed.Length()) / RED_BOOST_DASH_SPEED.X, 1f);
            var offset = -8f * Vector2.UnitY;
            
            foreach (var position in Util.TemporalLerp(ref extData.RedBoostParticleTimer, interval, player.PreviousPosition, player.Position, Engine.DeltaTime))
                level.ParticlesBG.Emit(RED_BOOST_PARTICLE, 1, position + offset, 6f * Vector2.One);
        }
        else
            extData.RedBoostParticleTimer = 0f;
        
        if (extData.RedBoostStoredSpeedTimer == 0f)
            extData.RedBoostStoredSpeed = 0f;

        if (extData.SurfSoundSource.Playing)
            extData.SurfSoundSource.Param("fade", MathHelper.Min(Math.Abs(player.Speed.X) / GROUND_BOOST_SPEED, 1f));
    }
    
    private static void Player_orig_Update_il(ILContext il) {
        var cursor = new ILCursor(il);

        cursor.GotoNext(MoveType.AfterLabel,
            instr => instr.OpCode == OpCodes.Ldarg_0,
            instr => instr.MatchCall<Actor>("Update"));

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Call, typeof(PlayerExtensions).GetMethodUnconstrained(nameof(BeforeBaseUpdate)));
    }

    private static void Player_OnCollideH(On.Celeste.Player.orig_OnCollideH onCollideH, Player player, CollisionData data) {
        player.GetData(out _, out var extData);
        
        if ((extData.RedBoostTimer > 0f || player.StateMachine.State == extData.BlueDashIndex) && data.Hit is DashBlock dashBlock)
            dashBlock.Break(player.Center, data.Direction, true, true);
        else
            onCollideH(player, data);
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
        cursor.GotoPrev(MoveType.AfterLabel,
            instr => instr.MatchLdcR4(0f),
            instr => instr.MatchStfld<Vector2>("X"));

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Call, typeof(PlayerExtensions).GetMethodUnconstrained(nameof(OnTrueCollideH)));
    }

    private static void Player_OnCollideV(On.Celeste.Player.orig_OnCollideV onCollideV, Player player, CollisionData data) {
        player.GetData(out _, out var extData);
        
        if ((extData.RedBoostTimer > 0f || player.StateMachine.State == extData.GreenDiveIndex) && data.Hit is DashBlock dashBlock)
            dashBlock.Break(player.Center, data.Direction, true, true);
        else
            onCollideV(player, data);
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
        cursor.GotoPrev(MoveType.Before,
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
        player.GetData(out _, out var extData);
        
        if (extData.Surfing)
            Util.PlaySound(SFX.char_mad_water_out, 2f);
            
        jump(player, particles, playsfx);
    }

    private static void Player_WallJump(On.Celeste.Player.orig_WallJump wallJump, Player player, int dir) {
        player.GetData(out var dynamicData, out var extData);
        
        if (extData.RedBoostTimer == 0f && extData.RedBoostStoredSpeedTimer == 0f) {
            wallJump(player, dir);
            
            return;
        }
        
        float beforeSpeedX = Math.Abs(player.Speed.X);
        float beforeSpeedY = player.Speed.Y;
        
        wallJump(player, dir);

        if (Math.Sign(Input.MoveX.Value) == dir) {
            float newSpeed = Math.Abs(player.Speed.X);

            if (extData.RedBoostTimer > 0f && beforeSpeedX > newSpeed)
                newSpeed = beforeSpeedX;

            if (extData.RedBoostStoredSpeedTimer > 0f && dir * extData.RedBoostStoredSpeed > newSpeed)
                newSpeed = dir * extData.RedBoostStoredSpeed;

            player.Speed.X = dir * newSpeed;
        }

        if (player.Speed.Y > beforeSpeedY)
            player.Speed.Y = beforeSpeedY;

        dynamicData.Set("varJumpTimer", 0f);
    }

    private static int Player_NormalUpdate(On.Celeste.Player.orig_NormalUpdate normalUpdate, Player player) {
        player.GetData(out var dynamicData, out var extData);
        
        int moveX = Input.MoveX.Value;
        
        if ((extData.RedBoostTimer > 0f || extData.Surfing) && dynamicData.Get<bool>("onGround") && !player.Ducking && moveX * player.Speed.X < GROUND_BOOST_SPEED)
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
        cursor.Emit(OpCodes.Call, typeof(PlayerExtensions).GetMethodUnconstrained(nameof(TryPopCard)));
        
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
        
        cursor.GotoNext(
            instr => instr.OpCode == OpCodes.Ldarg_0,
            instr => instr.MatchLdfld<Player>("jumpGraceTimer"));
        
        int index = cursor.Index;
        
        cursor.GotoNext(instr => instr.MatchBr(out label));
        cursor.Index = index;

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Call, typeof(PlayerExtensions).GetMethodUnconstrained(nameof(TryDoCustomJump)));
        cursor.Emit(OpCodes.Brtrue_S, label);
    }

    private static void Player_DashBegin(On.Celeste.Player.orig_DashBegin dashBegin, Player player) {
        foreach (var entity in player.CollideAll<Demon>())
            ((Demon) entity).OnPlayer(player);

        dashBegin(player);
        player.GetData(out _, out var extData);
        extData.BlueDashHyperGraceTimer = 0f;
        extData.WhiteDashSuperGraceTimer = 0f;
    }
    
    private static void Player_IntroRespawnEnd(On.Celeste.Player.orig_IntroRespawnEnd introRespawnEnd, Player player) {
        introRespawnEnd(player);
        player.Scene.Tracker.GetEntity<RushLevelController>()?.RespawnCompleted();
    }

    private static void Player_UpdateSprite(On.Celeste.Player.orig_UpdateSprite updateSprite, Player player) {
        if (!player.IsInCustomDash()) {
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

    private class Data {
        public int BlueDashIndex;
        public int GreenDiveIndex;
        public int RedBoostDashIndex;
        public int WhiteDashIndex;
        public CardInventory CardInventory = new();
        public CardInventoryIndicator CardInventoryIndicator;
        public float UseCardCooldown;
        public bool KilledInBlueDash;
        public float BlueDashHyperGraceTimer;
        public float RedBoostTimer;
        public float RedBoostStoredSpeed;
        public float RedBoostStoredSpeedTimer;
        public float RedBoostParticleTimer;
        public SoundSource RedBoostSoundSource;
        public float WhiteDashSuperGraceTimer;
        public SoundSource WhiteDashSoundSource;
        public float CustomTrailTimer;
        public bool Surfing;
        public float SurfParticleTimer;
        public SoundSource SurfSoundSource;
    }
}