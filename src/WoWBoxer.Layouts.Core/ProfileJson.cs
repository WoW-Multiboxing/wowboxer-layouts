using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WoWBoxer.Layouts.Core;

/// <summary>Reads and writes <c>wowboxer-layout/1</c> JSON and <c>WBX1:</c> share codes.</summary>
public static class ProfileJson
{
    public const string SharePrefix = "WBX1:";

    private static readonly JsonSerializerOptions Indented = Create(writeIndented: true);
    private static readonly JsonSerializerOptions Compact = Create(writeIndented: false);

    private static JsonSerializerOptions Create(bool writeIndented) => new(JsonSerializerDefaults.Web)
    {
        WriteIndented = writeIndented,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower) },
    };

    public static string Serialize(LayoutProfile profile, bool indented = true) =>
        JsonSerializer.Serialize(profile, indented ? Indented : Compact);

    /// <summary>Parses and validates a profile. Throws <see cref="FormatException"/> with a readable message.</summary>
    public static LayoutProfile Deserialize(string json)
    {
        LayoutProfile? profile;
        try
        {
            profile = JsonSerializer.Deserialize<LayoutProfile>(json, Indented);
        }
        catch (JsonException e)
        {
            throw new FormatException($"Not a valid layout file: {e.Message}", e);
        }
        if (profile is null) throw new FormatException("Not a valid layout file: empty.");

        var errors = ProfileValidator.Validate(profile);
        if (errors.Count > 0) throw new FormatException("Invalid layout: " + string.Join(" ", errors));
        return profile;
    }

    public static string ToShareCode(LayoutProfile profile) =>
        SharePrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(Serialize(profile, indented: false)));

    public static LayoutProfile FromShareCode(string code)
    {
        code = code.Trim();
        if (!code.StartsWith(SharePrefix, StringComparison.OrdinalIgnoreCase))
            throw new FormatException($"A layout code starts with \"{SharePrefix}\".");
        var base64 = new string(code[SharePrefix.Length..].Where(c => !char.IsWhiteSpace(c)).ToArray());
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64);
        }
        catch (FormatException e)
        {
            throw new FormatException("The layout code is damaged (not base64).", e);
        }
        return Deserialize(Encoding.UTF8.GetString(bytes));
    }
}

public static class ProfileValidator
{
    private const double Tolerance = 0.01;

    public static IReadOnlyList<string> Validate(LayoutProfile profile)
    {
        var errors = new List<string>();
        if (!profile.Format.StartsWith("wowboxer-layout/1", StringComparison.Ordinal))
            errors.Add($"Unknown format \"{profile.Format}\".");
        if (profile.Slots.Count == 0)
            errors.Add("The layout has no slots.");
        if (profile.Clients != profile.Slots.Count)
            errors.Add($"\"clients\" is {profile.Clients} but there are {profile.Slots.Count} slots.");

        var numbers = profile.Slots.Select(s => s.N).OrderBy(n => n).ToList();
        if (!numbers.SequenceEqual(Enumerable.Range(1, profile.Slots.Count)))
            errors.Add("Slot numbers must be 1, 2, 3 ... without gaps or repeats.");

        foreach (var s in profile.Slots)
        {
            if (s.W <= 0 || s.H <= 0)
                errors.Add($"Slot {s.N} has no size.");
            if (s.X < -Tolerance || s.Y < -Tolerance || s.X + s.W > 100 + Tolerance || s.Y + s.H > 100 + Tolerance)
                errors.Add($"Slot {s.N} is outside its monitor (x, y, w, h are % of the work area).");
        }
        if (profile.Options.MinAspect is < 0 or > 4)
            errors.Add("\"minAspect\" must be between 0 and 4.");
        return errors;
    }
}
