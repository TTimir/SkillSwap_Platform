using Microsoft.AspNetCore.Mvc;

namespace SkillSwap_Platform.Services.PDF
{
    public interface IViewRenderService
    {
        Task<string> RenderViewToStringAsync<TModel>(string viewName, TModel model, string controllerName);
    }

}
