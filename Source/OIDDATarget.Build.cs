using Flax.Build;

public class OIDDATarget : GameProjectTarget
{
    /// <inheritdoc />
    public override void Init()
    {
        base.Init();

        // Reference the modules for game
        Modules.Add("OIDDA");
    }
}