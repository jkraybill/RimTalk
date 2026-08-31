using System.Collections.Generic;
using System.Runtime.Serialization;

namespace RimTalk.Data;

/// <summary>
/// LLM response for personality expansion. rim-universe #51.
/// </summary>
[DataContract]
public class PersonalityExpansionData : IJsonData
{
    [DataMember(Name = "narrative")]
    public string Narrative { get; set; }

    [DataMember(Name = "speechStyle")]
    public string SpeechStyle { get; set; }

    [DataMember(Name = "quirks")]
    public List<string> Quirks { get; set; }

    [DataMember(Name = "topics")]
    public List<string> Topics { get; set; }

    [DataMember(Name = "lovedFood")]
    public string LovedFood { get; set; }

    [DataMember(Name = "hatedFood")]
    public string HatedFood { get; set; }

    [DataMember(Name = "lovedAnimal")]
    public string LovedAnimal { get; set; }

    [DataMember(Name = "hatedAnimal")]
    public string HatedAnimal { get; set; }

    [DataMember(Name = "lovedMaterial")]
    public string LovedMaterial { get; set; }

    [DataMember(Name = "lovedRitual")]
    public string LovedRitual { get; set; }

    [DataMember(Name = "hatedRitual")]
    public string HatedRitual { get; set; }

    public string GetText() => Narrative;
}
