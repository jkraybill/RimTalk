using System.Collections.Generic;
using System.Linq;
using RimTalk.Prose;

namespace RimTalk.Goals;

/// <summary>What a goal is about. Closed, and closed on purpose.</summary>
public enum GoalKind
{
    FoodSecurity,
    Medicine,
    Shelter,
    Power,
    BaseDefence,
    Companionship,
}

public enum GoalState
{
    Active,
    Met,
    Expired,
    /// <summary>Set aside — the pawn died, left, or the goal stopped making sense.</summary>
    Abandoned,
}

/// <summary>
/// rim-universe #28, the half that decides whether a goal ever resolves.
///
/// The issue names its own crux and it is not the generation:
///
///   "A goal must resolve, or it is worse than nothing. An unresolvable goal is a
///    pawn who repeats themselves for a hundred days."
///
/// It predicts three ways that fails, and each one is answered by a structural
/// choice here rather than by a prompt:
///
///   nags forever          -> a goal has an EXPIRY, and expiring is a normal outcome
///   completes on asking   -> the model never judges completion; a predicate does
///   a single sandbag      -> the GAME sets the target from current state, so a goal
///                            is born a real step above where the colony already is
///
/// And a fourth the issue does not name, which this project keeps meeting: a
/// predicate that cannot be READ must never read as satisfied. `FoodDays` is -1 on a
/// map mid-generation, and a goal that completes because nobody could check it is
/// the worst of the four — it grants the mood, prints the letter and means nothing.
///
/// No RimWorld types: source-linked into the test project and run for real.
/// </summary>
public static class GoalMath
{
    /// <summary>
    /// Ten days. The issue puts a goal at "days-weeks", between an arc (quadrums) and
    /// a mood (hours). Long enough that reaching it takes actual play, short enough
    /// that a pawn is not carrying the same sentence through a whole quadrum.
    /// </summary>
    public const int SpanTicks = 600_000;

    /// <summary>
    /// Five days after one resolves before the same KIND can be set again. Without
    /// it a pawn who just got a week of food immediately wants two, which is the
    /// needs-bar reading of a goal rather than the person reading.
    /// </summary>
    public const int CooldownTicks = 300_000;

    /// <summary>
    /// Three. A person choosing between three things is choosing; a list of nine is a
    /// needs bar with prose on top.
    /// </summary>
    public const int MaxCandidates = 3;

    /// <summary>
    /// What is actually wrong here, worst first.
    ///
    /// Empty when the colony is fine, and that is the point rather than a gap: the
    /// issue's own mitigation for taxing the player's strategy is that goals come from
    /// REAL deficiencies. A colony with nothing wrong should produce no goal at all
    /// instead of inventing one, and a pawn who has nothing to want is a pawn who says
    /// something else.
    ///
    /// Unknown state never scores. -1 means nobody could read it, not that it is zero.
    /// </summary>
    public static List<GoalKind> Candidates(ColonyFacts c)
    {
        if (c == null) return new List<GoalKind>();

        var scored = new List<(GoalKind Kind, float Want)>();

        // ONE gate. Want is "how far short", roughly 0..1 so the kinds are comparable,
        // and anything at or below zero is not a deficiency and never reaches the list.
        //
        // The first version of this had a threshold on each guard AND the floor below,
        // which is two places deciding the same thing — and a sabotage run proved the
        // floor unreachable, because every guard already implied it. Two gates that
        // agree are one gate and one decoration, and the decoration is the one the
        // next reader trusts.
        void Add(GoalKind k, float want) { if (want > 0f) scored.Add((k, want)); }

        // Unknown state scores zero, in every branch. -1 means nobody could read it,
        // not that it is empty, and a goal set from an unreadable colony is a goal
        // nobody can resolve.
        Add(GoalKind.FoodSecurity,
            c.FoodDays < 0f ? 0f : 1f - c.FoodDays / FoodComfortable);

        Add(GoalKind.Medicine,
            c.MedicineCount < 0 ? 0f : 1f - c.MedicineCount / (float)MedicineComfortable);

        Add(GoalKind.Shelter,
            c.Colonists <= 0 ? 0f : c.ColonistsWithoutBed / (float)c.Colonists);

        // Three-valued on purpose. A tribal start never had electricity and must not
        // be told to go and fix the power — #23 made this nullable for this reason.
        Add(GoalKind.Power, c.HasPower == false ? PowerWant : 0f);

        Add(GoalKind.BaseDefence,
            c.Colonists <= 0 || c.Turrets < 0 ? 0f : 1f - c.Turrets / (float)c.Colonists);

        // JK's playstyle. A pawn alone for fifty days wanting anyone else to exist is
        // the most natural goal in a solo landing and the most affecting when it
        // finally resolves — so it survives a colony that is otherwise comfortable.
        Add(GoalKind.Companionship,
            c.Colonists <= 0 ? 0f : 1f - c.Colonists / (float)CompanyEnough);

        return scored.OrderByDescending(s => s.Want).Take(MaxCandidates).Select(s => s.Kind).ToList();
    }

