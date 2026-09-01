using System;
using System.Linq;
using System.Threading.Tasks;
using RimTalk.Data;
using RimTalk.Narrative;
using RimTalk.Prose;
using RimTalk.Util;
using Verse;

namespace RimTalk.Service;

/// <summary>
/// Generates expanded personalities from backstory. rim-universe #51.
///
/// Runs before ArrivalService and GoalService: the personality feeds topics,
/// which feed profiles, which feed goals. Order matters.
/// </summary>
public static class PersonalityExpansionService
{
    static bool _generating;
    static DateTime? _generatingSince;
    const int StuckAfterSeconds = 120; // 2 minutes is generous for a single API call
    static readonly AttemptBudget Budget = new();

    public static void Clear()
    {
        Budget.Clear();
        _generating = false;
        _generatingSince = null;
    }

    /// <summary>Whether this pawn needs a personality expansion.</summary>
    static bool NeedsPersonality(Pawn pawn) =>
        pawn != null &&
        !PersonalityStore.Has(pawn) &&
        !Budget.Exhausted(pawn.thingIDNumber);

    /// <summary>
    /// A pawn who needs personality expansion. Inherits ArrivalService's InOrbit
    /// check — only colonists, prisoners, and slaves get the expensive treatment.
    /// </summary>
    public static Pawn NextNeeding() =>
        _generating || AIService.IsBusy() || Find.World == null
            ? null
            : Cache.Keys.FirstOrDefault(p => ArrivalService.InOrbit(p) && NeedsPersonality(p));

    public static void TryGenerate()
    {
        Logger.Message("Personality: TryGenerate called");

        // Check for stuck state before checking _generating
        if (_generating && _generatingSince != null &&
            (DateTime.Now - _generatingSince.Value).TotalSeconds >= StuckAfterSeconds)
        {
            Logger.Warning($"Personality generation stuck for {StuckAfterSeconds}s, releasing slot");
            _generating = false;
            _generatingSince = null;
        }

        if (_generating)
        {
            Logger.Message("Personality: skipped (already generating)");
            return;
        }
        if (AIService.IsBusy())
        {
            Logger.Message("Personality: skipped (AI busy)");
            return;
        }
        if (Find.World == null)
        {
            Logger.Message("Personality: skipped (no world)");
            return;
        }

        // Check each pawn
        var candidates = Cache.Keys.Where(p => ArrivalService.InOrbit(p)).ToList();
        Logger.Message($"Personality: {candidates.Count} pawns in orbit");

        foreach (var p in candidates.Take(3))
        {
            var has = PersonalityStore.Has(p);
            var exhausted = Budget.Exhausted(p.thingIDNumber);
            Logger.Message($"Personality: {p.LabelShort} has={has} exhausted={exhausted}");
        }

        var pawn = candidates.FirstOrDefault(NeedsPersonality);
        if (pawn == null)
        {
            Logger.Message("Personality: no pawn needs generation");
            return;
        }

        Logger.Message($"Personality: starting generation for {pawn.LabelShort}");
        _generating = true;
        _generatingSince = DateTime.Now;
        _ = GenerateFor(pawn).ContinueWith(_ =>
        {
            _generating = false;
            _generatingSince = null;
        });
    }

    static async Task GenerateFor(Pawn pawn)
    {
        try
        {
            var prompt = PersonalityText.Prompt(pawn);
            if (string.IsNullOrWhiteSpace(prompt))
            {
                Logger.Message($"Personality: prompt was empty for {pawn.LabelShort}");
                return;
            }

            Logger.Message($"Personality: querying API for {pawn.LabelShort}");
            var request = new TalkRequest(prompt, pawn)
            {
                Context = "You are a character backstory generator. Return ONLY valid JSON matching the requested schema. No prose, no markdown, no explanation — just the JSON object."
            };
            var data = await AIService.Query<PersonalityExpansionData>(request);
            Logger.Message($"Personality: got response for {pawn.LabelShort}, narrative={data?.Narrative?.Length ?? 0} chars");

            var personality = PersonalityText.Accept(pawn, data);
            if (personality == null)
            {
                Logger.Message($"Personality for {pawn.LabelShort} was refused (empty, too short, " +
                               "or malformed). " +
                               AttemptBudget.Verdict(Budget.Failed(pawn.thingIDNumber)));
                return;
            }

            Budget.Succeeded(pawn.thingIDNumber);
            PersonalityStore.Record(personality);
            Logger.Message($"Personality: {pawn.LabelShort} — {personality.SpeechStyle}, " +
                         $"loves {personality.LovedFood}, hates {personality.HatedAnimal}");
        }
        catch (Exception e)
        {
            Logger.Error($"Personality generation failed: {e.Message}");
        }
    }
}
