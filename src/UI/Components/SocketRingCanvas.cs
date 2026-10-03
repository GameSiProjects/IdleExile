using Godot;

namespace IdleExile.UI.Components;

/// <summary>
/// Custom canvas for drawing the celestial arc and glowing runic energy conduits
/// connecting the Active Skill Gem socket to the 5 surrounding Support Gem sockets.
/// </summary>
public partial class SocketRingCanvas : Control
{
    public GemView? ParentView { get; set; }

    public override void _Draw()
    {
        ParentView?.DrawSocketLinks(this);
    }
}
