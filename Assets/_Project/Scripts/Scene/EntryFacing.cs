/// <summary>
/// Per-destination-gate override for the hero's facing when entering a new scene.
/// </summary>
/// <remarks>
/// <para><c>None</c> does NOT carry the outgoing hero's facing across scenes. The new
/// scene's hero is freshly instantiated with its blackboard default
/// (<c>facingRight = true</c>), so <c>None</c> effectively means "use whatever the
/// destination hero's default / current facing is."</para>
/// <para>If a destination gate needs a specific facing (most do, especially Bottom
/// gates which use facing to pick the diagonal-throw direction), pick
/// <c>ForceRight</c> or <c>ForceLeft</c> explicitly.</para>
/// </remarks>
public enum EntryFacing
{
    /// <summary>Do not override facing. Hero arrives with the destination hero's default facing.</summary>
    None = 0,

    /// <summary>Force facingRight = true before entry motion starts.</summary>
    ForceRight,

    /// <summary>Force facingRight = false before entry motion starts.</summary>
    ForceLeft
}
