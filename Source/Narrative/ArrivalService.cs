using System;
using System.Linq;
using System.Threading.Tasks;
using RimTalk.Data;
using RimTalk.Prose;
using RimTalk.Service;
using RimTalk.Util;
using Verse;

namespace RimTalk.Narrative;

/// <summary>
/// Writes the arrival log entry, once, for a pawn who has just entered the colony's
/// orbit. rim-universe #37.
///
/// Triggered on join rather than on spawn, and the difference matters: a 30-raider
/// assault would otherwise be 30 calls for pawns who mostly die without speaking,
/// against a system already gated behind a single global in-flight request.
/// </summary>
public static class ArrivalService
{
    static bool _generating;

    /// <summary>
    /// Whether this pawn is close enough to the colony to be worth a generation.
    /// Colonists, prisoners and slaves; not raiders, visitors or traders, whose voice
    /// is generated lazily on first speech instead.
    /// </summary>
    public static bool InOrbit(Pawn pawn)
    {
        if (pawn == null || pawn.Dead || !pawn.Spawned) return false;
        if (pawn.RaceProps is not { Humanlike: true }) return false;
        return pawn.IsFreeNonSlaveColonist
            || (pawn.IsPrisonerOfColony && pawn.guest?.HostFaction?.IsPlayer == true)
            || pawn.IsSlaveOfColony;
    }

    /// <summary>
    /// One candidate, or null. Called from the cache refresh, which already runs on the
    /// main thread every few seconds and already walks every eligible pawn.
    /// </summary>
    public static Pawn NextNeeding() =>
        _generating || AIService.IsBusy() || Find.World == null
            ? null
            : Cache.Keys.FirstOrDefault(p => InOrbit(p) && !ArrivalLog.Has(p));

    public static void TryGenerate()
    {
        var pawn = NextNeeding();
        if (pawn == null) return;

        _generating = true;
        _ = GenerateFor(pawn).ContinueWith(_ => _generating = false);
    }

    static async Task GenerateFor(Pawn pawn)
    {
        try
        {
            var profile = PromptService.CreatePawnContext(pawn);
            var place = Place(pawn);

            var request = new TalkRequest(ArrivalText.Prompt(profile, place), pawn);
            var data = await AIService.Query<ArrivalData>(request);

            var accepted = ArrivalText.Accept(data?.Log);
            if (accepted == null)
            {
                // Refused, not repaired. A colony gets one arrival entry per person and
                // everything downstream treats it as canon, so a false one is worse than
                // none. The pawn stays on the list and the next refresh tries again.
                Logger.Message($"Arrival log for {pawn.LabelShort} was refused (empty, over-long, " +
                               "or it decided how they got here). Will retry.");
                return;
            }

            ArrivalLog.Record(pawn, accepted);
        }
        catch (Exception e)
        {
            Logger.Error($"Arrival log generation failed: {e.Message}");
        }
    }

    /// <summary>Where they woke up, in the words the scene prose would use.</summary>
    static string Place(Pawn pawn)
    {
        var map = pawn?.Map;
        if (map == null) return null;

        var biome = map.Biome?.label;
        return string.IsNullOrWhiteSpace(biome)
            ? null
            : $"{ProseWords.Article(biome)} {ProseWords.Mid(biome)}";
    }
}
