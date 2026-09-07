using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace FAFamilyBrowser.App.ViewModels;

/// <summary>
/// Observable collection that can replace a large result set with one UI notification.
/// </summary>
public sealed class BulkObservableCollection<T> : ObservableCollection<T>
{
    public void ReplaceAll(IEnumerable<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        Items.Clear();
        foreach (var value in values) Items.Add(value);

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
