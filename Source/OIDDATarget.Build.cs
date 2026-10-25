using Flax.Build;

public class OIDDATarget : GameProjectTarget
{
    /// <inheritdoc />
    public override void Init()
    {
        base.Init();

        // Reference the modules for game
        Modules.Add("OIDDA");

        // Optional: network support. Remove this line (and the OIDDANetwork folder) for a game without networking.
        Modules.Add("OIDDANetwork");
    }
}