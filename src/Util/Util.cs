using System;
using System.Collections;
using System.Reflection;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Celeste.Mod.HeavenRush; 

public static class Util {
    private const BindingFlags ALL_FLAGS = BindingFlags.Instance |
                                           BindingFlags.Static |
                                           BindingFlags.Public |
                                           BindingFlags.NonPublic;
    
    public static EventInstance PlaySound(string name, float volume = 1f, Vector2? position = null) {
        var instance = Audio.CreateInstance(name, position);

        if (instance == null)
            return null;

        instance.setVolume(volume);
        instance.start();
        instance.release();

        return instance;
    }
    
    public static Vector2 PreserveArea(Vector2 vec, float area = 1f) => area / (vec.X * vec.Y) * vec;

    public static void SetQuad(this VertexPositionColor[] mesh, int index, Vector3 a, Vector3 b, Vector3 c, Vector3 d) {
        mesh[index].Position = a;
        mesh[index + 1].Position = b;
        mesh[index + 2].Position = c;
        mesh[index + 3].Position = b;
        mesh[index + 4].Position = c;
        mesh[index + 5].Position = d;
    }

    public static IEnumerator NextFrame(Action action) {
        action();
        
        yield break;
    }

    public static IEnumerator AfterFrame(Action action) {
        yield return null;
        
        action();
    }

    public static IEnumerator AfterTime(float time, Action action) {
        yield return time;

        action();
    }

    public static MethodInfo GetMethodUnconstrained(this Type type, string name) => type.GetMethod(name, ALL_FLAGS);
    
    public static PropertyInfo GetPropertyUnconstrained(this Type type, string name) => type.GetProperty(name, ALL_FLAGS);
}