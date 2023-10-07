using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace Celeste.Mod.HeavenRush; 

public class DynamicWaterSurface {
    public Vector2 Position { get; }
    
    public int Width { get; }
    
    public int Height { get; }

    private float acceleration;
    private float diffusion;
    private float damping;
    private SurfacePoint[] surface;
    private SurfacePoint[] oldSurface;
    private VertexPositionColor[] fillMesh;
    private VertexPositionColor[] surfaceMesh;
    private int leftmostActive;
    private int rightmostActive;

    public DynamicWaterSurface(Vector2 position, int width, int height, float acceleration, float diffusion, float damping) {
        Position = position;
        Width = width;
        Height = height;
        this.acceleration = acceleration;
        this.diffusion = diffusion;
        this.damping = damping;

        surface = new SurfacePoint[width / 4 + 1];
        oldSurface = new SurfacePoint[surface.Length];
        fillMesh = new VertexPositionColor[(surface.Length - 1) * 6];
        surfaceMesh = new VertexPositionColor[(surface.Length - 1) * 6];

        for (int i = 0; i < fillMesh.Length; i++)
            fillMesh[i].Color = Water.FillColor;
        
        for (int i = 0; i < surfaceMesh.Length; i++)
            surfaceMesh[i].Color = Water.SurfaceColor;

        for (int quad = 0, x = 0; quad < fillMesh.Length; quad += 6, x += 4) {
            fillMesh.SetQuad(quad,
                new Vector3(position + new Vector2(x, 0f), 0f),
                new Vector3(position + new Vector2(x, height), 0f),
                new Vector3(position + new Vector2(x + 4f, 0f), 0f),
                new Vector3(position + new Vector2(x + 4f, height), 0f));
        }
        
        for (int quad = 0, x = 0; quad < surfaceMesh.Length; quad += 6, x += 4) {
            surfaceMesh.SetQuad(quad,
                new Vector3(position + new Vector2(x, -1f), 0f),
                new Vector3(position + new Vector2(x, 0f), 0f),
                new Vector3(position + new Vector2(x + 4f, -1f), 0f),
                new Vector3(position + new Vector2(x + 4f, 0f), 0f));
        }

        leftmostActive = surface.Length;
        rightmostActive = -1;
    }

    public void Update(int cameraLeft, int cameraRight, float deltaTime) {
        var temp = oldSurface;

        oldSurface = surface;
        surface = temp;

        while (leftmostActive < surface.Length) {
            var point = surface[leftmostActive];
            
            if (point.Position != 0f || point.Velocity != 0f)
                break;

            leftmostActive++;
        }

        if (leftmostActive > 0)
            leftmostActive--;
        
        while (rightmostActive >= 0) {
            var point = surface[rightmostActive];
            
            if (point.Position != 0f || point.Velocity != 0f)
                break;

            rightmostActive--;
        }

        if (rightmostActive < surface.Length - 1)
            rightmostActive++;

        for (int i = leftmostActive; i <= rightmostActive; i++) {
            var left = i > 0 ? oldSurface[i - 1] : SurfacePoint.Zero;
            var right = i < surface.Length - 1 ? oldSurface[i + 1] : SurfacePoint.Zero;
            
            surface[i] = oldSurface[i].Update(left, right, deltaTime, acceleration, diffusion, damping);
        }

        int startIndex = Math.Max(0, (cameraLeft - (int) Position.X) / 4);
        int endIndex = Math.Min((cameraRight - (int) Position.X) / 4 + 1, surface.Length - 1);

        for (int i = startIndex, quad = 6 * startIndex, x = 4 * startIndex; i < endIndex; i++, quad += 6, x += 4) {
            float startOffset = surface[i].Position;
            float endOffset = surface[i + 1].Position;
            
            fillMesh.SetQuad(quad,
                new Vector3(Position + new Vector2(x, startOffset), 0f),
                new Vector3(Position + new Vector2(x, Height), 0f),
                new Vector3(Position + new Vector2(x + 4f, endOffset), 0f),
                new Vector3(Position + new Vector2(x + 4f, Height), 0f));
            
            surfaceMesh.SetQuad(quad,
                new Vector3(Position + new Vector2(x, startOffset - 1f), 0f),
                new Vector3(Position + new Vector2(x, startOffset), 0f),
                new Vector3(Position + new Vector2(x + 4f, endOffset - 1f), 0f),
                new Vector3(Position + new Vector2(x + 4f, endOffset), 0f));
        }
    }

    public void Render(Camera camera) {
        GFX.DrawVertices(camera.Matrix, fillMesh, fillMesh.Length);
        GFX.DrawVertices(camera.Matrix, surfaceMesh, surfaceMesh.Length);
    }

    public void ApplyForce(float x, float amount, int radius) {
        int index = Math.Max(0, Math.Min((int) Math.Round(x - Position.X) / 4, surface.Length - 1));
        int minIndex = Math.Max(0, index - radius);
        int maxIndex = Math.Min(index + radius, surface.Length - 1);

        amount /= 2 * radius + 1;

        for (int i = minIndex; i <= maxIndex; i++)
            surface[index] = new SurfacePoint(surface[index].Position, surface[index].Velocity + amount);

        if (minIndex < leftmostActive)
            leftmostActive = minIndex;

        if (maxIndex > rightmostActive)
            rightmostActive = maxIndex;
    }
    
    private struct SurfacePoint {
        public static readonly SurfacePoint Zero = new(0f, 0f);
        
        public readonly float Position;
        public readonly float Velocity;

        public SurfacePoint(float position, float velocity) {
            Position = position;
            Velocity = velocity;
        }

        public SurfacePoint Update(SurfacePoint left, SurfacePoint right, float deltaTime, float acceleration, float diffusion, float damping) {
            float newVelocity = Velocity - Position * acceleration * deltaTime;

            newVelocity += (0.5f * (left.Position + right.Position) - Position) * diffusion * deltaTime;
            newVelocity *= 1f - Math.Min(damping * deltaTime, 1f);

            float newPosition = Position + newVelocity * deltaTime;

            if (Math.Abs(newPosition) < 0.01f && Math.Abs(newVelocity) < 0.01f)
                return Zero;

            return new SurfacePoint(MathHelper.Clamp(newPosition, -2f, 2f), newVelocity);
        }
    }
}