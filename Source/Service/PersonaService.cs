using System;
using System.Threading.Tasks;
using RimTalk.Service;
using RimTalk.Util;
using Verse;

namespace RimTalk.Data;

public static class PersonaService
{
    public static string GetPersonality(Pawn pawn)
    {
        return Hediff_Persona.GetOrAddNew(pawn).Personality;
    }

    public static void SetPersonality(Pawn pawn, string personality)
    {
        Hediff_Persona.GetOrAddNew(pawn).Personality = personality;
    }

    /// <summary>
    /// The baseline the player set. Editors read and write this — showing them the
    /// effective value in a field they can edit would make the slider drift every
    /// time the pawn got tired.
    /// </summary>
    public static float GetTalkInitiationWeight(Pawn pawn)
    {
        return Hediff_Persona.GetOrAddNew(pawn).TalkInitiationWeight;
    }

    /// <summary>
    /// What selection should actually use: the baseline moved by mood, rest, health
    /// and standing. rim-universe #16 — the stored value was written once at spawn and
    /// never again, so a recruited prisoner stayed near-mute for life.
    /// </summary>
    public static float GetEffectiveTalkWeight(Pawn pawn)
    {
        return Hediff_Persona.GetOrAddNew(pawn)?.EffectiveTalkWeight ?? 0f;
    }

    public static void SetTalkInitiationWeight(Pawn pawn, float frequency)
    {
        Hediff_Persona.GetOrAddNew(pawn).TalkInitiationWeight = frequency;
    }

    public static async Task<PersonalityData> GeneratePersona(Pawn pawn)
    {
        string pawnBackstory = PromptService.CreatePawnBackstory(pawn, PromptService.InfoLevel.Full);

        try
        {
            var request = new TalkRequest(Constant.PersonaGenInstruction, pawn)
            {
                Context = $"[Character]\n{pawnBackstory}"
            };
            PersonalityData personalityData = await AIService.Query<PersonalityData>(request);

            if (personalityData?.Persona != null)
            {
                personalityData.Persona = personalityData.Persona.Replace("**", "").Trim();
            }

            return personalityData;
        }
        catch (Exception e)
        {
            Logger.Error(e.Message);
            return null;
        }
    }
}