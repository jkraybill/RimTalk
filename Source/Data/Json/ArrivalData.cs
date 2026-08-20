using System.Runtime.Serialization;

namespace RimTalk.Data;

/// <summary>One arrival log entry, as the model returns it. rim-universe #37.</summary>
[DataContract]
public class ArrivalData : IJsonData
{
    [DataMember(Name = "log")] public string Log { get; set; }

    public string GetText() => Log;
}
