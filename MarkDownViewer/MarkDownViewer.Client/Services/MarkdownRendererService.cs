using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Microsoft.AspNetCore.Components;

namespace MarkDownViewer.Client.Services;

public sealed class MarkdownRendererService
{
    private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
        .Build();

    public MarkupString Render(string? markdown) =>
        new(Markdown.ToHtml(markdown ?? string.Empty, _pipeline));
}
