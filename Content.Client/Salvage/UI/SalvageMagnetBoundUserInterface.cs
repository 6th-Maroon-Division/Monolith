using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Content.Client.Message;
using Content.Shared.Salvage;
using Content.Shared.Salvage.Magnet;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client.Salvage.UI;

public sealed class SalvageMagnetBoundUserInterface : BoundUserInterface
{
    private static readonly Regex IdWordBreakRegex = new("([a-z0-9])([A-Z])|([A-Za-z])([0-9])", RegexOptions.Compiled);

    [Dependency] private readonly IEntityManager _entManager = default!;

    private OfferingWindow? _window;

    public SalvageMagnetBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindowCenteredLeft<OfferingWindow>();
        _window.Title = Loc.GetString("salvage-magnet-window-title");
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not SalvageMagnetBoundUserInterfaceState current || _window == null)
            return;

        _window.ClearOptions();

        var salvageSystem = _entManager.System<SharedSalvageSystem>();
        _window.NextOffer = current.NextOffer;
        _window.Progression = current.EndTime ?? TimeSpan.Zero;
        _window.Claimed = current.EndTime != null;
        _window.Cooldown = current.Cooldown;
        _window.ProgressionCooldown = current.Duration;

        for (var i = 0; i < current.Offers.Count; i++)
        {
            var seed = current.Offers[i];
            var offer = salvageSystem.GetSalvageOffering(seed);
            var option = new OfferingWindowOption();
            option.MinWidth = 210f;
            option.Disabled = current.EndTime != null;
            option.Claimed = current.ActiveSeed == seed;
            var claimIndex = i;

            option.ClaimPressed += _ =>
            {
                SendMessage(new MagnetClaimOfferEvent
                {
                    Index = claimIndex
                });
            };

            switch (offer)
            {
                case AsteroidOffering asteroid:
                    option.Title = Loc.GetString($"dungeon-config-proto-{asteroid.Id}");
                    var layerKeys = asteroid.MarkerLayers.Keys.ToList();
                    layerKeys.Sort();

                    foreach (var resource in layerKeys)
                    {
                        var count = asteroid.MarkerLayers[resource];

                        var container = new BoxContainer
                        {
                            Orientation = BoxContainer.LayoutOrientation.Horizontal,
                            HorizontalExpand = true,
                        };

                        var resourceLabel = new Label
                        {
                            Text = Loc.GetString("salvage-magnet-resources",
                                ("resource", resource)),
                            HorizontalAlignment = Control.HAlignment.Left,
                        };

                        var countLabel = new Label
                        {
                            Text = Loc.GetString("salvage-magnet-resources-count", ("count", count)),
                            HorizontalAlignment = Control.HAlignment.Right,
                            HorizontalExpand = true,
                        };

                        container.AddChild(resourceLabel);
                        container.AddChild(countLabel);

                        option.AddContent(container);
                    }

                    break;
                case DebrisOffering debris:
                    option.Title = Loc.GetString($"salvage-magnet-debris-{debris.Id}");

                    var debrisClassContainer = new BoxContainer
                    {
                        Orientation = BoxContainer.LayoutOrientation.Horizontal,
                        HorizontalExpand = true,
                    };

                    var debrisClassLabel = new Label
                    {
                        Text = Loc.GetString("salvage-magnet-debris-desc-class"),
                        HorizontalAlignment = Control.HAlignment.Left,
                    };

                    var debrisClassValueLabel = new Label
                    {
                        Text = Loc.GetString($"salvage-magnet-debris-{debris.Id}"),
                        HorizontalAlignment = Control.HAlignment.Right,
                        HorizontalExpand = true,
                    };

                    debrisClassContainer.AddChild(debrisClassLabel);
                    debrisClassContainer.AddChild(debrisClassValueLabel);
                    option.AddContent(debrisClassContainer);

                    var debrisOriginContainer = new BoxContainer
                    {
                        Orientation = BoxContainer.LayoutOrientation.Horizontal,
                        HorizontalExpand = true,
                    };

                    var debrisOriginLabel = new Label
                    {
                        Text = Loc.GetString("salvage-magnet-debris-desc-origin"),
                        HorizontalAlignment = Control.HAlignment.Left,
                    };

                    var debrisOriginValueLabel = new Label
                    {
                        Text = Loc.GetString("salvage-magnet-debris-origin-generic"),
                        HorizontalAlignment = Control.HAlignment.Right,
                        HorizontalExpand = true,
                    };

                    debrisOriginContainer.AddChild(debrisOriginLabel);
                    debrisOriginContainer.AddChild(debrisOriginValueLabel);
                    option.AddContent(debrisOriginContainer);
                    break;
                case SalvageOffering salvage:
                    option.Title = Loc.GetString($"salvage-map-wreck");

                    var designationContainer = new BoxContainer
                    {
                        Orientation = BoxContainer.LayoutOrientation.Horizontal,
                        HorizontalExpand = true,
                    };

                    var designationLabel = new Label
                    {
                        Text = Loc.GetString("salvage-map-wreck-desc-designation"),
                        HorizontalAlignment = Control.HAlignment.Left,
                    };

                    var designationValueLabel = new Label
                    {
                        Text = HumanizeMapId(salvage.SalvageMap.ID),
                        HorizontalAlignment = Control.HAlignment.Right,
                        HorizontalExpand = true,
                    };

                    designationContainer.AddChild(designationLabel);
                    designationContainer.AddChild(designationValueLabel);

                    option.AddContent(designationContainer);

                    var salvContainer = new BoxContainer
                    {
                        Orientation = BoxContainer.LayoutOrientation.Horizontal,
                        HorizontalExpand = true,
                    };

                    var sizeLabel = new Label
                    {
                        Text = Loc.GetString("salvage-map-wreck-desc-size"),
                        HorizontalAlignment = Control.HAlignment.Left,
                    };

                    var sizeValueLabel = new RichTextLabel
                    {
                        HorizontalAlignment = Control.HAlignment.Right,
                        HorizontalExpand = true,
                    };
                    sizeValueLabel.SetMarkup(Loc.GetString(salvage.SalvageMap.SizeString));

                    salvContainer.AddChild(sizeLabel);
                    salvContainer.AddChild(sizeValueLabel);

                    option.AddContent(salvContainer);

                    var mapFileContainer = new BoxContainer
                    {
                        Orientation = BoxContainer.LayoutOrientation.Horizontal,
                        HorizontalExpand = true,
                    };

                    var mapFileLabel = new Label
                    {
                        Text = Loc.GetString("salvage-map-wreck-desc-map"),
                        HorizontalAlignment = Control.HAlignment.Left,
                    };

                    var mapFileValueLabel = new Label
                    {
                        Text = salvage.SalvageMap.MapPath.Filename,
                        HorizontalAlignment = Control.HAlignment.Right,
                        HorizontalExpand = true,
                    };

                    mapFileContainer.AddChild(mapFileLabel);
                    mapFileContainer.AddChild(mapFileValueLabel);

                    option.AddContent(mapFileContainer);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            _window.AddOption(option);
        }
    }

    private static string HumanizeMapId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return "?";

        var spaced = IdWordBreakRegex.Replace(id, "$1$3 $2$4").Replace('_', ' ').Trim();
        if (spaced.Length == 0)
            return id;

        var builder = new StringBuilder(spaced.Length);
        var shouldCapitalize = true;

        foreach (var c in spaced)
        {
            if (char.IsLetter(c))
            {
                builder.Append(shouldCapitalize ? char.ToUpperInvariant(c) : c);
                shouldCapitalize = false;
            }
            else
            {
                builder.Append(c);
                shouldCapitalize = c == ' ' || c == '-';
            }
        }

        return builder.ToString();
    }
}
