using Flax.Build;
using Flax.Build.NativeCpp;

public class OIDDA : GameModule
{
    /// <inheritdoc />
    public override void Setup(BuildOptions options)
    {
        base.Setup(options);

        options.ScriptingAPI.IgnoreMissingDocumentationWarnings = true;

        BuildNativeCode = false;
    }
}