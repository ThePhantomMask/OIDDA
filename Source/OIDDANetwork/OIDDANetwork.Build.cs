using Flax.Build;
using Flax.Build.NativeCpp;

/// <summary>
/// Optional network support for OIDDA: replicates the pacing state to clients and lets them report metrics.
/// Needs the engine networking module; the base OIDDA module does not depend on it.
/// </summary>
public class OIDDANetwork : GameModule
{
    /// <inheritdoc />
    public override void Setup(BuildOptions options)
    {
        base.Setup(options);

        options.ScriptingAPI.IgnoreMissingDocumentationWarnings = true;

        options.PublicDependencies.Add("OIDDA");

        // Network RPCs are only generated for modules carrying this tag.
        Tags["Network"] = string.Empty;
        options.PublicDependencies.Add("Networking");

        BuildNativeCode = false;
    }
}
