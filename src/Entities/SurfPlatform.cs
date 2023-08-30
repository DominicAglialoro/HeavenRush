using System;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace Celeste.Mod.HeavenRush; 

[CustomEntity("heavenRush/surfPlatform"), Tracked]
public class SurfPlatform : Solid {
    private const float SURFACE_OFFSET = -1f;
    private const float FORCE = 1.5f;
    
    private SurfacePoint[] surface;
    private SurfacePoint[] oldSurface;
    private VertexPositionColor[] fillMesh;
    private VertexPositionColor[] surfaceMesh;
    private Level level;

    public SurfPlatform(EntityData data, Vector2 offset) : base(data.Position + offset, data.Width, data.Height, true) {
        surface = new SurfacePoint[(int) Width / 4 + 1];
        oldSurface = new SurfacePoint[surface.Length];
        fillMesh = new VertexPositionColor[(surface.Length - 1) * 6];
        surfaceMesh = new VertexPositionColor[(surface.Length - 1) * 6];

        for (int i = 0; i < fillMesh.Length; i++)
            fillMesh[i].Color = Water.FillColor;
        
        for (int i = 0; i < surfaceMesh.Length; i++)
            surfaceMesh[i].Color = Water.SurfaceColor;

        for (int quad = 0, x = 0; quad < fillMesh.Length; quad += 6, x += 4) {
            fillMesh[quad].Position = new Vector3(Position + new Vector2(x, SURFACE_OFFSET), 0f);
            fillMesh[quad + 1].Position = new Vector3(Position + new Vector2(x + 4f, SURFACE_OFFSET), 0f);
            fillMesh[quad + 2].Position = new Vector3(Position + new Vector2(x, Height), 0f);
            fillMesh[quad + 3].Position = new Vector3(Position + new Vector2(x, Height), 0f);
            fillMesh[quad + 4].Position = new Vector3(Position + new Vector2(x + 4f, Height), 0f);
            fillMesh[quad + 5].Position = new Vector3(Position + new Vector2(x + 4f, SURFACE_OFFSET), 0f);
        }
        
        for (int quad = 0, x = 0; quad < fillMesh.Length; quad += 6, x += 4) {
            surfaceMesh[quad].Position = new Vector3(Position + new Vector2(x, SURFACE_OFFSET - 1f), 0f);
            surfaceMesh[quad + 1].Position = new Vector3(Position + new Vector2(x + 4f, SURFACE_OFFSET - 1f), 0f);
            surfaceMesh[quad + 2].Position = new Vector3(Position + new Vector2(x, SURFACE_OFFSET), 0f);
            surfaceMesh[quad + 3].Position = new Vector3(Position + new Vector2(x, SURFACE_OFFSET), 0f);
            surfaceMesh[quad + 4].Position = new Vector3(Position + new Vector2(x + 4f, SURFACE_OFFSET), 0f);
            surfaceMesh[quad + 5].Position = new Vector3(Position + new Vector2(x + 4f, SURFACE_OFFSET - 1f), 0f);
        }

        SurfaceSoundIndex = 0;
    }

    public override void Added(Scene scene) {
        base.Added(scene);
        level = scene as Level;
    }

    public override void Update() {
        base.Update();

        var player = GetPlayerOnTop();

        if (player != null) {
            int index = Math.Max(0, Math.Min((int) Math.Round(0.25f * (player.Position.X - X)), surface.Length - 1));
            int minIndex = Math.Max(0, index - 2);
            int maxIndex = Math.Min(index + 2, surface.Length - 1);
            float force = FORCE * Math.Abs(player.Speed.X) * Engine.DeltaTime;

            for (int i = minIndex; i <= maxIndex; i++)
                surface[index] = new SurfacePoint(surface[index].Position, surface[index].Velocity - force);
        }

        var temp = oldSurface;

        oldSurface = surface;
        surface = temp;
        
        float deltaTime = Engine.DeltaTime;

        if (surface.Length > 1) {
            surface[0] = oldSurface[0].Update(SurfacePoint.Zero, oldSurface[1], deltaTime);

            for (int i = 1; i < surface.Length - 1; i++)
                surface[i] = oldSurface[i].Update(oldSurface[i - 1], oldSurface[i + 1], deltaTime);

            surface[surface.Length - 1] = oldSurface[oldSurface.Length - 1]
                .Update(oldSurface[oldSurface.Length - 2], SurfacePoint.Zero, deltaTime);
        }
        else
            surface[0] = oldSurface[0].Update(SurfacePoint.Zero, SurfacePoint.Zero, deltaTime);

        int cameraLeft = (int) level.Camera.Position.X;
        int cameraRight = cameraLeft + 320;
        int startIndex = Math.Max(0, (cameraLeft - (int) X) / 4);
        int endIndex = Math.Min((cameraRight - (int) X) / 4 + 1, surface.Length - 1);

        for (int i = startIndex, quad = 6 * startIndex, x = 4 * startIndex; i < endIndex; i++, quad += 6, x += 4) {
            float startOffset = surface[i].Position + SURFACE_OFFSET;
            float endOffset = surface[i + 1].Position + SURFACE_OFFSET;
            
            fillMesh[quad].Position = new Vector3(Position + new Vector2(x, startOffset), 0f);
            fillMesh[quad + 1].Position = new Vector3(Position + new Vector2(x + 4f, endOffset), 0f);
            fillMesh[quad + 5].Position = new Vector3(Position + new Vector2(x + 4f, endOffset), 0f);
            
            surfaceMesh[quad].Position = new Vector3(Position + new Vector2(x, startOffset - 1f), 0f);
            surfaceMesh[quad + 1].Position = new Vector3(Position + new Vector2(x + 4f, endOffset - 1f), 0f);
            surfaceMesh[quad + 2].Position = new Vector3(Position + new Vector2(x, startOffset), 0f);
            surfaceMesh[quad + 3].Position = new Vector3(Position + new Vector2(x, startOffset), 0f);
            surfaceMesh[quad + 4].Position = new Vector3(Position + new Vector2(x + 4f, endOffset), 0f);
            surfaceMesh[quad + 5].Position = new Vector3(Position + new Vector2(x + 4f, endOffset - 1f), 0f);
        }
    }

    public override void Render() {
        GameplayRenderer.End();
        GFX.DrawVertices(level.Camera.Matrix, fillMesh, fillMesh.Length);
        GFX.DrawVertices(level.Camera.Matrix, surfaceMesh, surfaceMesh.Length);
        GameplayRenderer.Begin();
        base.Render();
    }

    private struct SurfacePoint {
        private const float ACCELERATION = 360f;
        private const float DIFFUSION = 1300f;
        private const float DAMPENING = 1.75f;
        
        public static readonly SurfacePoint Zero = new(0f, 0f);
        
        public readonly float Position;
        public readonly float Velocity;

        public SurfacePoint(float position, float velocity) {
            Position = position;
            Velocity = velocity;
        }

        public SurfacePoint Update(SurfacePoint left, SurfacePoint right, float deltaTime) {
            float newVelocity = Velocity - Position * ACCELERATION * deltaTime;

            newVelocity += (0.5f * (left.Position + right.Position) - Position) * DIFFUSION * deltaTime;
            newVelocity *= 1f - Math.Min(DAMPENING * deltaTime, 1f);

            return new SurfacePoint(MathHelper.Clamp(Position + newVelocity * deltaTime, -2f, 2f), newVelocity);
        }
    }
}