using System.Runtime.CompilerServices;

namespace Celeste.Mod.HeavenRush;

public static class Extension<TTarget, TData> where TTarget : class where TData : class, new() {
    private static readonly ConditionalWeakTable<TTarget, TData> MAP = new();

    public static TData Of(TTarget target) => MAP.GetOrCreateValue(target);
}