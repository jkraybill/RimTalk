using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimTalk.Data;
using RimTalk.Goals;
using RimTalk.Prose;
using RimTalk.UI;
using RimTalk.Util;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk.Patches;

[StaticConstructorOnStartup]
public static class BioTabPersonalityPatch
{
    private static readonly Texture2D RimTalkIcon = ContentFinder<Texture2D>.Get("UI/RimTalkIcon");

    private static void AddPersonaElement(Pawn pawn)
    {
        if (!pawn.IsColonist && !pawn.IsPrisonerOfColony && !pawn.HasVocalLink())
            return;

        var tmpStackElements =
            (List<GenUI.AnonymousStackElement>)AccessTools.Field(typeof(CharacterCardUtility), "tmpStackElements")
                .GetValue(null);
        if (tmpStackElements == null) return;

        string personaLabelText = "RimTalk.BioTab.RimTalkPersona".Translate();
        float textWidth = Text.CalcSize(personaLabelText).x;
        float totalLabelWidth = 22f + 5f + textWidth + 5f; // Icon + padding + text + padding

        tmpStackElements.Add(new GenUI.AnonymousStackElement
        {
            width = totalLabelWidth,
            drawer = rect =>
            {
                Widgets.DrawOptionBackground(rect, false);
                Widgets.DrawHighlightIfMouseover(rect);

                string persona = PersonaService.GetPersonality(pawn);
                float chattiness = PersonaService.GetTalkInitiationWeight(pawn);
                string tooltipText =
                    $"{"RimTalk.PersonaEditor.Title".Translate(pawn.LabelShort).Colorize(ColoredText.TipSectionTitleColor)}\n\n{persona}\n\n{"RimTalk.PersonaEditor.Chattiness".Translate().Colorize(ColoredText.TipSectionTitleColor)} {chattiness:0.00}";
                TooltipHandler.TipRegion(rect, tooltipText);

                Rect iconRect = new Rect(rect.x + 2f, rect.y + 1f, 20f, 20f);
                GUI.DrawTexture(iconRect, RimTalkIcon);

                Rect labelRect = new Rect(iconRect.xMax + 5f, rect.y, textWidth, rect.height);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(labelRect, personaLabelText);
                Text.Anchor = TextAnchor.UpperLeft;

                if (Widgets.ButtonInvisible(rect))
                {
                    Find.WindowStack.Add(new PersonaEditorWindow(pawn));
                }
            }
        });
    }

    private static void AddGoalElement(Pawn pawn)
    {
        if (!pawn.IsColonist) return;

        var goal = GoalStore.Active(pawn);
        if (goal == null) return;

        var tmpStackElements =
            (List<GenUI.AnonymousStackElement>)AccessTools.Field(typeof(CharacterCardUtility), "tmpStackElements")
                .GetValue(null);
        if (tmpStackElements == null) return;

        string goalLabelText = "Goal";
        float textWidth = Text.CalcSize(goalLabelText).x;
        float totalLabelWidth = textWidth + 10f;

        tmpStackElements.Add(new GenUI.AnonymousStackElement
        {
            width = totalLabelWidth,
            drawer = rect =>
            {
                Widgets.DrawOptionBackground(rect, false);
                Widgets.DrawHighlightIfMouseover(rect);

                var elapsed = GoalMath.ElapsedDays(GenTicks.TicksGame - goal.SetTick);
                var remaining = GoalMath.RemainingDays(goal.ExpiryTick - GenTicks.TicksGame);
                var criteria = DescribeCriteriaWithProgress(pawn, goal.Kind, goal.Target);
                string tooltipText =
                    $"{"Current Goal".Colorize(ColoredText.TipSectionTitleColor)}\n\n" +
                    $"\"{goal.Statement}\"\n\n" +
                    $"{"Success:".Colorize(ColoredText.SubtleGrayColor)} {criteria}\n\n" +
                    $"Set {elapsed} ago\n" +
                    $"{remaining} remaining";
                TooltipHandler.TipRegion(rect, tooltipText);

                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rect, goalLabelText);
                Text.Anchor = TextAnchor.UpperLeft;
            }
        });
    }

    /// <summary>Describe what will satisfy this goal, with current progress.</summary>
    private static string DescribeCriteriaWithProgress(Pawn pawn, GoalKind kind, float target)
    {
        var facts = ProseScene.GatherColony(pawn?.Map);
        var (current, showProgress) = GetCurrentValue(kind, facts);

        var criteria = kind switch
        {
            GoalKind.FoodSecurity => $"Have {target:0}+ days of food stockpiled",
            GoalKind.Medicine => $"Have {target:0}+ medicine in storage",
            GoalKind.Shelter => "Everyone has a bed",
            GoalKind.Power => "Colony power grid is online",
            GoalKind.BaseDefence => $"Have {target:0}+ defensive positions",
            GoalKind.Companionship => $"Have {target:0}+ colonists",
            _ => "Unknown",
        };

        if (showProgress && current >= 0)
            criteria += $" (Progress: {current:0}/{target:0})";

        return criteria;
    }

    private static (float current, bool showProgress) GetCurrentValue(GoalKind kind, ColonyFacts facts)
    {
        if (facts == null) return (-1, false);

        return kind switch
        {
            GoalKind.FoodSecurity => (facts.FoodDays, true),
            GoalKind.Medicine => (facts.MedicineCount, true),
            GoalKind.Shelter => (facts.Colonists - facts.ColonistsWithoutBed, false),
            GoalKind.Power => (facts.HasPower == true ? 1 : 0, false),
            GoalKind.BaseDefence => (facts.Turrets, true),
            GoalKind.Companionship => (facts.Colonists, true),
            _ => (-1, false),
        };
    }

    [HarmonyPatch(typeof(CharacterCardUtility), "DoTopStack")]
    public static class DoTopStack_Patch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo anchorMethod = AccessTools.Method(
                typeof(QuestUtility),
                nameof(QuestUtility.AppendInspectStringsFromQuestParts),
                new Type[]
                {
                    typeof(Action<string, Quest>),
                    typeof(ISelectable),
                    typeof(int).MakeByRefType()
                }
            );

            foreach (var instruction in instructions)
            {
                yield return instruction;

                if (instruction.Calls(anchorMethod))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0); // Load 'pawn'
                    yield return new CodeInstruction(OpCodes.Call,
                        AccessTools.Method(typeof(BioTabPersonalityPatch), nameof(AddPersonaElement)));
                    yield return new CodeInstruction(OpCodes.Ldarg_0); // Load 'pawn' again
                    yield return new CodeInstruction(OpCodes.Call,
                        AccessTools.Method(typeof(BioTabPersonalityPatch), nameof(AddGoalElement)));
                }
            }
        }
    }
}