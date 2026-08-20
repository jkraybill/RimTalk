using System.Collections.Generic;
using System.Runtime.Serialization;

namespace RimTalk.Data;

/// <summary>A pawn's back-pocket topics, as the model returns them. rim-universe #44.</summary>
[DataContract]
public class TopicData : IJsonData
{
    [DataMember(Name = "topics")] public List<string> Topics { get; set; }

    public string GetText() => Topics == null ? "" : string.Join("; ", Topics);
}
