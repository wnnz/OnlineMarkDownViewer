namespace MarkDownViewer.Client.Models;

public sealed class HeadingItem
{
    public string Id { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public int Level { get; set; }
}
