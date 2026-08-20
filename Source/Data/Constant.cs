using System;
using RimTalk.Prompt;
using Verse;

namespace RimTalk.Data;

public static class Constant
{
    public const string DefaultCloudModel = "gemma-4-26b-a4b-it";
    public const string FallbackCloudModel = "gemma-4-31b-it";
    public const string ChooseModel = "(choose model)";

    public static string Lang => LanguageDatabase.activeLanguage?.info?.friendlyNameNative ?? "English";
    public static HediffDef VocalLinkDef => DefDatabase<HediffDef>.GetNamedSilentFail("VocalLinkImplant");

    /// <summary>
    /// Rewritten S166 after measuring the old one in the prompt lab.
    ///
    /// The previous instruction was ~90 words of role sketches and a JSONL contract.
    /// It said "Conversation = 4-8 short turns" and produced ONE turn in 3 of 4 raid
    /// samples and 4 of 4 quiet-morning samples -- it did not obey its own rule. It
    /// also gave voices to Prisoner, Slave, Visitor and Enemy and none to Colonist,
    /// the commonest speaker by an enormous margin.
    ///
    /// This one says what the job is, what the world is, how people sound, what they
    /// may never do, and shows two examples. Examples teach register; adjectives do
    /// not.
    /// </summary>
    public static string DefaultInstruction =>
        $"""
         You write dialogue for colonists in RimWorld, a survival sim on a hostile
         frontier planet. Each line appears as a speech bubble above someone's head
         while the player watches their colony.

         These are ordinary people in a place that is trying to kill them. They are
         not heroes and they do not know they are in a story.

         How they speak:
         Plainly, in {Lang}, about what is actually in front of them. Short. They
         understate. They swear when they mean it. Nobody narrates their own feelings
         and nobody reaches for a metaphor they would not use out loud.

         What they never do:
         Explain how they came to be on this planet. None of them knows, and none of
         them ever will. They may guess from their own past. They may never state it
         as fact.

         Two examples of the register:

           Rice again. Always rice.
           I'd kill something just to taste it.

           They're coming up the east side, and I'm still not done with you about
           the rice.

         Write only what is said. No stage directions, no asterisks, no narration.
         """;

    /// <summary>
    /// Moved LAST in the preset, closest to generation, and hardened.
    ///
    /// The prose instruction above is conversational, and a conversational system
    /// prompt loosens formatting generally: one lab run parsed 89% of its JSONL
    /// against 100% for the terse original. RimTalk silently DROPS a line it cannot
    /// parse, so a formatting slip is a lost turn nobody sees.
    /// </summary>
    public const string JsonInstruction = """
                                           FORMAT — this overrides everything above.

                                           Reply with JSON Lines and nothing else. One complete JSON object per line:

                                           {"name": "Someone", "text": "What they said."}

                                           Every line opens with { and closes with }. No trailing commas, no
                                           parentheses, no markdown fences, no commentary before or after.
                                           One speaker per line.
                                           """;
    
    public const string SocialInstruction = """
                                           Optional keys (Include only if social interaction occurs):
                                           "act": Insult, Slight, Chat, Kind
                                           "target": targetName
                                           """;

    // Get the current instruction from settings or fallback to default, always append JSON instruction
    // NOTE: This is now primarily used as a fallback. The new PromptManager system is preferred.
    public static string Instruction
    {
        get
        {
            var settings = Settings.Get();
            var baseInstruction = GetBaseInstruction();
        
            return baseInstruction + "\n" + JsonInstruction + (settings.ApplyMoodAndSocialEffects ? "\n" + SocialInstruction : "");
        }
    }

    private static string GetBaseInstruction()
    {
        var preset = PromptManager.Instance?.GetActivePreset();
        if (preset == null) return DefaultInstruction;

        var entry = preset.Entries.FirstOrDefault(e =>
            string.Equals(e.Name, "Base Instruction", StringComparison.OrdinalIgnoreCase))
                    ?? preset.Entries.FirstOrDefault(e =>
                        e.Role == PromptRole.System && e.Position == PromptPosition.Relative);

        // A preset carries whatever the default was on the day it was created. Once the
        // default changes, that copy is not a customisation — it is a fossil, and
        // preferring it means the rewrite never reaches anyone who played before it.
        // The decision lives in InstructionHeritage so it can be executed in the tests.
        return InstructionHeritage.Resolve(entry?.Content, DefaultInstruction);
    }
    
    // JSON instruction for use by PromptManager
    public static string GetJsonInstruction(bool includeSocialEffects)
    {
        return JsonInstruction + (includeSocialEffects ? "\n" + SocialInstruction : "");
    }

