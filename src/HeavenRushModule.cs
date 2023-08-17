using System;

namespace Celeste.Mod.HeavenRush;

public class HeavenRushModule : EverestModule {
    public static HeavenRushModule Instance { get; private set; }

    public override Type SettingsType => typeof(HeavenRushSettings);
    public static HeavenRushSettings Settings => (HeavenRushSettings) Instance._Settings;

    public override Type SessionType => typeof(HeavenRushSession);
    public static HeavenRushSession Session => (HeavenRushSession) Instance._Session;

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

    public override void Load() {
        PlayerExtensions.Load();
        InputExtensions.Load();
        LevelExtensions.Load();
    }

    public override void Initialize() { }

    public override void Unload() {
        PlayerExtensions.Unload();
        InputExtensions.Unload();
        LevelExtensions.Unload();
    }
}