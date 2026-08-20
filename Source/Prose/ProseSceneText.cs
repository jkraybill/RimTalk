using System;
using System.Collections.Generic;
using System.Linq;

namespace RimTalk.Prose;

/// <summary>
/// What kind of exchange this is. The old code passed prose mode a pre-formatted
/// instruction — "Bren dialogue short, urgent tone (colonist/command)" — which is a
/// field dump wearing a prose prompt's clothes. The shape is a fact; the sentence
/// that states it belongs to whichever renderer is speaking.
/// </summary>
public enum SceneShape
{
    /// <summary>Two or more people, taking turns.</summary>
    Conversation,
    /// <summary>One person, out loud, to nobody.</summary>
    Monologue,
    /// <summary>Mid-fight. Short, and no time to be careful.</summary>
    Urgent,
    /// <summary>Mid-fight, and not in charge of anything — a prisoner or a slave.</summary>
    UrgentAfraid,
    /// <summary>The player said something and wants one answer.</summary>
    ReplyToPlayer,
    /// <summary>The player said something and the scene carries on from there.</summary>
    ReplyToPlayerMulti,
}

/// <summary>Someone else in the scene, as the envelope needs them.</summary>
public class PersonNote
{
    public string Name;
    public string Activity;   // already tag-stripped by the gatherer; may be blank
    public bool InDanger;
}

/// <summary>
/// Everything the scene envelope says, reduced to primitives. Same reason as
/// <see cref="PawnFacts"/>: `Map`, `Room` and `Find.TickManager` are unconstructable,
/// so without this split the envelope can only be read, never run.
/// </summary>
public class SceneFacts
{
    public int Hour24;
    public int TempCelsius;
    public string Weather;              // null or "Clear" -> not worth a word
    public string RoomLabel;            // null -> "indoors" when Indoors is true
    public bool Indoors;
    public string PawnName = "";
    public string PawnActivity;         // blank -> standing still
    public List<PersonNote> Others = new();
    public int OtherColonistsOnMap;
    /// <summary>
    /// What this pawn was talking about before the situation escalated, as the game
    /// reported it — a whole sentence, third person, e.g. "Bren chatted about the rice
    /// with Kess." rim-universe #34.
    ///
    /// It is a sentence, which is why it cannot be dropped into a noun slot. The old
    /// line did exactly that and produced "A moment ago Bren was on about bren chatted
    /// about the rice with Kess, and is not finished with it."
    /// </summary>
    /// <summary>
    /// What this conversation is about, when something set it. rim-universe #44 and a
    /// regression from #34's refactor: BuildDialogueType has always produced a topic
    /// for ordinary conversation, and when the frame replaced two pre-formatted
    /// strings with fields, prose mode was wired to Preoccupation — which is only ever
    /// set in combat — and the everyday topic stopped reaching the model entirely.
    /// </summary>
    public string Topic;

    public string Preoccupation;

    /// <summary>#35's scale gap, in pieces rather than pre-formatted.</summary>
    public string Situation;
    public string Concern;

    /// <summary>Where this is and how it is doing. rim-universe #23. Null when unknown.</summary>
    public ColonyFacts Colony;

    public SceneShape Shape = SceneShape.Conversation;

    /// <summary>Who the player is speaking as, and what they said. Reply shapes only.</summary>
    public string OtherName;
    public string PlayerLine;

    /// <summary>
    /// Lines these people have said lately, newest first, so the model can be told
    /// not to say them again. #41: repetition is concentrated almost entirely in the
    /// opening line, and it is what "slop" turns out to be at scale — the same
    /// colonist, in the same room, on the same job, saying nearly the same thing for
    /// a whole quadrum.
    /// </summary>
    public List<string> RecentLines = new();
}

/// <summary>
/// The moment as sentences. No RimWorld types: source-linked into the test project
/// and run for real.
/// </summary>
public static class ProseSceneText
{
    public static string Compose(SceneFacts f)
    {
        if (f == null) return "";
        var lines = new List<string> { Setting(f) };

        // Second, because a person knows where they are before they notice who is in
        // the room with them, and because it is the only place the biome and the
        // colony's name ever appear. rim-universe #23.
        var colony = ProseColonyText.Compose(f.Colony);
        if (colony != null) lines.Add(colony);

        var others = Others(f);
        if (others != null) lines.Add(others);
        else if (f.OtherColonistsOnMap == 0) lines.Add("There is nobody to hear it.");

        // The preoccupation wins when both are set: that only happens in combat, where
        // the older topic is the one being carried through and the point is the gap
        // between it and the situation.
        var about = Preoccupied(f) ?? About(f);
        if (about != null) lines.Add(about);

        var gap = ScaleGap(f);
        if (gap != null) lines.Add(gap);

        lines.Add(Instruction(f));

        var recent = Recent(f);
        if (recent != null) lines.Add(recent);

        return string.Join("\n\n", lines.Where(l => !string.IsNullOrWhiteSpace(l)));
    }

