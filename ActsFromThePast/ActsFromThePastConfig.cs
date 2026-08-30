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

    // MP-safe accessor (fork fix 2026-08-28, family-C): DarvOnlyInLegacyActs re-rolls DARV
    // initial options with the event Rng (DarvUniqueOffersPatch). When only one peer has it
    // enabled, the two ends generate DIFFERENT option lists for the same event (evidence:
    // 2026-08-28 23:39 run — host saw ECTOPLASM/BLACK_STAR/ASTROLABE, remote saw
    // ECTOPLASM/PHILOSOPHERS_STONE/DUSTY_TOME; remote's DUSTY_TOME pick then produced
    // RewardSelectedMessages for a RewardsSet the host never created — buffered forever,
    // MoveToMapCoord stuck at 'Exiting event room EVENT.DARV', black screen).
    // MP always uses vanilla options so both peers agree regardless of local settings.
    public static bool DarvOnlyInLegacyActsEffective =>
            RunManager.Instance is { } rm && rm.NetService.Type == NetGameType.Singleplayer && DarvOnlyInLegacyActs;

    // MP-safe accessor (fork fix 2026-08-28): ClassicSlimed power replacement changes which
    // power model spawns — same class of local-config divergence as RebalancedMode.
    public static bool LegacyEnemiesGiveClassicSlimedEffective =>
            RunManager.Instance is { } rm && rm.NetService.Type == NetGameType.Singleplayer && LegacyEnemiesGiveClassicSlimed;

    // MP-safe accessor (fork fix 2026-08-29, family-D candidate; polarity fixed
    // 2026-08-30 after review): the shared-event filters in ShrinePatches run inside
    // ActModel.GenerateRooms, which executes symmetrically on BOTH peers
    // (StartNewMultiplayerRun has no host/client branch - NCharacterSelectScreen.cs
    // L726-790, both peers drive State.Rng.UpFront from the same seed). A filter
    // gated on a raw local config therefore removes different events from each
    // peer's room pool -> different event rooms -> map divergence.
    // MP keeps the vanilla (unfiltered) concat on both peers. These are ALLOW
    // flags consumed under negation (!Effective -> RemoveAll fires), so in MP they
    // must return TRUE (allow) to suppress the filter - returning false (the plain
    // enable-flag pattern) inverted the intent and always fired the filter in MP.
    public static bool AllowNonLegacySharedEventsInLegacyActsEffective =>
            (RunManager.Instance is { } rm && rm.NetService.Type != NetGameType.Singleplayer)
            || AllowNonLegacySharedEventsInLegacyActs;

    public static bool AllowLegacySharedEventsInNonLegacyActsEffective =>
            (RunManager.Instance is { } rm && rm.NetService.Type != NetGameType.Singleplayer)
            || AllowLegacySharedEventsInNonLegacyActs;
    
    [ConfigHoverTip]
    public static bool AllowNonLegacySharedEventsInLegacyActs { get; set; } = true;

    [ConfigHoverTip]
    public static bool AllowLegacySharedEventsInNonLegacyActs { get; set; } = false;
    
    [ConfigHoverTip]
    public static bool DarvOnlyInLegacyActs { get; set; } = false;
    
    [ConfigHoverTip]
    public static bool LegacyEnemiesGiveClassicSlimed { get; set; } = false;
}