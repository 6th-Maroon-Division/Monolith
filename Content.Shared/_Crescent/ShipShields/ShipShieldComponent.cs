namespace Content.Shared._Crescent.ShipShields;

[RegisterComponent, UnsavedComponent]
public sealed partial class ShipShieldComponent : Component
{
    public EntityUid? Source;
    public EntityUid Shielded;
}
