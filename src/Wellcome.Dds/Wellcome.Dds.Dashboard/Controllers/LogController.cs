using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Wellcome.Dds.Dashboard.Models.Log;

namespace Wellcome.Dds.Dashboard.Controllers
{
    public class LogController : Controller
    {
        private readonly DashOptions dashOptions;
        
        public LogController(IOptions<DashOptions> dashOptions)
        {
            this.dashOptions = dashOptions.Value;
        }
        
        public ActionResult Index()
        {
            var model = new LogModel(dashOptions);
            return View(model);
        }
    }
}