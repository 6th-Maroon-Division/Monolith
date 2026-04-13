namespace Content.Shared._Crescent.ShipShields;

[RegisterComponent, UnsavedComponent]
public sealed partial class ShipShieldedComponent : Component
{
    public EntityUid Shield;
    public EntityUid? Source;
}
