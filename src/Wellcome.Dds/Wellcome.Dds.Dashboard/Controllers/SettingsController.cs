using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace Wellcome.Dds.Dashboard.Controllers
{
    public class SettingsController : Controller
    {
        private readonly IWebHostEnvironment webHostEnvironment;

        public SettingsController(IWebHostEnvironment webHostEnvironment)
        {
            this.webHostEnvironment = webHostEnvironment;
        }
        
        public ActionResult Index()
        {
            const string appSettings = "appsettings.json";
            var appSettingsEnv = $"appsettings.{webHostEnvironment.EnvironmentName}.json";
            var settings = new SettingsModel
            {
                AppSettings = System.IO.File.ReadAllText(appSettings),
                AppSettingsForEnvironment = System.IO.File.ReadAllText(appSettingsEnv)
            };
            return View(settings);
        }
    }
}