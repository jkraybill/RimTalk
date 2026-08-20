using System.Collections.Generic;
using RimTalk.Service;
using RimTalk.Util;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk.Data;

public class Hediff_Persona : Hediff
{
    private const string RimtalkHediff = "RimTalk_PersonaData";

    /// <summary>
    /// The longest suppression interval. An entry older than this can never suppress
    /// anything again, so keeping it is pure weight — and this dictionary is scribed,
    /// so the weight lands in the save file. rim-universe #4.
    /// </summary>
    public const int MaxSuppressTicks = 150000;

    private Dictionary<string, int> _spokenThoughtTicks = new();
    public string Personality;

    /// <summary>
    /// The pawn's own disposition, from their persona. A BASELINE now, not the answer:
    /// rim-universe #16, this was written once in GetOrAddNew and never again, so a
    /// prisoner recruited into the colony kept the 0.2 they were given as a captive
    /// for the rest of their life. Read <see cref="EffectiveTalkWeight"/> instead.
    /// </summary>
    public float TalkInitiationWeight = 1.0f;

    /// <summary>
    /// The baseline, moved by what is true of this pawn right now. Computed on read,
    /// so nothing has to remember to update it when a pawn is recruited, wounded,
    /// exhausted or miserable.
    /// </summary>
    public float EffectiveTalkWeight => TalkWeight.Effective(Facts());

    TalkWeightFacts Facts()
    {
        var p = pawn;
        return new TalkWeightFacts
        {
            Baseline = TalkInitiationWeight,
            Mood = p?.needs?.mood?.CurLevelPercentage,
            Rest = p?.needs?.rest?.CurLevelPercentage,
            Downed = p?.Downed ?? false,
            InMentalState = p?.InMentalState ?? false,
            Consciousness = p?.health?.capacities?.GetLevel(PawnCapacityDefOf.Consciousness),
            // Re-read every time. This is the branch that used to freeze at spawn.
            Captive = (p?.IsPrisoner ?? false) || (p?.IsSlave ?? false),
            Outsider = (p?.IsVisitor() ?? false) || (p?.IsEnemy() ?? false),
        };
    }
    public override bool Visible => false;
    
    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref Personality, "Personality");
        Scribe_Values.Look(ref TalkInitiationWeight, "TalkInitiationWeight", 1.0f);
        Scribe_Collections.Look(ref _spokenThoughtTicks, "SpokenThoughtTicks", LookMode.Value, LookMode.Value);
        
        if (_spokenThoughtTicks == null)
        {
            _spokenThoughtTicks = new Dictionary<string, int>();
        }
    }
    
    public static Hediff_Persona GetOrAddNew(Pawn pawn)
    {
        var def = DefDatabase<HediffDef>.GetNamedSilentFail(RimtalkHediff);
        if (pawn?.health?.hediffSet == null || def == null) return null;

        if (pawn.health.hediffSet.GetFirstHediffOfDef(def) is not Hediff_Persona hediff)
        {
            hediff = (Hediff_Persona)HediffMaker.MakeHediff(def, pawn);
        
            // Assign a random personality on creation
            PersonalityData randomPersonalityData =
                pawn.RaceProps.Humanlike ? Constant.Personalities.RandomElement()
                : pawn.RaceProps.Animal ? Constant.PersonaAnimal
                : pawn.RaceProps.IsMechanoid ? Constant.PersonaMech
                : Constant.PersonaNonHuman;
            hediff.Personality = randomPersonalityData.Persona;
        
            // The captive/outsider discount is no longer baked in here — it moved to
            // TalkWeight.StandingFactor, which is re-read on every query. Baking it
            // in was rim-universe #16: recruit a prisoner and they stayed near-mute
            // forever, because this branch never ran a second time.
            hediff.TalkInitiationWeight = randomPersonalityData.Chattiness;
        
            pawn.health.AddHediff(hediff);
        }
    
        // Ensure dictionary is initialized (for both new and existing hediffs)
        hediff._spokenThoughtTicks ??= new Dictionary<string, int>();
    
        return hediff;
    }
    
    /// <summary>
    /// Drop entries that can no longer suppress anything. rim-universe #4: the key is
    /// defName_stageIndex and RimWorld ships several hundred ThoughtDefs, many
    /// multi-stage, so over a long colony every pawn converged on holding most of
    /// them — forever, in every save, because nothing ever pruned this and it is
    /// scribed.
    /// </summary>
    private void Prune(int currentTick)
    {
        if (_spokenThoughtTicks == null || _spokenThoughtTicks.Count == 0) return;

        List<string> dead = null;
        foreach (var pair in _spokenThoughtTicks)
            if (TalkCacheMath.Expired(pair.Value, currentTick, MaxSuppressTicks))
                (dead ??= new List<string>()).Add(pair.Key);

        if (dead == null) return;
        foreach (var key in dead) _spokenThoughtTicks.Remove(key);
    }

    // Check if thought was spoken recently, if not mark it as spoken
    // Returns true if successfully marked (was not spoken recently)
    // Returns false if already spoken recently (within intervalTicks)
    public bool TryMarkAsSpoken(Thought thought)
    {
        string key = $"{thought.def.defName}_{thought.CurStageIndex}";
        int currentTick = Find.TickManager.TicksGame;
    
        // Rand, not UnityEngine.Random. RimWorld seeds Rand so that reloading a save
        // reproduces the same rolls; Unity's generator sits outside that, so two loads
        // of the same save diverged in when a thought became speakable again.
        // rim-universe #4.
        int randomInterval = Rand.Range(60000, MaxSuppressTicks);
    
        if (_spokenThoughtTicks.TryGetValue(key, out int lastTick))
        {
            if (currentTick - lastTick < randomInterval)
            {
                return false; // Already spoken recently
            }
        }
    
        _spokenThoughtTicks[key] = currentTick;
        Prune(currentTick);

        // Also mark for nearby pawns so they don't talk about the same thing
        var nearbyPawns = PawnSelector.GetAllNearByPawns(thought.pawn);
        foreach (var p in nearbyPawns)
        {
            if (p == thought.pawn) continue; 
            var hediff = GetOrAddNew(p);
            if (hediff != null)
            {
                hediff._spokenThoughtTicks[key] = currentTick;
                hediff.Prune(currentTick);
            }
        }

        return true;
    }
}