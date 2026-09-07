namespace FAFamilyBrowser.Core.Models;

public sealed class ExplorerSelection
{
    private readonly HashSet<string> _selected = new(StringComparer.OrdinalIgnoreCase);
    private string? _anchor;
    public IReadOnlyCollection<string> Selected => _selected;

    public void Select(string path, IReadOnlyList<string> visibleOrder, bool control = false, bool shift = false, bool rightClick = false)
    {
        if (rightClick)
        {
            if (_selected.Contains(path)) return;
            _selected.Clear(); _selected.Add(path); _anchor = path; return;
        }
        if (shift && _anchor is not null)
        {
            var a = IndexOf(visibleOrder, _anchor); var b = IndexOf(visibleOrder, path);
            if (a >= 0 && b >= 0)
            {
                if (!control) _selected.Clear();
                for (var i = Math.Min(a, b); i <= Math.Max(a, b); i++) _selected.Add(visibleOrder[i]);
                return;
            }
        }
        if (control)
        {
            if (!_selected.Add(path)) _selected.Remove(path);
            _anchor = path; return;
        }
        _selected.Clear(); _selected.Add(path); _anchor = path;
    }

    public void Retain(IEnumerable<string> visiblePaths) => _selected.IntersectWith(visiblePaths);
    public void Clear() { _selected.Clear(); _anchor = null; }
    private static int IndexOf(IReadOnlyList<string> items, string value)
    {
        for (var i = 0; i < items.Count; i++) if (string.Equals(items[i], value, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }
}
