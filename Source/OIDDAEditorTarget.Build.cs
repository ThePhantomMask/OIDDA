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
    }
}