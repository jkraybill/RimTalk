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
    static readonly AttemptBudget Budget = new();

    public static void Clear() => Budget.Clear();

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
        var pawn = NextNeeding();
        if (pawn == null) return;

        _generating = true;
        _ = GenerateFor(pawn).ContinueWith(_ => _generating = false);
    }

    static async Task GenerateFor(Pawn pawn)
    {
        try
        {
            var prompt = PersonalityText.Prompt(pawn);
            if (string.IsNullOrWhiteSpace(prompt)) return;

            var request = new TalkRequest(prompt, pawn);
            var data = await AIService.Query<PersonalityExpansionData>(request);

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
            Logger.Debug($"Personality: {pawn.LabelShort} — {personality.SpeechStyle}, " +
                         $"loves {personality.LovedFood}, hates {personality.HatedAnimal}");
        }
        catch (Exception e)
        {
            Logger.Error($"Personality generation failed: {e.Message}");
        }
    }
}
