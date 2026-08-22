using System.Runtime.Serialization;

namespace RimTalk.Data;

/// <summary>One proposed goal, as the model returns it. rim-universe #28.</summary>
[DataContract]
public class GoalData : IJsonData
{
    /// <summary>Must be one of the three kinds the pawn was shown. Never trusted.</summary>
    [DataMember(Name = "kind")] public string Kind { get; set; }

    [DataMember(Name = "goal")] public string Goal { get; set; }

    public string GetText() => Goal;
}
