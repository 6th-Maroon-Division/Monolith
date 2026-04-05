using Content.Client.UserInterface.Controls;
using Content.Shared.ProgrammableComputer;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.ProgrammableComputer;

public sealed class ProgrammableComputerLinkerWindow : FancyWindow
{
    private readonly BoxContainer _list;
    private readonly Label _countLabel;

    public event Action? OnClearAll;
    public event Action<NetEntity>? OnRemoveOne;

    public ProgrammableComputerLinkerWindow()
    {
        Title = Loc.GetString("programmable-computer-linker-ui-title");

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(8)
        };

        _countLabel = new Label
        {
            Text = Loc.GetString("programmable-computer-linker-ui-count", ("count", 0))
        };
        root.AddChild(_countLabel);

        var scroll = new ScrollContainer { VerticalExpand = true, MinHeight = 180 };
        _list = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 3
        };
        scroll.AddChild(_list);
        root.AddChild(scroll);

        var clear = new Button { Text = Loc.GetString("programmable-computer-linker-ui-clear-all") };
        clear.OnPressed += _ => OnClearAll?.Invoke();
        root.AddChild(clear);

        AddChild(root);
    }

    public void UpdateState(ProgrammableComputerLinkerUiState state)
    {
        _countLabel.Text = Loc.GetString("programmable-computer-linker-ui-count", ("count", state.Buffered.Length));
        _list.RemoveAllChildren();

        if (state.Buffered.Length == 0)
        {
            _list.AddChild(new Label { Text = Loc.GetString("programmable-computer-linker-ui-empty") });
            return;
        }

        foreach (var entry in state.Buffered)
        {
            var row = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 4
            };

            var info = new Label
            {
                HorizontalExpand = true,
                Text = $"[{entry.DeviceType}] {entry.Name} ({entry.Address})"
            };

            var remove = new Button { Text = Loc.GetString("programmable-computer-linker-ui-remove") };
            var target = entry.Target;
            remove.OnPressed += _ => OnRemoveOne?.Invoke(target);

            row.AddChild(info);
            row.AddChild(remove);
            _list.AddChild(row);
        }
    }
}
