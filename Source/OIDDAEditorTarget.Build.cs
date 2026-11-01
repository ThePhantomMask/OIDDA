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

        // Optional: network support. Keep this line: the module does nothing until "Enable Network" is turned on in the
        // OIDDA settings (Network Config), so turning networking on or off never needs a code change.
        // For a game that never needs networking, you can remove this line and the OIDDANetwork folder (the base OIDDA module does not depend on it).
        Modules.Add("OIDDANetwork");
    }
}