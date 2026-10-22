using CommunityToolkit.Mvvm.ComponentModel;

namespace PowerOptionsManager.ViewModels;

/// <summary>
/// A combo box choice whose label can change (on a language switch) without
/// replacing the item, so the combo box keeps its selection.
/// </summary>
public sealed class OptionItem<T> : ObservableObject
{
    private readonly Func<string> _label;

    public OptionItem(T value, Func<string> label)
    {
        Value = value;
        _label = label;
    }

    public T Value { get; }

    public string DisplayName => _label();

    public void RefreshLabel() => OnPropertyChanged(nameof(DisplayName));

    /// <summary>What screen readers announce for the item.</summary>
    public override string ToString() => DisplayName;
}
