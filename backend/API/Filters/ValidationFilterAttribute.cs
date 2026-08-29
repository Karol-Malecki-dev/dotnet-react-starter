using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace API.Filters
{
    /// <summary>
    /// Validation Filter - Checks if ModelState is valid
    /// </summary>
    public class ValidationFilterAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (!context.ModelState.IsValid)
            {
                context.Result = new BadRequestObjectResult(ValidationResponseFactory.Create(context.ModelState));
                return;
            }

            base.OnActionExecuting(context);
        }
    }
}
