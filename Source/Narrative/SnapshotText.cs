using System.Collections.Generic;
using System.Text;

namespace RimTalk.Narrative;

/// <summary>
/// A minimal JSON writer for the run snapshot. rim-universe S169.
///
/// Hand-rolled rather than DataContract, and that is a deliberate reaction to the
/// bug that opened this session: DataContractJsonSerializer round-trips through a
/// "sanitizer" that spent months silently welding string arrays into one string.
/// This file writes and never reads, it is forty lines, and every one of them is
/// covered — which is a better trade than reusing the thing that already lied once.
///
/// Escaping is the whole risk. A colonist called "O'Brien" is fine; a generated line
/// containing a quote or a newline is not, and both appear constantly.
/// </summary>
public static class SnapshotText
{
    public static string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";

        var sb = new StringBuilder(s.Length + 8);
        foreach (var c in s)
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    // Control characters are illegal raw in a JSON string and a model
                    // will emit one eventually.
                    if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                    break;
            }

        return sb.ToString();
    }

    public static string Str(string s) => s == null ? "null" : $"\"{Escape(s)}\"";

    public static string Field(string name, string value) => $"{Str(name)}:{Str(value)}";

    public static string Field(string name, int value) => $"{Str(name)}:{value}";

    public static string Obj(params string[] fields) => "{" + string.Join(",", fields) + "}";

    public static string Arr(IEnumerable<string> items) => "[" + string.Join(",", items) + "]";

    public static string ArrOfStrings(IEnumerable<string> items)
    {
        var quoted = new List<string>();
        foreach (var i in items ?? new List<string>()) quoted.Add(Str(i));
        return Arr(quoted);
    }
}
