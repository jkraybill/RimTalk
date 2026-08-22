using Verse;

namespace RimTalk.Data
{
    public class ContextSettings : IExposable
    {
        public bool EnableContextOptimization = false;
        public int MaxPawnContextCount = 3;
        // rim-universe #9. One is defensible as a token economy and indefensible as a
        // default for a mod whose selling point is characters: the model saw exactly one
        // prior turn. Four exchanges is still cheap, and TalkMemory now caps by token
        // estimate rather than by count, so a long reply no longer costs the same as a
        // short one.
        public int ConversationHistoryCount = 4;

        /// <summary>
        /// How many participants get a full profile. rim-universe #7: this used to be
        /// hardcoded to one — index 0 — so in a two-hander one speaker had no skills,
        /// no equipment and three traits, decided by list position and re-decided
        /// every time the pair spoke.
        ///
        /// Two, so both sides of an ordinary conversation are rounded. Bystanders
        /// stay Short. The cost is bounded and predictable: one extra Normal block is
        /// roughly a skills line, an equipment line and a few more traits.
        /// </summary>
        public int FullProfileParticipants = 2;
        
        // Pawn Info
        public bool IncludeRace = true;
        public bool IncludeNotableGenes = true;
        public bool IncludeIdeology = true;
        public bool IncludeBackstory = true;
        public bool IncludeTraits = true;
        public bool IncludeSkills = true;
        public bool IncludeHealth = true;
        public bool IncludeMood = true;
        public bool IncludeThoughts = true;
        public bool IncludeRelations = true;
        public bool IncludeEquipment = true;
        public bool IncludePrisonerSlaveStatus = false;

        // Storytelling (rim-universe #34, #35)
        //
        // A colony that always talks proportionately about its situation is accurate
        // and completely forgettable. These three exist to keep the register mismatched
        // on purpose.
        // CUT by unanimous roundtable verdict, S166. Three reviewers independently
        // said a prompt line is the wrong instrument for a tonal goal: "a dial the
        // model can see is a dial the model will play to". The code stays so the
        // decision is reversible and A/B-able; it does not ship on.
        public bool IncludeScaleGap = false;
        public bool IncludeDominantTrait = true;
        public float PreoccupationChance = 0.5f;

        // Narrative letters. Off would make the whole design invisible again, so it
        // ships on -- but it is a toggle because letter spam is the fastest route to
        // uninstall and this is the first of several senders.
        public bool NarrativeLetters = true;

        /// <summary>
        /// Colonist goals. rim-universe #28. OFF by default, and unlike everything
        /// else here that is not caution about the prompt — it is the only feature in
        /// this mod that writes MOOD. A player's colony balance is theirs, and a mood
        /// mechanic that arrives switched on with a mod update is the kind of thing
        /// that gets a mod uninstalled rather than configured.
        ///
        /// The gate covers generation AND the prompt block, so with it off the mod
        /// sends exactly the prompt it sent before goals existed.
        /// </summary>
        public bool Goals = false;

        // The whole prompt shape. On = prose profile and prose scene; off = the
        // original labelled field dump. Kept switchable because it is a large change
        // and the old path still has to be A/B-able against it.
        public bool ProsePrompt = true;

        // Environment
        public bool IncludeTime = true;
        public bool IncludeDate = false;
        public bool IncludeSeason = true;
        public bool IncludeWeather = true;
        public bool IncludeLocationAndTemperature = true;
        public bool IncludeTerrain = false;
        public bool IncludeBeauty = false;
        public bool IncludeCleanliness = false;
        public bool IncludeSurroundings = false;
        public bool IncludeWealth = false;

        public void ExposeData()
        {
            Scribe_Values.Look(ref EnableContextOptimization, "EnableContextOptimization", false);
            Scribe_Values.Look(ref MaxPawnContextCount, "MaxPawnContextCount", 3);
            Scribe_Values.Look(ref ConversationHistoryCount, "ConversationHistoryCount", 4);
            Scribe_Values.Look(ref FullProfileParticipants, "FullProfileParticipants", 2);
            Scribe_Values.Look(ref IncludeRace, "IncludeRace", true);
            Scribe_Values.Look(ref IncludeNotableGenes, "IncludeNotableGenes", true);
            Scribe_Values.Look(ref IncludeIdeology, "IncludeIdeology", true);
            Scribe_Values.Look(ref IncludeBackstory, "IncludeBackstory", true);
            Scribe_Values.Look(ref IncludeTraits, "IncludeTraits", true);
            Scribe_Values.Look(ref IncludeSkills, "IncludeSkills", true);
            Scribe_Values.Look(ref IncludeHealth, "IncludeHealth", true);
            Scribe_Values.Look(ref IncludeMood, "IncludeMood", true);
            Scribe_Values.Look(ref IncludeThoughts, "IncludeThoughts", true);
            Scribe_Values.Look(ref IncludeRelations, "IncludeRelations", true);
            Scribe_Values.Look(ref IncludeEquipment, "IncludeEquipment", true);
            Scribe_Values.Look(ref IncludePrisonerSlaveStatus, "IncludePrisonerSlaveStatus", false);

            Scribe_Values.Look(ref IncludeScaleGap, "IncludeScaleGap", false);
            Scribe_Values.Look(ref IncludeDominantTrait, "IncludeDominantTrait", true);
            Scribe_Values.Look(ref PreoccupationChance, "PreoccupationChance", 0.5f);
            Scribe_Values.Look(ref NarrativeLetters, "NarrativeLetters", true);
            Scribe_Values.Look(ref Goals, "Goals", false);
            Scribe_Values.Look(ref ProsePrompt, "ProsePrompt", true);

            Scribe_Values.Look(ref IncludeTime, "IncludeTime", true);
            Scribe_Values.Look(ref IncludeDate, "IncludeDate", false);
            Scribe_Values.Look(ref IncludeSeason, "IncludeSeason", true);
            Scribe_Values.Look(ref IncludeWeather, "IncludeWeather", true);
            Scribe_Values.Look(ref IncludeLocationAndTemperature, "IncludeLocationAndTemperature", true);
            Scribe_Values.Look(ref IncludeTerrain, "IncludeTerrain", false);
            Scribe_Values.Look(ref IncludeBeauty, "IncludeBeauty", false);
            Scribe_Values.Look(ref IncludeCleanliness, "IncludeCleanliness", false);
            Scribe_Values.Look(ref IncludeSurroundings, "IncludeSurroundings", false);
            Scribe_Values.Look(ref IncludeWealth, "IncludeWealth", false);
        }
    }
}
