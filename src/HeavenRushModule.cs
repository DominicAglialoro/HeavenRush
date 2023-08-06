﻿using System;

namespace Celeste.Mod.HeavenRush;

public class HeavenRushModule : EverestModule {
    public static HeavenRushModule Instance { get; private set; }

    public override Type SettingsType => typeof(HeavenRushModuleSettings);
    public static HeavenRushModuleSettings Settings => (HeavenRushModuleSettings) Instance._Settings;

    public override Type SessionType => typeof(HeavenRushModuleSession);
    public static HeavenRushModuleSession Session => (HeavenRushModuleSession) Instance._Session;

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
    }

    public override void Initialize() {
        PlayerExtensions.Initialize();
    }

    public override void Unload() {
        PlayerExtensions.Unload();
    }
}