    /// <summary>
    /// Losing the power outranks thin medicine and loses to starving. It is a step
    /// change rather than a shortfall, so it has no natural 0..1 reading and this is
    /// a placed number rather than a derived one.
    /// </summary>
    public const float PowerWant = 0.8f;

    /// <summary>Ten days of food is the band ProseColonyText already calls "about a week".</summary>
    public const float FoodComfortable = 10f;
    public const int MedicineComfortable = 10;

    /// <summary>Three. Enough that somebody is around when something goes wrong.</summary>
    public const int CompanyEnough = 3;

    /// <summary>
    /// The number the predicate tests against, derived from where the colony IS.
    ///
    /// This is the answer to "satisfied by a single sandbag" and it is deliberately
    /// not the model's job. A target the model proposes is a number chosen to sound
    /// reasonable; a target derived from current state is a real step, and it cannot
    /// be born already met.
    /// </summary>
    public static float Target(GoalKind kind, ColonyFacts c)
    {
        c ??= new ColonyFacts();
        return kind switch
        {
            GoalKind.FoodSecurity  => System.Math.Max(FoodComfortable, Known(c.FoodDays) + FoodStep),
            GoalKind.Medicine      => System.Math.Max(MedicineComfortable, Known(c.MedicineCount) + MedicineStep),
            GoalKind.Shelter       => 0f,                                     // nobody without a bed
            GoalKind.Power         => 1f,                                     // the lights back on
            GoalKind.BaseDefence   => System.Math.Max(c.Colonists, Known(c.Turrets) + 1),
            GoalKind.Companionship => System.Math.Max(CompanyEnough, Known(c.Colonists) + 1),
            _ => 0f,
        };
    }

    public const float FoodStep = 5f;
    public const int MedicineStep = 5;

    /// <summary>-1 is "nobody could read it", which is not a quantity to add to.</summary>
    static float Known(float v) => v < 0f ? 0f : v;

    /// <summary>
    /// Whether the game says so. Never the model — the issue's second predicted
    /// failure is a goal that completes because the model is agreeable.
    ///
    /// Unreadable state is FALSE, in every branch. A goal that completes because
    /// nobody could check it grants the mood, prints the letter and means nothing.
    /// </summary>
    public static bool IsMet(GoalKind kind, float target, ColonyFacts c)
    {
        if (c == null) return false;
        return kind switch
        {
            GoalKind.FoodSecurity  => c.FoodDays >= 0f && c.FoodDays >= target,
            GoalKind.Medicine      => c.MedicineCount >= 0 && c.MedicineCount >= target,
            GoalKind.Shelter       => c.Colonists > 0 && c.ColonistsWithoutBed == 0,
            GoalKind.Power         => c.HasPower == true,
            GoalKind.BaseDefence   => c.Turrets >= 0 && c.Turrets >= target,
            GoalKind.Companionship => c.Colonists > 0 && c.Colonists >= target,
            _ => false,
        };
    }

