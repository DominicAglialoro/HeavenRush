namespace Celeste.Mod.HeavenRush;

public class HeavenRushModule : EverestModule {
    public static HeavenRushModule Instance { get; private set; }

    public HeavenRushModule() {
        Instance = this;
#if DEBUG
        // debug builds use verbose logging
        Logger.SetLogLevel(nameof(HeavenRushModule), LogLevel.Verbose);
#else
        // release builds use info logging to reduce spam in log files
        Logger.SetLogLevel(nameof(HeavenRushModule), LogLevel.Info);
#endif
    }

    public override void Load() { }

    public override void Unload() { }
}