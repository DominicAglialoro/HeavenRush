using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FMOD.Studio;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.HeavenRush; 

public static class Util {
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

    public static IEnumerable<Vector2> TemporalLerp(ref float timer, float interval, Vector2 fromPosition, Vector2 toPosition, float deltaTime) {
        timer = timer % interval + deltaTime;

        if (timer < interval)
            return new Vector2[0];

        var enumerable = Enumerate(timer, interval, fromPosition, toPosition, deltaTime);

        timer %= interval;

        return enumerable;
        
        IEnumerable<Vector2> Enumerate(float timer, float interval, Vector2 fromPosition, Vector2 toPosition, float deltaTime) {
            while (timer >= interval) {
                yield return Vector2.Lerp(toPosition, fromPosition, timer / deltaTime);

                timer -= interval;
            }
        }
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

    public static MethodInfo GetMethodUnconstrained(this Type type, string name) => type.GetMethod(name,
        BindingFlags.Instance |
        BindingFlags.Static |
        BindingFlags.Public |
        BindingFlags.NonPublic);
    
    public static PropertyInfo GetPropertyUnconstrained(this Type type, string name) => type.GetProperty(name,
        BindingFlags.Instance |
        BindingFlags.Static |
        BindingFlags.Public |
        BindingFlags.NonPublic);
}