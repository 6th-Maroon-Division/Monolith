using Content.Client.Computer;
using Content.Client.UserInterface.Controls;
using Content.Shared.ProgrammableComputer;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client.ProgrammableComputer;

public sealed class ProgrammableComputerDeviceWindow : FancyWindow
{
    private ComputerBoundUserInterfaceBase? _bui;

    private readonly BoxContainer _linkedDeviceList;

    public ProgrammableComputerDeviceWindow()
    {
        Title = Loc.GetString("programmable-computer-devices-title");

        var mainBox = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6, Margin = new Thickness(8) };

        // Linked devices header
        mainBox.AddChild(new Label { Text = Loc.GetString("programmable-computer-devices-linked-header"), StyleClasses = { "LabelHeading" } });

        // Linked devices scroll + list
        var linkedScroll = new ScrollContainer { VerticalExpand = true, MinHeight = 140 };
        _linkedDeviceList = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 3 };
        linkedScroll.AddChild(_linkedDeviceList);
        mainBox.AddChild(linkedScroll);

        AddChild(mainBox);
    }

    public void SetupWindow(ComputerBoundUserInterfaceBase bui)
    {
        _bui = bui;
    }

    public void UpdateState(ProgrammableComputerBoundUserInterfaceState state)
    {
        RebuildLinkedList(state.AtmosLinks);
    }

    // ─── Linked devices panel ────────────────────────────────────────────────

    private void RebuildLinkedList(AtmosLinkEntry[] links)
    {
        _linkedDeviceList.RemoveAllChildren();

        if (links.Length == 0)
        {
            _linkedDeviceList.AddChild(new Label
            {
                Text = Loc.GetString("programmable-computer-devices-none"),
                FontColorOverride = Color.FromHex("#888888"),
            });
            return;
        }

        foreach (var entry in links)
            _linkedDeviceList.AddChild(BuildLinkedRow(entry));
    }

    private Control BuildLinkedRow(AtmosLinkEntry entry)
    {
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };

        var labelEdit = new LineEdit
        {
            Text = entry.Label,
            MinWidth = 120,
            HorizontalExpand = true,
            PlaceHolder = Loc.GetString("programmable-computer-devices-label-placeholder"),
        };

        var typeLabel = new Label
        {
            Text = $"[{entry.DeviceType}] {entry.EntityName}",
            MinWidth = 140,
            FontColorOverride = Color.FromHex("#aaaaaa"),
        };

        var addrLabel = new Label
        {
            Text = entry.Address,
            FontColorOverride = Color.FromHex("#666666"),
            MinWidth = 60,
        };

        var renameBtn = new Button { Text = Loc.GetString("programmable-computer-devices-rename"), MinWidth = 60 };
        renameBtn.OnPressed += _ =>
        {
            var newLabel = labelEdit.Text.Trim();
            if (newLabel.Length > 0 && newLabel != entry.Label)
                _bui?.SendMessage(new ProgrammableComputerAtmosRenameMessage(entry.Label, newLabel));
        };

        var unlinkBtn = new Button { Text = Loc.GetString("programmable-computer-devices-unlink"), MinWidth = 60 };
        unlinkBtn.OnPressed += _ => _bui?.SendMessage(new ProgrammableComputerAtmosUnlinkMessage(entry.Label));

        row.AddChild(labelEdit);
        row.AddChild(typeLabel);
        row.AddChild(addrLabel);
        row.AddChild(renameBtn);
        row.AddChild(unlinkBtn);
        return row;
    }
}
