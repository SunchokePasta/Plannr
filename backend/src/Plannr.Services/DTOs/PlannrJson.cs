using System.Text.Json;
using System.Text.Json.Serialization;

namespace Plannr.Services.DTOs;

/// <summary>
/// How Plannr's programs write JSON to each other and to the phone:
/// ASP.NET Core's web defaults (camelCase names), plus enums as camelCase
/// names ("webPage", "waiting") rather than numbers. Refit, which the
/// worker sends with, already writes enums that way; without this the API
/// refused every result the worker sent (issue #34). Names also survive
/// reordering an enum, where numbers would silently change meaning.
/// </summary>
public static class PlannrJson
{
    /// <summary>Applies Plannr's rules to options that already have the web defaults (ASP.NET Core's).</summary>
    public static void Configure(JsonSerializerOptions options) =>
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
}
