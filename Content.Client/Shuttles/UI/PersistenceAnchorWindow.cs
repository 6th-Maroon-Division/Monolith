using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Content.Client.UserInterface.Controls;

namespace Content.Client.Shuttles.UI;

public sealed class PersistenceAnchorWindow : FancyWindow
{
    private readonly Label _anchorLabel;
    private readonly Label _gridLabel;
    private readonly Label _ownerLabel;
    private readonly Label _insertedIdLabel;
    private readonly Label _lockLabel;
    private readonly Button _claimOwnerButton;
    private readonly Button _unlockButton;

    public event Action? ClaimOwnerPressed;
    public event Action? UnlockPressed;

    public PersistenceAnchorWindow()
    {
        Title = Loc.GetString("persistence-anchor-ui-title");
        MinSize = new Vector2(340, 210);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(8),
        };

        _anchorLabel = new Label();
        _gridLabel = new Label();
        _ownerLabel = new Label();
        _insertedIdLabel = new Label();
        _lockLabel = new Label();

        _claimOwnerButton = new Button
        {
            Text = Loc.GetString("persistence-anchor-ui-claim-owner"),
        };
        _claimOwnerButton.OnPressed += _ => ClaimOwnerPressed?.Invoke();

        _unlockButton = new Button
        {
            Text = Loc.GetString("persistence-anchor-ui-unlock-grid"),
        };
        _unlockButton.OnPressed += _ => UnlockPressed?.Invoke();

        root.AddChild(_anchorLabel);
        root.AddChild(_gridLabel);
        root.AddChild(_ownerLabel);
        root.AddChild(_insertedIdLabel);
        root.AddChild(_lockLabel);
        root.AddChild(new Control { MinSize = new Vector2(0, 4) });
        root.AddChild(_claimOwnerButton);
        root.AddChild(_unlockButton);

        ContentsContainer.AddChild(root);
    }

    public void UpdateState(
        string anchorId,
        string gridName,
        string? ownerName,
        string? insertedIdName,
        bool hasInsertedIdCard,
        bool isLocked,
        bool canClaimOwner,
        bool canUnlock)
    {
        _anchorLabel.Text = Loc.GetString("persistence-anchor-ui-anchor-id", ("id", anchorId));
        _gridLabel.Text = Loc.GetString("persistence-anchor-ui-grid-name", ("name", gridName));
        _ownerLabel.Text = Loc.GetString(
            "persistence-anchor-ui-owner",
            ("owner", string.IsNullOrWhiteSpace(ownerName)
                ? Loc.GetString("persistence-anchor-ui-owner-unset")
                : ownerName));
        _insertedIdLabel.Text = Loc.GetString(
            "persistence-anchor-ui-inserted-id",
            ("id", hasInsertedIdCard
                ? insertedIdName ?? Loc.GetString("persistence-anchor-ui-inserted-id-unknown")
                : Loc.GetString("persistence-anchor-ui-inserted-id-none")));
        _lockLabel.Text = Loc.GetString(
            isLocked ? "persistence-anchor-ui-lock-state-locked" : "persistence-anchor-ui-lock-state-unlocked");

        _claimOwnerButton.Disabled = !canClaimOwner || !hasInsertedIdCard;
        _unlockButton.Disabled = !canUnlock;
    }
}
