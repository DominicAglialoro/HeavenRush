using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.HeavenRush; 

public struct ParticleBurst {
    public ParticleType ParticleType;
    public int Amount = 1;
    public Vector2 Offset = Vector2.Zero;
    public Vector2 Range = Vector2.Zero;

    public ParticleBurst(ParticleType particleType) => ParticleType = particleType;
}