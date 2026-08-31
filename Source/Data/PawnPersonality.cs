using System.Collections.Generic;
using Verse;

namespace RimTalk.Data;

/// <summary>
/// Rich personality derived from backstory expansion. rim-universe #51.
///
/// Generated once when a pawn enters the colony's orbit. The LLM reads their
/// backstories, traits, skills, and biome, then produces a coherent personality
/// that feeds everything downstream: profiles, goals, gossip, conversations.
///
/// This replaces multiple separate LLM calls (topics, preferences, persona) with
/// one upstream expansion that keeps the pawn internally consistent.
/// </summary>
public class PawnPersonality : IExposable
{
    public int PawnId;
    public string PawnName;

    /// <summary>The expanded backstory narrative — 2-3 paragraphs.</summary>
    public string Narrative;

    /// <summary>Speech pattern: "folksy", "formal", "clipped", "rambling".</summary>
    public string SpeechStyle;

    /// <summary>Personality quirks: "hates mornings", "laughs too loud".</summary>
    public List<string> Quirks = new();

    /// <summary>Things they bring up in idle conversation.</summary>
    public List<string> Topics = new();

    // Preferences — biome-filtered, backstory-derived
    public string LovedFood;
    public string HatedFood;
    public string LovedAnimal;
    public string HatedAnimal;
    public string LovedMaterial;

    // Ideology preferences (if applicable)
    public string LovedRitual;
    public string HatedRitual;

    public PawnPersonality() { }

    public PawnPersonality(Pawn pawn)
    {
        PawnId = pawn?.thingIDNumber ?? 0;
        PawnName = pawn?.LabelShort;
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref PawnId, "pawnId");
        Scribe_Values.Look(ref PawnName, "pawnName");
        Scribe_Values.Look(ref Narrative, "narrative");
        Scribe_Values.Look(ref SpeechStyle, "speechStyle");
        Scribe_Collections.Look(ref Quirks, "quirks", LookMode.Value);
        Scribe_Collections.Look(ref Topics, "topics", LookMode.Value);
        Scribe_Values.Look(ref LovedFood, "lovedFood");
        Scribe_Values.Look(ref HatedFood, "hatedFood");
        Scribe_Values.Look(ref LovedAnimal, "lovedAnimal");
        Scribe_Values.Look(ref HatedAnimal, "hatedAnimal");
        Scribe_Values.Look(ref LovedMaterial, "lovedMaterial");
        Scribe_Values.Look(ref LovedRitual, "lovedRitual");
        Scribe_Values.Look(ref HatedRitual, "hatedRitual");

        Quirks ??= new List<string>();
        Topics ??= new List<string>();
    }
}
