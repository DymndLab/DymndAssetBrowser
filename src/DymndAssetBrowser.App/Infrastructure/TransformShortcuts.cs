using System.Windows.Input;
using DymndAssetBrowser.App.ViewModels;

namespace DymndAssetBrowser.App.Infrastructure;

public static class TransformShortcuts
{
    public static bool Apply(MainViewModel vm, Key key, ModifierKeys modifiers, bool repeat = false)
    {
        if (modifiers is not (ModifierKeys.None or ModifierKeys.Shift)) return false;
        int step = modifiers == ModifierKeys.Shift ? 45 : 90;
        if (key == Key.Q) vm.RotateSelected(-step);
        else if (key == Key.E) vm.RotateSelected(step);
        else if (key == Key.F && modifiers == ModifierKeys.None) vm.FlipSelectedHorizontally();
        else if (key == Key.R && modifiers == ModifierKeys.None) { if (!repeat) vm.RandomRotationEnabled = !vm.RandomRotationEnabled; }
        else return false;
        return true;
    }
}
