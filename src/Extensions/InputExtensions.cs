using System;
using MonoMod.RuntimeDetour;

namespace Celeste.Mod.HeavenRush; 

public static class InputExtensions {
    private static IDetour Celeste_Input_get_GrabCheck;
    
    public static void Load() => Celeste_Input_get_GrabCheck = new Hook(typeof(Input).GetPropertyUnconstrained("GrabCheck").GetGetMethod(), Input_get_GrabCheck);

    public static void Unload() => Celeste_Input_get_GrabCheck.Dispose();

    private static bool Input_get_GrabCheck(Func<bool> grabCheck) => !HeavenRushModule.Session.HeavenRushModeEnabled && grabCheck();
}