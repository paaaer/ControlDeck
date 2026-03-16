namespace ControlDeck.Audio;

/// <summary>
/// Represents a bindable audio volume target.
/// Id is what gets stored in config; DisplayName is shown in the UI.
/// Category drives the group header in the dropdown.
/// The special category "__missing__" marks a configured target that is not currently available.
/// </summary>
public record AudioTarget(string Id, string DisplayName, string Category)
{
    public override string ToString() => DisplayName;
}