    /// <summary>
    /// Where a goal stands now.
    ///
    /// Met outranks the clock: a goal reached on its last day was reached. Met and
    /// Abandoned are both terminal — food goes up and down every quadrum, and a goal
    /// that un-meets is a pawn who celebrates and then takes it back, after the mood
    /// has already been granted.
    /// </summary>
    public static GoalState Evaluate(GoalKind kind, float target, int expiryTick, int now,
                                     ColonyFacts c, GoalState current)
    {
        if (current != GoalState.Active) return current;
        if (IsMet(kind, target, c)) return GoalState.Met;
        // Still expires when the colony cannot be read, or an unreadable predicate
        // becomes "nags forever" by a different door.
        if (now >= expiryTick) return GoalState.Expired;
        return GoalState.Active;
    }

    /// <summary>
    /// Mutual exclusivity, colony-wide: one pawn per kind. S170.
    ///
    /// If somebody already carries FoodSecurity, nobody else is offered it until
    /// theirs resolves — otherwise the whole colony chants the same goal, which is
    /// the sameness #28 exists to remove, arrived at from the other direction.
    ///
    /// Empty out is a real answer and the caller returns without an API call.
    /// Order is the deficiency ranking from Candidates() and survives.
    /// </summary>
    public static List<GoalKind> Exclusive(IEnumerable<GoalKind> shortlist,
                                           IEnumerable<GoalKind> activeKinds)
    {
        if (shortlist == null) return new List<GoalKind>();
        if (activeKinds == null) return shortlist.ToList();
        var taken = activeKinds.ToHashSet();
        return shortlist.Where(k => !taken.Contains(k)).ToList();
    }

    /// <summary>
    /// Whether these work assignments qualify a pawn for this kind. #52.
    ///
    /// A cook owns food security; a doctor owns medicine; a constructor owns shelter.
    /// Takes the enabled WorkTypeDef names rather than a Pawn so the mapping is
    /// testable — DefDatabase lookup stays at the call site.
    /// </summary>
    public static bool Qualifies(GoalKind kind, IEnumerable<string> activeWorkTypes)
    {
        if (activeWorkTypes == null) return false;
        var w = activeWorkTypes.ToHashSet();
        bool Has(string d) => w.Contains(d);

        return kind switch
        {
            GoalKind.FoodSecurity  => Has("Cooking") || Has("Growing") || Has("Hunting"),
            GoalKind.Medicine      => Has("Doctor"),
            GoalKind.Shelter       => Has("Construction"),
            GoalKind.Power         => Has("Research") || Has("Construction"),
            GoalKind.BaseDefence   => Has("Hunting") || Has("Construction"),
            GoalKind.Companionship => Has("Warden") || Has("Handling"),
            _ => false,
        };
    }

    /// <summary>
    /// Filter a shortlist to what this pawn's jobs qualify them for.
    ///
    /// null activeWorkTypes is "could not be read", NOT "nothing enabled", and passes
    /// the shortlist through untouched — a pawn whose assignments are unreadable must
    /// not silently stop wanting things. An EMPTY set is a real answer and keeps
    /// nothing. The pre-S171 pair answered these two states inconsistently: the
    /// filter kept everything on a null workSettings while the per-kind check kept
    /// nothing, two readings of one state inside one pipeline.
    /// </summary>
    public static List<GoalKind> ByJob(IEnumerable<GoalKind> candidates,
                                       IEnumerable<string> activeWorkTypes)
    {
        if (candidates == null) return new List<GoalKind>();
        if (activeWorkTypes == null) return candidates.ToList();
        var w = activeWorkTypes.ToHashSet();
        return candidates.Where(k => Qualifies(k, w)).ToList();
    }

