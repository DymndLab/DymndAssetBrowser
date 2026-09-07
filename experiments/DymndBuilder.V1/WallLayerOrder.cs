namespace DymndBuilder.V1;

public enum WallLayerMove { BringToFront, BringForward, SendBackward, SendToBack }

public static class WallLayerOrder
{
    public static IReadOnlyList<string> Move(IReadOnlyList<string> current, string id, WallLayerMove move)
    {
        var result = current.ToList();
        var index = result.IndexOf(id);
        if (index < 0) return result;

        var destination = move switch
        {
            WallLayerMove.BringToFront => result.Count - 1,
            WallLayerMove.BringForward => Math.Min(result.Count - 1, index + 1),
            WallLayerMove.SendBackward => Math.Max(0, index - 1),
            WallLayerMove.SendToBack => 0,
            _ => index
        };
        if (destination == index) return result;
        result.RemoveAt(index);
        result.Insert(destination, id);
        return result;
    }
}
