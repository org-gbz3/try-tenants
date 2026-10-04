using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Backend.Auth;

// 標準の AutoValidateAntiforgeryToken は View 機能のサービスを前提とするため、API 用に同等の検証だけを行う。
// Cookie 認証の API を別サイトから更新されないよう、GET などの安全なメソッド以外では X-CSRF-TOKEN を必須にする。
public class ValidateAntiforgeryTokenFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var method = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method))
        {
            return;
        }

        if (!await antiforgery.IsRequestValidAsync(context.HttpContext))
        {
            context.Result = new BadRequestObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Detail = "CSRF トークンが無効です。",
            });
        }
    }
}
