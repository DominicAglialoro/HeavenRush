using Microsoft.Xna.Framework;

namespace Celeste.Mod.HeavenRush; 

public static class Util {
    public static Vector2 PreserveArea(Vector2 vec, float area = 1f) => area / (vec.X * vec.Y) * vec;
}