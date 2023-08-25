using System.Collections.Generic;

namespace Celeste.Mod.HeavenRush; 

public class HeavenRushSaveData : EverestModuleSaveData {
    public Dictionary<string, long> BestTimes { get; set; } = new();
}