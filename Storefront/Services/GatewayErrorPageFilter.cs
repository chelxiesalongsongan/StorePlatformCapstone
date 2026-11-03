using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Storefront.Services;

/// Global handling for gateway security responses on every page:
/// 401 (missing/expired token) => sign out and go to Login; 403 (wrong role) => Access denied.
public class GatewayErrorPageFilter : IAsyncPageFilter
{
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var executed = await next();
        if (executed.ExceptionHandled || executed.Exception is not GatewayException ex) return;

        if (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            var returnUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;
            executed.Result = new RedirectToPageResult("/Account/Login", new { returnUrl });
            executed.ExceptionHandled = true;
        }
        else if (ex.StatusCode == HttpStatusCode.Forbidden)
        {
            executed.Result = new RedirectToPageResult("/Account/AccessDenied");
            executed.ExceptionHandled = true;
        }
    }
}
