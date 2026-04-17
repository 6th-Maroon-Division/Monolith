using Content.Shared.Destructible.Thresholds;
using Content.Shared.Procedural;
using Content.Shared.Procedural.DungeonLayers;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Content.Shared.Salvage.Magnet;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Shared.Salvage;

public abstract partial class SharedSalvageSystem
{
    private readonly List<SalvageMapPrototype> _salvageMaps = new();

    private readonly Dictionary<ISalvageMagnetOffering, float> _offeringWeights = new()
    {
        { new AsteroidOffering(), 4.5f },
        { new DebrisOffering(), 3.5f },
        { new SalvageOffering(), 2.0f },
    };

    private readonly List<ProtoId<DungeonConfigPrototype>> _asteroidConfigs = new()
    {
        "BlobAsteroid",
        "ClusterAsteroid",
        "SpindlyAsteroid",
        "SwissCheeseAsteroid"
    };

    private readonly ProtoId<WeightedRandomPrototype> _asteroidOreWeights = "AsteroidOre";

    private readonly MinMax _asteroidOreCount = new(5, 7);

    private readonly List<ProtoId<DungeonConfigPrototype>> _debrisConfigs = new()
    {
        "ChunkDebris"
    };

    public ISalvageMagnetOffering GetSalvageOffering(int seed)
    {
        var rand = new System.Random(seed);

        var type = SharedRandomExtensions.Pick(_offeringWeights, rand);
        if (type is AsteroidOffering)
        {
            if (TryCreateAsteroidOffering(rand, out var asteroidOffering))
                return asteroidOffering;

            // Fall back to debris if asteroid configs are missing.
            if (TryCreateDebrisOffering(rand, out var debrisOffering))
                return debrisOffering;

            // Final fallback is always salvage maps.
            return CreateSalvageWreckOffering(rand);
        }

        if (type is DebrisOffering)
        {
            if (TryCreateDebrisOffering(rand, out var debrisOffering))
                return debrisOffering;

            return CreateSalvageWreckOffering(rand);
        }

        if (type is SalvageOffering)
            return CreateSalvageWreckOffering(rand);

        throw new NotImplementedException($"Salvage type {type} not implemented!");
    }

    private SalvageOffering CreateSalvageWreckOffering(System.Random rand)
    {
        _salvageMaps.Clear();
        _salvageMaps.AddRange(_proto.EnumeratePrototypes<SalvageMapPrototype>());
        _salvageMaps.Sort((x, y) => string.Compare(x.ID, y.ID, StringComparison.Ordinal));

        if (_salvageMaps.Count == 0)
            throw new InvalidOperationException("No salvage map prototypes are available.");

        var mapIndex = rand.Next(_salvageMaps.Count);
        var map = _salvageMaps[mapIndex];

        return new SalvageOffering
        {
            SalvageMap = map,
        };
    }

    private bool TryCreateAsteroidOffering(System.Random rand, out AsteroidOffering offering)
    {
        offering = default;

        var validConfigs = new List<ProtoId<DungeonConfigPrototype>>();
        foreach (var asteroidConfigId in _asteroidConfigs)
        {
            if (_proto.TryIndex<DungeonConfigPrototype>(asteroidConfigId, out _))
                validConfigs.Add(asteroidConfigId);
        }

        if (validConfigs.Count == 0)
            return false;

        var selectedConfigId = validConfigs[rand.Next(validConfigs.Count)];
        var configProto = _proto.Index<DungeonConfigPrototype>(selectedConfigId);
        var layers = new Dictionary<string, int>();

        var data = new DungeonData();
        data.Apply(configProto.Data);

        var config = new DungeonConfig
        {
            Data = data,
            Layers = new(configProto.Layers),
            MaxCount = configProto.MaxCount,
            MaxOffset = configProto.MaxOffset,
            MinCount = configProto.MinCount,
            MinOffset = configProto.MinOffset,
            ReserveTiles = configProto.ReserveTiles
        };

        var count = _asteroidOreCount.Next(rand);
        var weightedProto = _proto.Index(_asteroidOreWeights);
        for (var i = 0; i < count; i++)
        {
            var ore = weightedProto.Pick(rand);
            config.Layers.Add(_proto.Index<OreDunGenPrototype>(ore));

            var layerCount = layers.GetOrNew(ore);
            layerCount++;
            layers[ore] = layerCount;
        }

        offering = new AsteroidOffering
        {
            Id = selectedConfigId,
            DungeonConfig = config,
            MarkerLayers = layers,
        };

        return true;
    }

    private bool TryCreateDebrisOffering(System.Random rand, out DebrisOffering offering)
    {
        offering = default;

        var validConfigs = new List<ProtoId<DungeonConfigPrototype>>();
        foreach (var debrisConfigId in _debrisConfigs)
        {
            if (_proto.TryIndex<DungeonConfigPrototype>(debrisConfigId, out _))
                validConfigs.Add(debrisConfigId);
        }

        if (validConfigs.Count == 0)
            return false;

        offering = new DebrisOffering
        {
            Id = rand.Pick(validConfigs)
        };
        return true;
    }
}
