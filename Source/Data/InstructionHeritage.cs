using System;
using System.Linq;

namespace RimTalk.Data;

/// <summary>
/// Whether a saved base instruction is something the user wrote, or a copy of a
/// shipped default they have simply been carrying around.
///
/// rim-universe. Found by reading an assembled prompt out of JK's Player.log: the
/// mod had been rewritten to send prose, his profile and scene blocks were the new
/// ones, and the system instruction was still the old terse "Role-play RimWorld
/// character per profile". The whole register half of the rewrite had never reached
/// the model, for him or for anyone who had played before it.
///
/// The mechanism is a migration doing exactly what it was told:
///
///   if (SimpleModeInstruction is the default)
///       if (preset's Base Instruction != Constant.DefaultInstruction)
///           SimpleModeInstruction = preset's Base Instruction;   // "a customisation"
///
/// A preset is seeded with whatever the default was on the day it was created. The
/// moment the default changes, every existing copy stops matching it and is promoted
/// to "the user's custom instruction" — permanently, silently, and in preference to
/// the new one. A stamp that was supposed to record the default becomes a veto over it.
///
/// Detection is by opening line rather than by whole text, because the defaults have
/// been through several revisions and interpolate {Lang}, so exact matching would
/// recognise one of them and miss the rest. A user who wrote their own instruction
/// beginning with the same line loses it — that is the cost, it is stated rather than
/// hidden, and the upgrade is logged by the caller rather than done in silence.
/// </summary>
public static class InstructionHeritage
{
    /// <summary>
    /// The opening line of every base instruction RimTalk has shipped before the prose
    /// rewrite. Upstream's original, the fork's early revisions, and the version that
    /// was live when the presets in the wild were saved all open this way.
    /// </summary>
    static readonly string[] SupersededOpenings =
    {
        "Role-play RimWorld character per profile",
    };

    /// <summary>True when this is a shipped default from before the prose rewrite.</summary>
    public static bool IsSuperseded(string instruction)
    {
        var first = FirstLine(instruction);
        return first != null && SupersededOpenings.Any(
            o => string.Equals(first, o, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// True when this is a shipped default of any vintage, current included — i.e. not
    /// something the user wrote and not worth preserving over the current one.
    /// </summary>
    public static bool IsShipped(string instruction, string currentDefault) =>
        IsSuperseded(instruction) || Same(instruction, currentDefault);

    /// <summary>
    /// Line endings and trailing whitespace differ between what the compiler produces
    /// and what comes back out of a base64 round trip through Scribe.
    /// </summary>
    public static bool Same(string a, string b) =>
        string.Equals(Normalise(a), Normalise(b), StringComparison.Ordinal);

    static string Normalise(string s) =>
        (s ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Trim();

    static string FirstLine(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var line = Normalise(s).Split('\n').FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));
        return line?.Trim().TrimEnd('.');
    }
}
