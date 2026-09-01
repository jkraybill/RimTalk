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

    /// <summary>
    /// Describe what will satisfy this goal, with current progress.
    ///
    /// The wording, the rounding and the decision about which kinds carry a number
    /// all live in GoalMath, where they are tested for real. This is the map read
    /// and nothing else — the twin that used to live here rounded to nearest on
    /// both halves and could print "3/3" on an unmet goal.
    /// </summary>
    private static string DescribeCriteriaWithProgress(Pawn pawn, GoalKind kind, float target) =>
        GoalMath.Criteria(kind, target, ProseScene.GatherColony(pawn?.Map));

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