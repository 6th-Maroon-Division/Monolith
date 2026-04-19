using System.Globalization;
using System.Numerics;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;
using Robust.Shared.Utility;

namespace Content.Shared.EntityTable.ValueSelector;

[TypeSerializer]
public sealed class NumberSelectorTypeSerializer :
    ITypeReader<NumberSelector, ValueDataNode>
{
    public ValidationNode Validate(ISerializationManager serializationManager,
        ValueDataNode node,
        IDependencyCollection dependencies,
        ISerializationContext? context = null)
    {
        // ConstantNumberSelector validation
        if (int.TryParse(node.Value, out _))
            return new ValidatedValueNode(node);

        // RangeNumberSelector validation
        if (VectorSerializerUtility.TryParseArgs(node.Value, 2, out _))
        {
            return new ValidatedValueNode(node);
        }

        return new ErrorNode(node, "Custom validation not supported! Please specify the type manually!");
    }

    public NumberSelector Read(ISerializationManager serializationManager,
        ValueDataNode node,
        IDependencyCollection dependencies,
        SerializationHookContext hookCtx,
        ISerializationContext? context = null,
        ISerializationManager.InstantiationDelegate<NumberSelector>? instanceProvider = null)
    {
        if (int.TryParse(node.Value, out var result))
            return new ConstantNumberSelector(result);

        if (VectorSerializerUtility.TryParseArgs(node.Value, 2, out var args))
        {
            var x = float.Parse(args[0], CultureInfo.InvariantCulture);
            var y = float.Parse(args[1], CultureInfo.InvariantCulture);
            return new RangeNumberSelector { Range = new Vector2(x, y) };
        }

        return (NumberSelector) serializationManager.Read(typeof(NumberSelector), node, context)!;
    }
}
