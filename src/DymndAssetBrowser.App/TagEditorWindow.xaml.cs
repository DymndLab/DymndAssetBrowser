using System.ComponentModel;
using System.Windows;
using DymndAssetBrowser.App.ViewModels;
using DymndAssetBrowser.Core.Models;

namespace DymndAssetBrowser.App;

public partial class TagEditorWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly AssetRecord[] _targets;
    private bool _ready, _saving;
    public TagEditorWindow(MainViewModel vm)
    {
        _vm = vm;
        _targets = vm.GetTagEditTargets();
        InitializeComponent();
        TargetsLabel.Text = _targets.Length == 1 ? _targets[0].FileName : $"{_targets.Length:N0} selected assets";
        TagDetailsText.Text = vm.TagDetails(_targets.FirstOrDefault());
        _ready = true;
        UpdatePreview();
    }
    private string[] PendingTags => TagInput.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    private void InputChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready || _saving) return;
        SaveStatus.Text = "";
        UpdatePreview();
    }
    private void UpdatePreview()
    {
        var tags = PendingTags;
        ApplyButton.IsEnabled = !_saving && _targets.Length > 0 && tags.Length > 0;
        ChangePreview.Text = tags.Length == 0 ? "No pending changes." :
            $"{(RemoveMode.IsChecked == true ? "Remove" : "Add")}: {string.Join(", ", tags)} — {_targets.Length:N0} asset(s)." +
            (RemoveMode.IsChecked == true ? "\nCustom-only tags will be deleted. Automatic source tags will be suppressed." : "\nExisting unrelated tags are preserved.");
    }
    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (_saving || PendingTags.Length == 0 || _targets.Length == 0) return;
        var text = string.Join(", ", PendingTags);
        var removing = RemoveMode.IsChecked == true;
        _saving = true;
        ApplyButton.IsEnabled = CloseButton.IsEnabled = TagInput.IsEnabled = AddMode.IsEnabled = RemoveMode.IsEnabled = false;
        SaveStatus.Text = "Saving…";
        try
        {
            // Targets stay fixed even if the edit removes them from the active filter.
            await _vm.EditTagsAsync(_targets, text, removing);
            TagDetailsText.Text = _vm.TagDetails(_targets.FirstOrDefault());
            TagInput.Clear();
            SaveStatus.Text = $"Saved: {(removing ? "removed" : "added")} {text} on {_targets.Length:N0} asset(s). Source files unchanged.";
            CloseButton.Content = "Close";
        }
        catch (Exception ex) { SaveStatus.Text = "Could not complete the tag update: " + ex.Message; }
        finally
        {
            _saving = false;
            CloseButton.IsEnabled = TagInput.IsEnabled = AddMode.IsEnabled = RemoveMode.IsEnabled = true;
            UpdatePreview();
        }
    }
    private void Close_Click(object sender, RoutedEventArgs e) { if (!_saving) Close(); }
    protected override void OnClosing(CancelEventArgs e) { if (_saving) e.Cancel = true; base.OnClosing(e); }
}
