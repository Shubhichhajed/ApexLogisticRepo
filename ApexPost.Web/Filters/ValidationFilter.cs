using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ApexPost.Web.Filters;

public sealed class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.ModelState.IsValid)
        {
            if (context.HttpContext.Request.Method == HttpMethods.Post)
            {
                context.Result = new BadRequestObjectResult(context.ModelState);
                return;
            }

            context.Result = new ViewResult { ViewName = context.ActionDescriptor.DisplayName?.Split(" ").FirstOrDefault() ?? "Index" };
            return;
        }

        await next();
    }
}
