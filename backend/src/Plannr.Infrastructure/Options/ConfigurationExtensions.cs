using Microsoft.Extensions.Configuration;
using Plannr.Core.Options;

namespace Plannr.Infrastructure.Options;

public static class ConfigurationExtensions
{
    /// <summary>
    /// Binds <paramref name="sectionName"/> to a <typeparamref name="T"/> and
    /// validates it in one step, so no Program.cs can bind a section and
    /// forget to check it. A missing section gives the class's defaults
    /// (still validated): whether that's acceptable is Validate's call, not
    /// the binder's.
    /// </summary>
    public static T GetValidated<T>(this IConfiguration configuration, string sectionName)
        where T : AppOptionsBase, new()
    {
        var options = configuration.GetSection(sectionName).Get<T>() ?? new T();
        options.Validate();
        return options;
    }
}