    /// <summary>
    /// The tooltip's "Success:" line, with progress where a number means something.
    /// S170. This is the player's only answer to "why has this not completed?".
    /// </summary>
    public static string Criteria(GoalKind kind, float target, ColonyFacts c)
    {
        // Both halves round AWAY from claiming success, and they round in opposite
        // directions to do it. S170 used `{v:0}` on both, which rounds to nearest,
        // and that produced a tooltip reading "Progress: 3/3" on a goal IsMet says is
        // not met — FoodDays 2.5 printed as 3, and a target of 19.4 printed as 19.
        // The player is then staring at a completed goal that never completes, with
        // the tooltip that exists to answer exactly that question telling them a lie.
        //
        // floor(current) >= ceil(target) implies current >= target, always. So a
        // readout of N/N is now a fact about the predicate rather than about
        // formatting. The cost is the honest direction: a met goal can briefly read
        // 14/15, and it resolves that night anyway.
        var shownTarget = Ceil(target);

        var text = kind switch
        {
            GoalKind.FoodSecurity  => $"Have {shownTarget}+ days of food stockpiled",
            GoalKind.Medicine      => $"Have {shownTarget}+ medicine in storage",
            GoalKind.Shelter       => "Everyone has a bed",
            GoalKind.Power         => "Colony power grid is online",
            GoalKind.BaseDefence   => $"Have {shownTarget}+ defensive positions",
            GoalKind.Companionship => $"Have {shownTarget}+ colonists",
            _ => "Unknown",
        };

        var (current, show) = Progress(kind, c);
        if (show && current >= 0) text += $" (Progress: {Floor(current)}/{shownTarget})";
        return text;
    }

    /// <summary>
    /// What actually satisfied the goal, for the completion letter.
    ///
    /// Same Ceil as Criteria, and that is the whole point of it living here: the
    /// letter and the tooltip are two renderings of ONE target and must print one
    /// number. They did not. GoalService held a third copy of this wording with
    /// {target:0}, so a goal shown as "Have 20+ defensive positions" completed with
    /// a letter reading "now has 19+" — the same float, two bars, and nothing in a
    /// green suite to notice.
    /// </summary>
    public static string Achievement(GoalKind kind, float target)
    {
        var t = Ceil(target);
        return kind switch
        {
            GoalKind.FoodSecurity  => $"The colony now has {t}+ days of food secured.",
            GoalKind.Medicine      => $"The colony now has {t}+ medicine stockpiled.",
            GoalKind.Shelter       => "Everyone in the colony now has a bed.",
            GoalKind.Power         => "The colony's power grid is back online.",
            GoalKind.BaseDefence   => $"The colony now has {t}+ defensive positions.",
            GoalKind.Companionship => $"The colony now has {t}+ people.",
            _ => "The goal was achieved.",
        };
    }

    static int Floor(float v) => (int)System.Math.Floor(v);
    static int Ceil(float v) => (int)System.Math.Ceiling(v);

    /// <summary>
    /// Current value and whether it is worth printing. Shelter and Power are states
    /// rather than counts; "Everyone has a bed (Progress: 5/0)" is worse than nothing.
    /// -1 stays -1 so the caller drops it: unknown is not zero.
    /// </summary>
    public static (float Current, bool Show) Progress(GoalKind kind, ColonyFacts c)
    {
        if (c == null) return (-1f, false);
        return kind switch
        {
            GoalKind.FoodSecurity  => (c.FoodDays, true),
            GoalKind.Medicine      => (c.MedicineCount, true),
            GoalKind.Shelter       => (c.Colonists - c.ColonistsWithoutBed, false),
            GoalKind.Power         => (c.HasPower == true ? 1f : 0f, false),
            GoalKind.BaseDefence   => (c.Turrets, true),
            GoalKind.Companionship => (c.Colonists, true),
            _ => (-1f, false),
        };
    }

    /// <summary>Format ticks as "X days" for UI display.</summary>
    public static string ElapsedDays(int ticks)
    {
        var days = ticks / 60000f;
        if (days < 1f) return "less than a day";
        if (days < 2f) return "1 day";
        return $"{(int)days} days";
    }

    /// <summary>Format remaining ticks as "X days" for UI display.</summary>
    public static string RemainingDays(int ticks)
    {
        if (ticks <= 0) return "expired";
        var days = ticks / 60000f;
        if (days < 1f) return "less than a day";
        if (days < 2f) return "1 day";
        return $"{(int)days} days";
    }
}
