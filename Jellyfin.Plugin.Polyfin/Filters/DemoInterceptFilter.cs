using System.Collections.Generic;
using System.Threading.Tasks;
using Jellyfin.Plugin.Polyfin.Services;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Jellyfin.Plugin.Polyfin.Filters;

/// <summary>
/// Manual verification filter: recognizes which response shapes carry item
/// data, resolves who the request is actually from, and hands both to a
/// <see cref="DemoMetadataAnnotator"/> - the interception plumbing only, no
/// business logic of its own. Not real feature logic - delete once the
/// result-filter + provider-query mechanism has been checked against a live
/// server. See notes/jellyfin-plugin-migration.md.
/// </summary>
public class DemoInterceptFilter : IAsyncResultFilter
{
    private readonly DemoMetadataAnnotator _annotator;
    private readonly IAuthorizationContext _authorizationContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DemoInterceptFilter"/> class.
    /// </summary>
    /// <param name="annotator">Instance of the <see cref="DemoMetadataAnnotator"/> class.</param>
    /// <param name="authorizationContext">Instance of the <see cref="IAuthorizationContext"/> interface.</param>
    public DemoInterceptFilter(DemoMetadataAnnotator annotator, IAuthorizationContext authorizationContext)
    {
        // Arrives already built - the container resolves this constructor param
        // from its own registration, we never look it up or call `new` ourselves.
        _annotator = annotator;
        _authorizationContext = authorizationContext;
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
        if (context.Result is ObjectResult { Value: BaseItemDto or QueryResult<BaseItemDto> or IEnumerable<BaseItemDto> } objectResult)
        {
            // Same request auth ticket Jellyfin's own controllers resolve per call -
            // gives the caller's UserId/Client/DeviceId, none of which context.Result
            // carries on its own. Resolved once per request, not once per item.
            var authInfo = await _authorizationContext.GetAuthorizationInfo(context.HttpContext).ConfigureAwait(false);

            // Plain HTTP header, no Jellyfin API needed - unlike AuthorizationInfo,
            // ASP.NET Core exposes this on every request regardless of what sent it.
            var acceptLanguage = context.HttpContext.Request.Headers.AcceptLanguage.ToString();

            if (objectResult.Value is BaseItemDto dto)
            {
                await _annotator.AnnotateAsync(dto, authInfo, acceptLanguage).ConfigureAwait(false);
            }
            else if (objectResult.Value is QueryResult<BaseItemDto> query)
            {
                foreach (var item in query.Items)
                {
                    await _annotator.AnnotateAsync(item, authInfo, acceptLanguage).ConfigureAwait(false);
                }
            }
            else if (objectResult.Value is IEnumerable<BaseItemDto> list)
            {
                // Items/Latest ("Recently Added") returns a raw list instead of
                // QueryResult<BaseItemDto> - found by testing, not documented anywhere.
                foreach (var item in list)
                {
                    await _annotator.AnnotateAsync(item, authInfo, acceptLanguage).ConfigureAwait(false);
                }
            }
        }

        await next().ConfigureAwait(false);
    }
}
