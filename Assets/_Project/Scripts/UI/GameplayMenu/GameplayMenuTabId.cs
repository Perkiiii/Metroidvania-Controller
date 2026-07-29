/// <summary>
/// Stable identity for the five confirmed Gameplay Menu tabs. The declaration order IS the
/// authored left-to-right/top-to-bottom presentation order and the Previous/Next cycling order;
/// <see cref="GameplayMenuScreen"/> and UIFoundationValidator both assert registrations match it.
///
/// Visibility is fixed: all five tabs are always present and reachable in production. A tab whose
/// gameplay owner does not exist yet shows an authored empty state — it is never hidden, disabled,
/// or filled with invented data. See Docs/FeatureSpecs/GameplayMenu.md.
/// </summary>
public enum GameplayMenuTabId
{
    Gear = 0,
    CombatLoadout = 1,
    Satchel = 2,
    FieldNotes = 3,
    Map = 4
}