    public static string PersonaGenInstruction =>
        $"""
         Create a funny persona (to be used as conversation style) in {Lang}. Must be short in 1 sentence.
         Include: how they speak, their main attitude, and one weird quirk that makes them memorable.
         Be specific and bold, avoid boring traits.
         Also determine chattiness: 0.1-0.3 (quiet), 0.4-0.7 (normal), 0.8-1.0 (chatty).
         Must return JSON only, with fields 'persona' (string) and 'chattiness' (float).
         """;

    private static PersonalityData[] _personalities;
    public static PersonalityData[] Personalities => _personalities ??=
    [
        new("RimTalk.Persona.CheerfulHelper".Translate(), 0.75f),
        new("RimTalk.Persona.CynicalRealist".Translate(), 0.4f),
        new("RimTalk.Persona.ShyThinker".Translate(), 0.15f),
        new("RimTalk.Persona.Hothead".Translate(), 0.6f),
        new("RimTalk.Persona.Philosopher".Translate(), 0.8f),
        new("RimTalk.Persona.DarkHumorist".Translate(), 0.7f),
        new("RimTalk.Persona.Caregiver".Translate(), 0.75f),
        new("RimTalk.Persona.Opportunist".Translate(), 0.65f),
        new("RimTalk.Persona.OptimisticDreamer".Translate(), 0.8f),
        new("RimTalk.Persona.Pessimist".Translate(), 0.35f),
        new("RimTalk.Persona.StoicSoldier".Translate(), 0.2f),
        new("RimTalk.Persona.FreeSpirit".Translate(), 0.85f),
        new("RimTalk.Persona.Workaholic".Translate(), 0.25f),
        new("RimTalk.Persona.Slacker".Translate(), 0.55f),
        new("RimTalk.Persona.NobleIdealist".Translate(), 0.75f),
        new("RimTalk.Persona.StreetwiseSurvivor".Translate(), 0.5f),
        new("RimTalk.Persona.Scholar".Translate(), 0.8f),
        new("RimTalk.Persona.Jokester".Translate(), 0.9f),
        new("RimTalk.Persona.MelancholicPoet".Translate(), 0.2f),
        new("RimTalk.Persona.Paranoid".Translate(), 0.3f),
        new("RimTalk.Persona.Commander".Translate(), 0.5f),
        new("RimTalk.Persona.Coward".Translate(), 0.35f),
        new("RimTalk.Persona.ArrogantNoble".Translate(), 0.7f),
        new("RimTalk.Persona.LoyalCompanion".Translate(), 0.65f),
        new("RimTalk.Persona.CuriousExplorer".Translate(), 0.85f),
        new("RimTalk.Persona.ColdRationalist".Translate(), 0.15f),
        new("RimTalk.Persona.FlirtatiousCharmer".Translate(), 0.95f),
        new("RimTalk.Persona.BitterOutcast".Translate(), 0.25f),
        new("RimTalk.Persona.Zealot".Translate(), 0.9f),
        new("RimTalk.Persona.Trickster".Translate(), 0.8f),
        new("RimTalk.Persona.DeadpanRealist".Translate(), 0.3f),
        new("RimTalk.Persona.ChildAtHeart".Translate(), 0.85f),
        new("RimTalk.Persona.SkepticalScientist".Translate(), 0.6f),
        new("RimTalk.Persona.Martyr".Translate(), 0.65f),
        new("RimTalk.Persona.Manipulator".Translate(), 0.75f),
        new("RimTalk.Persona.Rebel".Translate(), 0.7f),
        new("RimTalk.Persona.Oddball".Translate(), 0.6f),
        new("RimTalk.Persona.GreedyMerchant".Translate(), 0.85f),
        new("RimTalk.Persona.Romantic".Translate(), 0.8f),
        new("RimTalk.Persona.BattleManiac".Translate(), 0.4f),
        new("RimTalk.Persona.GrumpyElder".Translate(), 0.5f),
        new("RimTalk.Persona.AmbitiousClimber".Translate(), 0.75f),
        new("RimTalk.Persona.Mediator".Translate(), 0.7f),
        new("RimTalk.Persona.Gambler".Translate(), 0.75f),
        new("RimTalk.Persona.ArtisticSoul".Translate(), 0.45f),
        new("RimTalk.Persona.Drifter".Translate(), 0.3f),
        new("RimTalk.Persona.Perfectionist".Translate(), 0.4f),
        new("RimTalk.Persona.Vengeful".Translate(), 0.35f)
    ];

    private static PersonalityData _personaAnimal;
    public static PersonalityData PersonaAnimal => _personaAnimal ??= new("RimTalk.Persona.Animal".Translate(), 0.2f);

    private static PersonalityData _personaMech;
    public static PersonalityData PersonaMech => _personaMech ??= new("RimTalk.Persona.Mech".Translate(), 0.2f);

    private static PersonalityData _personaNonHuman;
    public static PersonalityData PersonaNonHuman => _personaNonHuman ??= new("RimTalk.Persona.NonHuman".Translate(), 0.2f);
}
