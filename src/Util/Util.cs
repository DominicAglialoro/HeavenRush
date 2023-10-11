using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Celeste.Mod.HeavenRush; 

public static class Util {
    public static void SetQuad(this VertexPositionColor[] mesh, int index, Vector3 a, Vector3 b, Vector3 c, Vector3 d) {
        mesh[index].Position = a;
        mesh[index + 1].Position = b;
        mesh[index + 2].Position = c;
        mesh[index + 3].Position = b;
        mesh[index + 4].Position = c;
        mesh[index + 5].Position = d;
    }
}