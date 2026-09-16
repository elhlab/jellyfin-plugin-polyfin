using System.Collections.Generic;
using System.Threading.Tasks;
using Jellyfin.Plugin.Polyfin.Services;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Jellyfin.Plugin.Polyfin.Filters;

/// <summary>
/// Manual verification filter: recognizes which response shapes carry item
/// data and hands each item to a <see cref="DemoMetadataAnnotator"/> - the
/// interception plumbing only, no business logic of its own. Not real
/// feature logic - delete once the result-filter + provider-query mechanism
/// has been checked against a live server. See notes/jellyfin-plugin-migration.md.
/// </summary>
public class DemoInterceptFilter : IAsyncResultFilter
{
    private readonly DemoMetadataAnnotator _annotator;

    /// <summary>
    /// Initializes a new instance of the <see cref="DemoInterceptFilter"/> class.
    /// </summary>
    /// <param name="annotator">Instance of the <see cref="DemoMetadataAnnotator"/> class.</param>
    public DemoInterceptFilter(DemoMetadataAnnotator annotator)
    {
        // Arrives already built - the container resolves this constructor param
        // from its own registration, we never look it up or call `new` ourselves.
        _annotator = annotator;
    }

    /// <inheritdoc />
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        // Called by ASP.NET Core itself, once per request, after Jellyfin's
        // controller builds context.Result but before it's serialized to JSON.
        // Registered globally (PluginServiceRegistrator), so this runs for every
        // response in the server - the type checks below narrow it to item data.
        // context.Result.Value is the live object, so mutating it in place (before
        // calling next()) is enough - there's no "send it back" step.
        if (context.Result is ObjectResult { Value: BaseItemDto dto })
        {
            await _annotator.AnnotateAsync(dto).ConfigureAwait(false);
        }
        else if (context.Result is ObjectResult { Value: QueryResult<BaseItemDto> query })
        {
            foreach (var item in query.Items)
            {
                await _annotator.AnnotateAsync(item).ConfigureAwait(false);
            }
        }
        else if (context.Result is ObjectResult { Value: IEnumerable<BaseItemDto> list })
        {
            // Items/Latest ("Recently Added") returns a raw list instead of
            // QueryResult<BaseItemDto> - found by testing, not documented anywhere.
            foreach (var item in list)
            {
                await _annotator.AnnotateAsync(item).ConfigureAwait(false);
            }
        }

        await next().ConfigureAwait(false);
    }
}
