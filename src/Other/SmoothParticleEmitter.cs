using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.HeavenRush; 

public class SmoothParticleEmitter : Component {
    public ParticleSystem System;
    public ParticleType Type;
    public Vector2 Position;
    public Vector2 Range;
    public float Interval;
    public float? Direction;

    private bool startedLastFrame = true;
    private float timer;
    private Vector2 previousPosition;

    public SmoothParticleEmitter(ParticleSystem system, ParticleType type, Vector2 position, Vector2 range, float interval) : base(true, false) {
        System = system;
        Type = type;
        Position = position;
        Range = range;
        Interval = interval;
    }

    public override void Added(Entity entity) {
        base.Added(entity);
        previousPosition = entity.Position + Position;
    }

    public override void Update() {
        if (startedLastFrame) {
            startedLastFrame = false;
            
            return;
        }
        
        var worldPosition = Entity.Position + Position;
        float deltaTime = Engine.DeltaTime;
        float emitDirection = Direction ?? Type.Direction;

        timer = timer % Interval + deltaTime;

        while (timer >= Interval) {
            timer -= Interval;
            System.Emit(Type, 1, Vector2.Lerp(worldPosition, previousPosition, timer / deltaTime), Range, emitDirection);
        }

        previousPosition = worldPosition;
    }

    public void Start() {
        if (Active)
            return;
        
        Active = true;
        startedLastFrame = true;
        timer = 0f;
        previousPosition = Entity.Position + Position;
    }

    public void Stop() => Active = false;
}