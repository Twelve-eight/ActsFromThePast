using BaseLib.Config;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;

namespace ActsFromThePast;

public class ActsFromThePastConfig : SimpleModConfig
{
    [ConfigHoverTip]
    public static bool RebalancedMode { get; set; } = false;

    // MP-safe accessor (fork fix 2026-08-27): RebalancedMode is a LOCAL config; when the two
    // peers disagree, the same option index resolves to different actions and the run desyncs
    // (evidence: divergence #55/#35 — host INITIAL_REBALANCED vs remote INITIAL). Multiplayer
    // runs always use the vanilla branch so both peers agree regardless of local settings.
    public static bool RebalancedModeEffective =>
            RunManager.Instance is { } rm && rm.NetService.Type == NetGameType.Singleplayer && RebalancedMode;
    
    [ConfigHoverTip]
    public static bool AllowNonLegacySharedEventsInLegacyActs { get; set; } = true;

    [ConfigHoverTip]
    public static bool AllowLegacySharedEventsInNonLegacyActs { get; set; } = false;
    
    [ConfigHoverTip]
    public static bool DarvOnlyInLegacyActs { get; set; } = false;
    
    [ConfigHoverTip]
    public static bool LegacyEnemiesGiveClassicSlimed { get; set; } = false;
}