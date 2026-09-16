using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Net.Http.Headers;

namespace Jellyfin.Plugin.Polyfin.Filters;

/// <summary>
/// blAHLBAH.
/// </summary>
public class InterceptionFilter : IAsyncResultFilter
{
    private readonly IAuthorizationContext _authorizationContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="InterceptionFilter"/> class.
    /// </summary>
    /// <param name="authorizationContext">Instance of the <see cref="IAuthorizationContext"/> interface. Used to identify the user.</param>
    public InterceptionFilter(IAuthorizationContext authorizationContext)
    {
        _authorizationContext = authorizationContext;
    }

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
                var (authInfo, acceptedLanguages) = await ResolveRequestContextAsync(context.HttpContext).ConfigureAwait(false);
                await RouteItem(item, authInfo, acceptedLanguages).ConfigureAwait(false);
                break;
            }

            case QueryResult<BaseItemDto> itemQuery:
            {
                var (authInfo, acceptedLanguages) = await ResolveRequestContextAsync(context.HttpContext).ConfigureAwait(false);
                foreach (var item in itemQuery.Items)
                {
                    await RouteItem(item, authInfo, acceptedLanguages).ConfigureAwait(false);
                }

                break;
            }

            case IEnumerable<BaseItemDto> list:
            {
                var (authInfo, acceptedLanguages) = await ResolveRequestContextAsync(context.HttpContext).ConfigureAwait(false);
                foreach (var item in list)
                {
                    await RouteItem(item, authInfo, acceptedLanguages).ConfigureAwait(false);
                }

                break;
            }
        }

        await next().ConfigureAwait(false);
    }

    private async Task<(AuthorizationInfo AuthInfo, IList<StringWithQualityHeaderValue> AcceptedLanguages)> ResolveRequestContextAsync(HttpContext httpContext)
    {
        var authInfo = await _authorizationContext.GetAuthorizationInfo(httpContext).ConfigureAwait(false);
        var acceptedLanguages = GetOrderedLanguages(httpContext.Request);
        return (authInfo, acceptedLanguages);
    }

    private async Task RouteItem(BaseItemDto item, AuthorizationInfo authInfo, IList<StringWithQualityHeaderValue> acceptedLanguages)
    {
        throw new NotImplementedException();
    }

    // Accept-Language is a weighted list ("fi,en-US;q=0.9,en;q=0.8"), not a single
    // value - GetTypedHeaders() parses it into tag+quality pairs instead of us
    // hand-splitting the raw string, sorted by quality here so index 0 is the
    // caller's actual first preference.
    private static IList<StringWithQualityHeaderValue> GetOrderedLanguages(HttpRequest request) =>
        request.GetTypedHeaders().AcceptLanguage
            .OrderByDescending(language => language.Quality ?? 1)
            .ToList();
}
