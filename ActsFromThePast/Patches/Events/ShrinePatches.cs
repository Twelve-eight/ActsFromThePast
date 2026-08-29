using System.Reflection;
using ActsFromThePast.Acts;
using ActsFromThePast.Acts.TheBeyond;
using ActsFromThePast.Acts.TheCity;
using ActsFromThePast.Interfaces;
using BaseLib.Abstracts;
using BaseLib.Patches.Content;
using HarmonyLib;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Random;

namespace ActsFromThePast.Patches.Events;

public class ShrinePatches
{
    [HarmonyPatch(typeof(ActModel), nameof(ActModel.GenerateRooms))]
[HarmonyPriority(Priority.Low)]
public static class EventPoolPatch
{
    private static readonly FieldInfo RoomsField =
        typeof(ActModel).GetField("_rooms", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly Assembly ModAssembly = typeof(EventPoolPatch).Assembly;

    private static readonly HashSet<EventModel> ModSharedEvents =
        new(CustomContentDictionary.SharedCustomEvents.Where(e => e.GetType().Assembly == ModAssembly));

    private const float ShrineChance = 0.25f;

    private static bool IsLegacyAct(ActModel act) =>
        act is ExordiumAct or TheCityAct or TheBeyondAct;

    private static bool IsModSharedEvent(EventModel e) =>
        ModSharedEvents.Contains(e);

    private static bool IsBaseGameSharedEvent(EventModel e) =>
        ModelDb.AllSharedEvents.Contains(e) && !IsModSharedEvent(e);

    private static int GetActNumber(ActModel act) => act switch
    {
        Overgrowth or Underdocks => 1,
        Hive => 2,
        Glory => 3,
        CustomActModel custom => custom.ActNumber,
        _ => -1
    };

    public static void Postfix(ActModel __instance, Rng rng)
    {
        var rooms = RoomsField?.GetValue(__instance) as RoomSet;
        if (rooms == null) return;

        // --- Phase 1: Filter ---

        if (IsLegacyAct(__instance) && !ActsFromThePastConfig.AllowNonLegacySharedEventsInLegacyActsEffective)
            rooms.events.RemoveAll(e => IsBaseGameSharedEvent(e));

        if (!IsLegacyAct(__instance) && !ActsFromThePastConfig.AllowLegacySharedEventsInNonLegacyActsEffective)
            rooms.events.RemoveAll(e => IsModSharedEvent(e));

        int actNumber = GetActNumber(__instance);
        if (actNumber >= 0)
        {
            rooms.events.RemoveAll(e =>
                e is IActRestricted restricted &&
                !restricted.AllowedActIndices.Contains(actNumber));
        }

        // --- Phase 2: Interleave shrines ---

        var shrines = rooms.events.Where(e => e is IShrineEvent).ToList();
        if (shrines.Count == 0) return;

        var regular = rooms.events.Where(e => e is not IShrineEvent).ToList();

        var result = new List<EventModel>();
        int shrineIdx = 0;
        int regularIdx = 0;
        int totalEvents = shrines.Count + regular.Count;

        for (int i = 0; i < totalEvents; i++)
        {
            bool tryShrine = rng.NextFloat(1f) < ShrineChance;

            if (tryShrine && shrineIdx < shrines.Count)
                result.Add(shrines[shrineIdx++]);
            else if (regularIdx < regular.Count)
                result.Add(regular[regularIdx++]);
            else
                result.Add(shrines[shrineIdx++]);
        }

        rooms.events.Clear();
        rooms.events.AddRange(result);
    }
}

[HarmonyPatch(typeof(RoomSet), nameof(RoomSet.EnsureNextEventIsValid))]
public static class RepeatableShrineValidityPatch
{
    private static readonly FieldInfo VisitedEventIdsField =
        typeof(RunState).GetField("_visitedEventIds", BindingFlags.NonPublic | BindingFlags.Instance);

    [ThreadStatic]
    private static List<ModelId>? _temporarilyRemoved;

    public static void Prefix(RoomSet __instance, RunState runState)
    {
        _temporarilyRemoved = null;
        if (__instance.events.Count == 0) return;

        var visited = VisitedEventIdsField?.GetValue(runState) as HashSet<ModelId>;
        if (visited == null) return;

        foreach (var e in __instance.events)
        {
            if (e is IShrineEvent { IsOneTimeEvent: false } &&
                visited.Contains(e.Id))
            {
                _temporarilyRemoved ??= new();
                _temporarilyRemoved.Add(e.Id);
            }
        }

        if (_temporarilyRemoved != null)
        {
            foreach (var id in _temporarilyRemoved)
                visited.Remove(id);
        }
    }

    public static void Postfix(RunState runState)
    {
        if (_temporarilyRemoved == null) return;

        var visited = VisitedEventIdsField?.GetValue(runState) as HashSet<ModelId>;
        if (visited == null) return;

        foreach (var id in _temporarilyRemoved)
            visited.Add(id);

        _temporarilyRemoved = null;
    }
}

/*

[HarmonyPatch(typeof(ActModel), nameof(ActModel.PullNextEvent))]
public static class EventPoolDebugPatch
{
    private static readonly FieldInfo RoomsField =
        typeof(ActModel).GetField("_rooms", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo VisitedEventIdsField =
        typeof(RunState).GetField("_visitedEventIds", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly HashSet<ActModel> LoggedActs = new();

    public static void Prefix(ActModel __instance, RunState runState)
    {
        if (!LoggedActs.Add(__instance)) return;

        var rooms = RoomsField?.GetValue(__instance) as RoomSet;
        if (rooms == null) return;

        var visited = VisitedEventIdsField?.GetValue(runState) as HashSet<ModelId>;

        var actName = __instance.GetType().Name;
        Log.Info($"[EventPoolDebug] Event list for {actName} ({rooms.events.Count} events):");

        for (int i = 0; i < rooms.events.Count; i++)
        {
            var e = rooms.events[i];
            bool isShrine = e is IShrineEvent;
            bool isAllowed = e.IsAllowed((IRunState)runState);
            bool isVisited = visited?.Contains(e.Id) ?? false;
            bool isRepeatableShrine = e is IShrineEvent { IsOneTimeEvent: false };

            string status;
            if (!isAllowed)
                status = "BLOCKED (IsAllowed=false)";
            else if (isVisited && isRepeatableShrine)
                status = "ELIGIBLE (repeatable shrine, visited prev act)";
            else if (isVisited)
                status = "BLOCKED (visited)";
            else
                status = "ELIGIBLE";

            string tag = isShrine ? " [SHRINE]" : "";
            Log.Info($"  [{i}] {e.Id.Entry}{tag} - {status}");
        }
    }
}

*/

}