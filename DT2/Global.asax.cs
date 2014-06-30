using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.Security;

namespace DT2
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            // log4net has trouble finding the configuration.  We fix that here
            var logConfigFilePath = Server.MapPath("~/Web.config");
            log4net.Config.XmlConfigurator.ConfigureAndWatch(new System.IO.FileInfo(logConfigFilePath));
            log4net.ILog logger = LogManager.GetLogger(typeof(MvcApplication));
            logger.Info("Started WebApp Service");

            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            // Test config
            try
            {
                logger.Info("Checking authentication provider.");
                Membership.ValidateUser("test", "test");

                if (!Directory.Exists(DT2.Properties.Settings.Default.PowerShellScriptsFolder))
                {
                    throw new System.Configuration.ConfigurationErrorsException("Invalid path for powershell scripts, " + DT2.Properties.Settings.Default.PowerShellScriptsFolder + " does not exist.");
                }
                if (!File.Exists(Path.Combine(DT2.Properties.Settings.Default.PowerShellScriptsFolder, "GetCatalogs.ps1")))
                {
                    throw new System.Configuration.ConfigurationErrorsException("Missing powershell scripts at path ");
                }
            }
            catch (System.Configuration.ConfigurationErrorsException exCfg)
            {
                logger.Error("Deployed with bad config.  Might be AD controller name.  Details:  " + exCfg.Message);
                throw exCfg;
            }

            // CloudStack errors might be transient, so we merely log the error.
            try
            {
                var csClient = DT2.Controllers.ServicesController.CloudStackClient;
                var req = new CloudStack.SDK.ListZonesRequest();
                var rply = csClient.ListZones(req);
            }
            catch (CloudStack.SDK.CloudStackException e)
            {
                String errMsg = "Cannot access CloudStack, problem: " + e.Message;
                logger.Error(errMsg);
            }
        }
    }
}
