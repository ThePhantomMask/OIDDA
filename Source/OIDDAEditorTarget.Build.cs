using Flax.Build;

public class OIDDAEditorTarget : GameProjectEditorTarget
{
    /// <inheritdoc />
    public override void Init()
    {
        base.Init();

        // Reference the modules for editor
        Modules.Add("OIDDA");
        Modules.Add("OIDDAEditor");

        // Optional: network support. Remove this line (and the OIDDANetwork folder) for a game without networking.
        Modules.Add("OIDDANetwork");
    }
}