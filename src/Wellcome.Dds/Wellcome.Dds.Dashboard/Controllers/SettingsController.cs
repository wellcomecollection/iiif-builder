using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Wellcome.Dds.Dashboard.Models.Settings;

namespace Wellcome.Dds.Dashboard.Controllers
{
    public class SettingsController : Controller
    {
        private readonly IWebHostEnvironment webHostEnvironment;
        private readonly BuildInfo buildInfo;

        public SettingsController(IWebHostEnvironment webHostEnvironment, IOptions<BuildInfo> buildInfo)
        {
            this.webHostEnvironment = webHostEnvironment;
            this.buildInfo = buildInfo.Value;
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

        public ActionResult Build()
        {
            var model = new BuildModel
            {
                Build = buildInfo,
                EnvironmentName = webHostEnvironment.EnvironmentName,
                Started = Process.GetCurrentProcess().StartTime,
                Framework = RuntimeInformation.FrameworkDescription,
                MachineName = Environment.MachineName
            };
            return View(model);
        }
    }
}