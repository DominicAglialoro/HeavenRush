using System;
using System.Collections;
using Monocle;
using MonoMod.Utils;

namespace Celeste.Mod.HeavenRush; 

public static class StateMachineExtensions {
    public static int AddState(this StateMachine stateMachine, Func<int> onUpdate = null, Func<IEnumerator> coroutine = null, Action begin = null, Action end = null) {
        var dynamicData = DynamicData.For(stateMachine);
        var updates = dynamicData.Get<Func<int>[]>("updates");
        var coroutines = dynamicData.Get<Func<IEnumerator>[]>("coroutines");
        var begins = dynamicData.Get<Action[]>("begins");
        var ends = dynamicData.Get<Action[]>("ends");
        int nextIndex = begins.Length;
        
        Array.Resize(ref updates, begins.Length + 1);
        Array.Resize(ref coroutines, coroutines.Length + 1);
        Array.Resize(ref begins, begins.Length + 1);
        Array.Resize(ref ends, begins.Length + 1);

        dynamicData.Set("updates", updates);
        dynamicData.Set("coroutines", coroutines);
        dynamicData.Set("begins", begins);
        dynamicData.Set("ends", ends);
        stateMachine.SetCallbacks(nextIndex, onUpdate, coroutine, begin, end);
        
        return nextIndex;
    }
}