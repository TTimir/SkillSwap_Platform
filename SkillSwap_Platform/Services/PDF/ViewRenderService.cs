using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc;

namespace SkillSwap_Platform.Services.PDF
{
    public class ViewRenderService : IViewRenderService
    {
        private readonly ICompositeViewEngine _viewEngine;
        private readonly ITempDataProvider _tempDataProvider;
        private readonly IServiceProvider _serviceProvider;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ViewRenderService(
            ICompositeViewEngine viewEngine,
            ITempDataProvider tempDataProvider,
            IServiceProvider serviceProvider,
            IHttpContextAccessor httpContextAccessor)
        {
            _viewEngine = viewEngine;
            _tempDataProvider = tempDataProvider;
            _serviceProvider = serviceProvider;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<string> RenderViewToStringAsync<TModel>(string viewName, TModel model, string controllerName = "Contract")
        {
            // Get the current HttpContext; if null, create a new DefaultHttpContext with RequestServices set
            var httpContext = _httpContextAccessor.HttpContext ?? new DefaultHttpContext { RequestServices = _serviceProvider };

            var routeData = new RouteData();
            routeData.Values["controller"] = controllerName;

            // Create a valid ActionContext
            var actionContext = new ActionContext(httpContext, routeData, new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());

            // Find the view
            var viewResult = _viewEngine.FindView(actionContext, viewName, isMainPage: false);
            if (!viewResult.Success)
                throw new InvalidOperationException($"View '{viewName}' not found for controller '{controllerName}'.");

            try
            {
                await using var sw = new StringWriter();
                var viewContext = new ViewContext(
                actionContext,
                viewResult.View,
                new ViewDataDictionary<TModel>(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = model },
                new TempDataDictionary(httpContext, _tempDataProvider),
                sw,
                new HtmlHelperOptions());

                await viewResult.View.RenderAsync(viewContext);
                return sw.ToString();
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Error rendering view '{viewName}': {ex.Message}", ex);
            }
        }
    }
}