    static string Setting(SceneFacts f)
    {
        // Weather and temperature are qualities of the air; a room is a place a body
        // is standing in. Joining them into one list produced "Cool and open ground"
        // and "freezing and bedroom" — the reason to split them is that they are not
        // the same kind of thing, and English notices.
        var air = new List<string>();
        if (!string.IsNullOrWhiteSpace(f.Weather) && !f.Weather.Equals("clear", StringComparison.OrdinalIgnoreCase))
            air.Add(ProseWords.Mid(f.Weather));
        air.Add(ProseWords.Cold(f.TempCelsius));

        var when = air.Count > 1
            ? $"{ProseWords.TimeOfDay(f.Hour24)}, {air[1]}, with {air[0]}."
            : $"{ProseWords.TimeOfDay(f.Hour24)}, {air[0]}.";

        var doing = string.IsNullOrWhiteSpace(f.PawnActivity)
            ? "standing still"
            : ProseWords.Mid(f.PawnActivity);

        return $"{when} {f.PawnName} is {doing} {Place(f)}.";
    }

    /// <summary>"in the dining room", "indoors", "on open ground".</summary>
    static string Place(SceneFacts f)
    {
        if (!f.Indoors) return "on open ground";
        return string.IsNullOrWhiteSpace(f.RoomLabel) ? "indoors" : $"in the {ProseWords.Mid(f.RoomLabel)}";
    }

    /// <summary>
    /// What the conversation was about a moment ago, when the game escalated and #34
    /// carried it through instead of deleting it.
    ///
    /// Reported, not framed. The sentence that shipped — "…and is not finished with
    /// it" — handed the model a stance in a form it could simply say out loud, and it
    /// did: JK read "I'm still not finished with that sick knot" in a bubble. A prompt
    /// should state a thing, not supply a sentence to copy.
    /// </summary>
    static string Preoccupied(SceneFacts f)
    {
        if (string.IsNullOrWhiteSpace(f.Preoccupation)) return null;
        return $"A moment ago: {ProseWords.Paragraph(f.Preoccupation.Trim())}";
    }

    /// <summary>
    /// What they are talking about. Present tense, because unlike the preoccupation
    /// this has not been interrupted by anything.
    /// </summary>
    static string About(SceneFacts f)
    {
        if (string.IsNullOrWhiteSpace(f.Topic)) return null;

        // Reported, not slotted. The topic is a whole sentence that opens with a name
        // — "Jesse said something about the debt his sister never repaid to Charon" —
        // so "talking about {Mid(topic)}" lowercases the name and buries the verb.
        // Exactly the bug the preoccupation had, which is why it reads the same way.
        return $"Just now: {ProseWords.Paragraph(f.Topic.Trim())}";
    }

    /// <summary>
    /// #35's scale gap. Stated as two facts and their coexistence, never as an
    /// instruction to perform the mismatch — that version produced strained metaphor
    /// within the hour and was cut.
    /// </summary>
    static string ScaleGap(SceneFacts f)
    {
        if (string.IsNullOrWhiteSpace(f.Situation) || string.IsNullOrWhiteSpace(f.Concern))
            return null;
        return $"The situation is {ProseWords.Mid(f.Situation)}. " +
               $"{f.PawnName} is thinking about {ProseWords.Mid(f.Concern)}. " +
               $"Both are true at the same time.";
    }

