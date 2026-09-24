using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.Polyfin.Models;
using Jellyfin.Plugin.Polyfin.Services;
using Jellyfin.Plugin.Polyfin.Transformers;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Polyfin.Filters;

/// <summary>
/// Filter that transforms the metadata of movie items.
/// </summary>
/// <param name="authorizationContext">Instance of the <see cref="IAuthorizationContext"/> interface. Used to identify the user.</param>
/// <param name="languageResolver">nstance of the <see cref="LanguageResolver"/> used to determine the client's language.</param>
/// <param name="movieTransformer">Instance of the <see cref="MovieTransformer"/> used to handle transforming movies.</param>
/// <param name="logger">Instance of the <see cref="ILogger"/>.</param>
public class InterceptionFilter(IAuthorizationContext authorizationContext, LanguageResolver languageResolver, MovieTransformer movieTransformer, ILogger<InterceptionFilter> logger) : IAsyncResultFilter
{
    private readonly IAuthorizationContext _authorizationContext = authorizationContext;
    private readonly LanguageResolver _languageResolver = languageResolver;
    private readonly MovieTransformer _movieTransformer = movieTransformer;
    private readonly ILogger<InterceptionFilter> _logger = logger;

    /// <inheritdoc />
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is not ObjectResult result)
        {
            await next().ConfigureAwait(false);
            return;
        }

        switch (result.Value)
        {
            case BaseItemDto item:
                {
                    var locale = await ResolveLocaleAsync(context.HttpContext).ConfigureAwait(false);
                    await TransformItemAsync(item, locale, context.HttpContext.RequestAborted).ConfigureAwait(false);
                    break;
                }

            case QueryResult<BaseItemDto> itemQuery:
                {
                    var locale = await ResolveLocaleAsync(context.HttpContext).ConfigureAwait(false);
                    foreach (var item in itemQuery.Items)
                    {
                        await TransformItemAsync(item, locale, context.HttpContext.RequestAborted).ConfigureAwait(false);
                    }

                    break;
                }

            case IEnumerable<BaseItemDto> list:
                {
                    var locale = await ResolveLocaleAsync(context.HttpContext).ConfigureAwait(false);
                    foreach (var item in list)
                    {
                        await TransformItemAsync(item, locale, context.HttpContext.RequestAborted).ConfigureAwait(false);
                    }

                    break;
                }
        }

        await next().ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves the metadata locale for the current request.
    /// </summary>
    private async Task<Locale?> ResolveLocaleAsync(HttpContext httpContext)
    {
        var authInfo = await _authorizationContext.GetAuthorizationInfo(httpContext).ConfigureAwait(false);
        var language = _languageResolver.Resolve(authInfo.UserId, httpContext.Request.Headers.AcceptLanguage.ToString());

        return language?.MetadataLocale;
    }

    /// <summary>
    /// Routes an item to the appropriate transformer based on its type.
    /// Currently supports transforming movie items.
    /// </summary>
    private async Task TransformItemAsync(BaseItemDto item, Locale? locale, CancellationToken cancellationToken)
    {
        if (locale == null)
        {
            _logger.LogDebug(
                "Skipping transformation for item {ItemId}: no locale was resolved.",
                item.Id);
            return;
        }

        switch (item.Type)
        {
            case BaseItemKind.Movie:
                {
                    await _movieTransformer.Transform(item, locale, cancellationToken).ConfigureAwait(false);
                    break;
                }
        }
    }
}