    /// <summary>
    /// What to write. Last, and always present — a scene with no instruction gets
    /// answered with a description of the weather.
    /// </summary>
    static string Instruction(SceneFacts f)
    {
        var alone = (f.Others ?? new List<PersonNote>()).Count == 0;
        var shape = f.Shape == SceneShape.Conversation && alone ? SceneShape.Monologue : f.Shape;
        var ask = Ask(f, shape);

        // #34 lives or dies here rather than in the scene paragraph. Measured, raid
        // fixture, 40 samples per arm, gemma-3-27b-it — how often the pawn actually
        // mentions the thing they were talking about:
        //
        //   scene paragraph only                            0%
        //   scene paragraph + "that conversation is not finished."   20%
        //   scene paragraph + this clause on the instruction        90%
        //
        // The instruction is the sentence closest to the answer and the one the model
        // is obeying; a fact stated earlier in the scene reads as furniture. Adding the
        // unfinishedness to BOTH places also reached 90% and cost variety — repeated
        // openings went from 47% to 62% — so it stays here and only here.
        if (!alone && !string.IsNullOrWhiteSpace(f.Preoccupation))
            ask += " What they were talking about a moment ago comes into it.";

        // #23, and the same lesson: the colony's state stated in the scene gets
        // mentioned 0-18% of the time and pointed at from here 33-72%. Gated on the
        // state actually being pressing, because a clause that fires every time makes
        // every conversation about the food stores.
        //
        // Never on a monologue. One line from someone who is sowing is about the soil,
        // and four different phrasings all failed to move that — including "Three days
        // in, with nothing to eat", which got 0% and cost a third of the variety. The
        // solo case needs something other than a harder instruction; see #23.
        if (shape != SceneShape.Monologue && ProseColonyText.IsPressing(f.Colony))
            ask += " How this place is doing comes into it.";

        return ask;
    }

    static string Ask(SceneFacts f, SceneShape shape)
    {
        switch (shape)
        {
            case SceneShape.Monologue:
                return $"Give one thing {f.PawnName} says out loud.";
            case SceneShape.Urgent:
                return $"Give three to six short turns, {f.PawnName} first. There is no time to be careful.";
            case SceneShape.UrgentAfraid:
                return $"Give three to six short turns, {f.PawnName} first. {f.PawnName} is frightened and in charge of nothing.";
            case SceneShape.ReplyToPlayer:
                return $"{Said(f)} Give {f.PawnName}'s reply, and nothing after it.";
            case SceneShape.ReplyToPlayerMulti:
                return $"{Said(f)} Carry on from there, {f.PawnName} first.";
            default:
                return $"Give the opening of a conversation between them — four to eight short turns, {f.PawnName} first.";
        }
    }

    static string Said(SceneFacts f)
    {
        var who = string.IsNullOrWhiteSpace(f.OtherName) ? "Someone" : f.OtherName;
        var line = (f.PlayerLine ?? "").Trim();
        return $"{who} just said: \"{line}\"";
    }

    /// <summary>
    /// The anti-repeat block. Last, because it is a constraint on the answer and not
    /// part of the scene — and because the prototype that was measured in the prompt
    /// lab put it there.
    ///
    /// "Do not repeat these", never "be original". Forbidding the obvious line pushes
    /// the model toward the unobvious one, and over hundreds of generations that
    /// drifts into strain — the same failure mode as the old "let that mismatch show".
    /// A short explicit ban list has a floor; an instruction to be interesting does not.
    /// </summary>
    static string Recent(SceneFacts f)
    {
        var lines = (f.RecentLines ?? new List<string>())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => l.Trim())
            .Distinct()
            .Take(MaxRecentLines)
            .ToList();
        if (lines.Count == 0) return null;

        var names = new List<string> { f.PawnName };
        names.AddRange((f.Others ?? new List<PersonNote>()).Where(p => p != null).Select(p => p.Name));
        var who = ProseWords.Join(names.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList());

        // "said", not "has said" / "have said", so the number of speakers cannot make
        // the sentence ungrammatical.
        return $"{who} said these recently. Do not repeat them, and do not reach for " +
               $"the same shape:\n" + string.Join("\n", lines.Select(l => "  " + l));
    }

    /// <summary>
    /// Five. Enough to cover the attractor the model keeps falling into, short enough
    /// that the ban list cannot become the bulk of the prompt. ~30 tokens.
    /// </summary>
    public const int MaxRecentLines = 5;

    static string Others(SceneFacts f)
    {
        var near = (f.Others ?? new List<PersonNote>())
            .Where(p => p != null)
            .Select(p =>
            {
                var danger = p.InDanger ? ", in trouble" : "";
                return string.IsNullOrWhiteSpace(p.Activity)
                    ? $"{p.Name} is here{danger}"
                    : $"{p.Name} is {ProseWords.Mid(p.Activity)}{danger}";
            })
            .Take(3).ToList();
        return near.Count == 0 ? null : ProseWords.Cap(ProseWords.Join(near)) + ".";
    }
}
