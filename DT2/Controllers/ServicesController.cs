using System.Data;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Web.Routing;
using DotNet.Highcharts;
using DotNet.Highcharts.Options;
using DT2.Models;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using Newtonsoft.Json;

namespace DT2.Controllers
{
    [Authorize]
    public class ServicesController : Controller
    {
        private static ILog logger = LogManager.GetLogger(typeof(ServicesController));

        private static string CreateStatus = "Preparing to create";
        private static string DeleteStatus = "Preparing to delete";

        public static CloudStack.SDK.Client CloudStackClient
        {
            get
            {
                CloudStack.SDK.Client result = null;
                try
                {
                    var csApiUrl = new Uri(DT2.Properties.Settings.Default.CloudStackUrl);
                    var csApiKey = DT2.Properties.Settings.Default.CloudStackApiKey;
                    var csApiSecret = DT2.Properties.Settings.Default.CloudStackSecretKey;
                    result = new CloudStack.SDK.Client(csApiUrl, csApiKey, csApiSecret);
                }
                catch (Exception e)
                {
                    String errMsg = "Exception on CloudStack.SDK.Client creation: " + e.Message + e.StackTrace;
                    logger.Error(errMsg);
                }
                return result;
            }
        }

        /// <summary>
        /// Services dashboard
        /// </summary>
        /// <returns></returns>
        public ActionResult Index()
        {
            //var machineChart = Utils.Utils.StackedBar();
            //ViewBag.BarChart = machineChart;
            using (log4net.NDC.Push(Guid.NewGuid().ToString()))
            {

                if (DT2.Properties.Settings.Default.CheckUserForCreatePrivileges)
                {
                    try
                    {
                        logger.Info("Check whether user "+ this.User.Identity.Name + " has sufficient XenDesktop & AD permission to create a catalog");
                        Catalog.TestADaccess();
                        logger.Info("Success! " + this.User.Identity.Name + " has sufficient XenDesktop & AD permission to create a catalog");
                    }
                    catch (Exception e)
                    {
                        logger.Error("Problem: " + this.User.Identity.Name + " does NOT have sufficient AD permission to create a catalog", e);
                    }
                }
                var catalogList = Catalog.GetCatalogs();
                var templateList = Template.GetTemplates(CloudStackClient);

                var dashboard = new DashboardViewModel()
                {
                    ActiveUsers = 0,
                    DesktopGroups = catalogList.Count,
                    Desktops = 0,
                    DesktopImages = templateList.Count
                };

                foreach (var cat in catalogList)
                {
                    dashboard.Desktops += cat.Count;
                    dashboard.ActiveUsers += cat.CountInUse;
                }

                    var catalogChart = Utils.Utils.PieWithGradientFill(catalogList);
                    ViewBag.PieChart = catalogChart;
                return View(dashboard);
            }
        }

        #region DesktopImages
        public ActionResult DesktopImages(Template newItem)
        {
            using (log4net.NDC.Push(Guid.NewGuid().ToString()))
            {
                logger.Debug("Legit request for Index of templates");
                var templates = Template.GetTemplates(CloudStackClient);

                if (newItem.Name != null)
                {
                    var dbgMsg = "Adding preview of Disk Image " + newItem.Name + " which getting state change of: " +
                                 newItem.Ready;
                    logger.Debug(dbgMsg);
                    var existingItem  = templates.Find(x => x.Name == newItem.Name);

                    // Items being deleted need to have status update if they were not already marked as deleted
                    // TODO: is there a cleaner way to do this?
                    if (existingItem == null)
                    {
                        templates.Add(newItem);
                        logger.Debug("Had to add " + newItem.Name + " to image list");
                    }
                    else if (!existingItem.Ready.ToLowerInvariant().Contains("delet"))
                    {
                        existingItem.Ready = newItem.Ready;
                    }
                }
                templates = templates.OrderBy(x => x.Name).ToList();

                return View(templates);
            }
        }

        public ActionResult DesktopImagesDetails(string id)
        {
            using (log4net.NDC.Push(Guid.NewGuid().ToString()))
            {
                logger.Debug("Legit request for Index of templates");
                var templates = Template.GetTemplates(CloudStackClient, id);
                if (templates.Count == 0)
                {
                    return RedirectToAction("DesktopImages");
                }
                return View(templates[0]);
            }
        }

        public ActionResult DesktopImagesAdd()
        {
            using (log4net.NDC.Push(Guid.NewGuid().ToString()))
            {
                return View();
            }
        }

        [HttpPost]
        public ActionResult DesktopImagesAdd(Template newItem)
        {
            using (log4net.NDC.Push(Guid.NewGuid().ToString()))
            {
                logger.Debug("Legit request to Create template " + newItem.ToString());
                Task.Run(() => Template.CreateTemplate(newItem, CloudStackClient));
                logger.Debug("Task running");

                newItem.Ready = CreateStatus;
                return RedirectToAction("DesktopImages", newItem);
            }
        }

        public ActionResult DesktopImagesDelete(string id)
        {
            using (log4net.NDC.Push(Guid.NewGuid().ToString()))
            {
                if (id == null)
                {
                    logger.Debug("Bad request to Delete: template w/ NULL id!");
                }
                else
                {
                    logger.Debug("Legit request to Delete template w/ id " + id);
                    var delItems = Template.GetTemplates(CloudStackClient, id);
                    if (delItems.Count > 0)
                    {
                        Template.DeleteTemplate(id, CloudStackClient);
                        delItems[0].Ready = DeleteStatus;
                        logger.Debug("Deleting Desktop Group" + delItems[0].Name);
                        return RedirectToAction("DesktopImages", delItems[0]);
                    }
                }
                return RedirectToAction("DesktopImages");
            }
        }

        #endregion

        #region DesktopGroups
        public ActionResult DesktopGroups(Catalog newItem)
        {
            using (log4net.NDC.Push(Guid.NewGuid().ToString()))
            {
                logger.Debug("Listing DesktopGroups page");
                ViewBag.StoreFront = DT2.Properties.Settings.Default.XenDesktopStoreFrontUrl;
                var catalogList = Catalog.GetCatalogs();

                if (newItem.Name != null)
                {
                    var dbgMsg = "Adding preview of Desktop Group " + newItem.Name + " which getting state change of: " +
                                 newItem.Status;
                    logger.Debug(dbgMsg);
                    var existingItem  = catalogList.Find(x => x.Name == newItem.Name);
                    if (existingItem == null)
                    {
                        catalogList.Add(newItem);
                        catalogList = catalogList.OrderBy(x => x.Name).ToList();
                    }
                    // Items being deleted need to have status update if they were not already marked as deleted
                    // TODO: is there a cleaner way to do this?
                    else if (!existingItem.Status.ToLowerInvariant().Contains("delet"))
                    {
                        existingItem.Status = newItem.Status;
                    }
                }
                return View(catalogList);
            }
        }

        public ActionResult DesktopGroupsDetails(string name)
        {
            using (log4net.NDC.Push(Guid.NewGuid().ToString()))
            {
                logger.Debug("Details for DesktopGroup " + name);
                var catInfo = Catalog.GetCatalogAndUserList(name);
                var machines = Machine.GetMachines(name).ToArray();
                foreach (var machine in machines)
                {
                    if (machine.AssociatedUserNames.Length == 0)
                    {
                        logger.Debug("Machine " + machine.MachineName + " has no associated users" );
                        machine.AssociatedUserNames = new string[] { "None at the moment" };
                    }
                    else if (machine.AssociatedUserNames.Length > 1)
                    {
                        logger.Info("Machine " + machine.MachineName + " has no " + machine.AssociatedUserNames.Length + " users");
                    }
                }
                var catDetailsViewModel = new CatalogDetailsViewModel() {CatalogInfo = catInfo[0], Machines = machines};

                return View(catDetailsViewModel);
            }
        }

        public ActionResult DesktopGroupsAdd()
        {
            using (log4net.NDC.Push(Guid.NewGuid().ToString()))
            {
                logger.Debug("Request for Create DesktopGroups page");
                LoginViewModel clientId =
                    LoginViewModel.JsonDeserialize(((FormsIdentity)User.Identity).Ticket);
                ViewBag.Domain = clientId.DomainName;
                // ViewBag offers typeless mechanism of exposing objects to the view page
                ViewBag.Templates = new List<Template>();
//                ViewBag.Templates = Template.GetTemplates(CloudStackClient);
                ViewBag.DesktopTypes = Catalog.DesktopTypeList;
                ViewBag.ComputeOfferings = XenDesktopInventoryItem.GetServiceOfferingList();
                ViewBag.AvailableUsers = Catalog.GetActiveDirectoryUsers("*");
                return View();
            }
        }

        [HttpPost]
        public ActionResult DesktopGroupsAdd(Catalog newItem)
        {
            using (log4net.NDC.Push(Guid.NewGuid().ToString()))
            {
                var dbgMsg = "Request to Create Desktop group " + newItem.Name + " data: " + newItem;
                logger.Debug(dbgMsg);
                try
                {
                    var networks = XenDesktopInventoryItem.GetNetworkList();
                    newItem.Network = networks[networks.Count - 1].Id;
                    logger.Debug("Using network " + newItem.Network);
                    if (XenDesktopInventoryItem.ZoneSupportsSecurityGroups())
                    {
                        var securityGroups = XenDesktopInventoryItem.GetSecurityGroupList();
                        newItem.SecurityGroup = securityGroups[0].Id;
                        logger.Debug("Using security group " + newItem.SecurityGroup);
                    }

                    Catalog.CreateCatalog(newItem);
                    newItem.Status = CreateStatus;

                    // Network names can include an apostrophe, which triggers code that screens for an SQL injection attack,
                    // so we null out the network
                    newItem.Network = null;
                    return RedirectToAction("DesktopGroups", newItem);
                }
                catch (System.Exception ex)
                {
                    var errMsg = "Exception when starting create for " + newItem + " Message: " + ex.Message;
                    logger.Error(errMsg, ex);
                    return View();
                }
            }
        }

        public ActionResult DesktopGroupsAddEx()
        {
            using (log4net.NDC.Push(Guid.NewGuid().ToString()))
            {
                logger.Debug("Request for Create DesktopGroups page with billing");
                LoginViewModel clientId =
                    LoginViewModel.JsonDeserialize(((FormsIdentity)User.Identity).Ticket);
                ViewBag.Domain = clientId.DomainName;
                // ViewBag offers typeless mechanism of exposing objects to the view page
                ViewBag.Templates = new List<Template>();

                // Templates come from AJAX call
                //                ViewBag.Templates = Template.GetTemplates(CloudStackClient);
                ViewBag.DesktopTypes = Catalog.DesktopTypeList;
                ViewBag.ComputeOfferings = XenDesktopInventoryItem.GetServiceOfferingList();
                ViewBag.AvailableUsers = Catalog.GetActiveDirectoryUsers("*");

                // ProductBundle list comes from CPBM
                if (DT2.Properties.Settings.Default.TestDisableProductBundleGet)
                {
                    // Dev scaffolding:
                    dynamic response = JsonConvert.DeserializeObject(ProductBundle.SampleCatalogJson2);
                    ViewBag.Bundles = DT2.Models.ProductBundle.ParseJson(response);
                }
                else
                {
                    var bundles = ProductBundle.GetBundles();  

                    // verify that each bundle has a monthly and one time charge.
                    foreach (var item in bundles)
                    {
                        if (item.RateCardCharges.Count < 2)
                        {
                            item.RateCardCharges.Add("0.00");
                        }
                        if (item.RateCardCharges.Count < 2)
                        {
                            item.RateCardCharges.Add("0.00");
                        }
                    }
                    ViewBag.Bundles = bundles;                   
                }
                return View();
            }
        }

        [HttpPost]
        public ActionResult DesktopGroupsAddEx(Catalog newItem)
        {
            using (log4net.NDC.Push(Guid.NewGuid().ToString()))
            {
                var dbgMsg = "Request to Create Desktop group " + newItem.Name + " data: " + newItem;
                logger.Debug(dbgMsg);
                try
                {
                    var networks = XenDesktopInventoryItem.GetNetworkList();
                    newItem.Network = networks[networks.Count - 1].Id;
                    logger.Debug("Using network " + newItem.Network);
                    if (XenDesktopInventoryItem.ZoneSupportsSecurityGroups())
                    {
                        var securityGroups = XenDesktopInventoryItem.GetSecurityGroupList();
                        newItem.SecurityGroup = securityGroups[0].Id;
                        logger.Debug("Using security group " + newItem.SecurityGroup);
                    }

                    Catalog.CreateCatalog(newItem);
                    newItem.Status = CreateStatus;

                    // Network names can include an apostrophe, which triggers code that screens for an SQL injection attack,
                    // so we null out the network
                    newItem.Network = null;
                    return RedirectToAction("DesktopGroups", newItem);
                }
                catch (System.Exception ex)
                {
                    var errMsg = "Exception when starting create for " + newItem + " Message: " + ex.Message;
                    logger.Error(errMsg, ex);
                    return View();
                }
            }
        }

        public JsonResult GetValidTemplates(string desktopType)
        {
            var templates = Template.GetTemplates(CloudStackClient);

            List<XenDesktopInventoryItem> validTemplates = new List<XenDesktopInventoryItem>();

            // Dev sample
            if (DT2.Properties.Settings.Default.TestDisableImageFetch)
            {
                templates.Clear();
                templates.Add(
                    new Template()
                    {
                        InventoryPath = @"XDHyp:\HostingUnits\CloudResourcesJS\Zone1.availabilityzone\Windows 7.template",
                        Name = "Windows 7",
                        DesktopType = Template.VirtualDesktopType
                    });
                templates.Add(
                    new Template()
                    {
                        InventoryPath = @"XDHyp:\HostingUnits\CloudResourcesJS\Zone1.availabilityzone\CloudDesktopVDA.template",
                        Name = "CloudDesktopVDA",
                        DesktopType = Template.VirtualDesktopType
                    });
                templates.Add(
                    new Template()
                    {
                        InventoryPath = @"XDHyp:\HostingUnits\CloudResourcesJS\Zone1.availabilityzone\Windows 2012R2.template",
                        Name = "Windows 2012R2",
                        DesktopType = Template.PublishedDesktopType
                    });
                templates.Add(
                    new Template()
                    {
                        InventoryPath = @"XDHyp:\HostingUnits\CloudResourcesJS\Zone1.availabilityzone\CloudDesktopVDASvr.template",
                        Name = "CloudDesktopVDASvr",
                        DesktopType = Template.PublishedDesktopType
                    });
            }

            foreach (Template template in templates)
                {

                    if (desktopType == Catalog.PublishedDesktops && template.DesktopType == Template.PublishedDesktopType)
                    {
                        validTemplates.Add(new XenDesktopInventoryItem()
                        {
                            Id = template.InventoryPath,
                            Name = template.Name,
                            Uuid = template.Id
                        });
                    }
                    else if (desktopType != Catalog.PublishedDesktops && template.DesktopType != Template.PublishedDesktopType)
                    {
                        validTemplates.Add(new XenDesktopInventoryItem()
                        {
                            Id = template.InventoryPath,
                            Name = template.Name,
                            Uuid = template.Id
                        });
                    }
                }
                return Json(validTemplates);
        }

        [HttpPost]
        public ActionResult GetUsers(string query)
        {
            using (log4net.NDC.Push(Guid.NewGuid().ToString()))
            {
                logger.Debug("User search request for pattern " + query);

                if (!query.EndsWith("*"))
                {
                    query += "*";
                }
                var users = Catalog.GetActiveDirectoryUsers(query);
                return Json(users);
            }
        }

        public ActionResult DesktopGroupMachineRestart(string machineName, string catalogName)
        {
            using (log4net.NDC.Push(Guid.NewGuid().ToString()))
            {
                logger.Debug("Restarting Machine " + machineName + " from Desktop Group" + catalogName);

                if (machineName == null)
                {
                    Machine.Restart(machineName);
                    logger.Debug("Restarted Machine " + machineName + " from Desktop Group" + catalogName);

                    // TODO: need new HTTPGet that takes the machineName, and sets the machine state to pending.
                    return RedirectToAction("DesktopGroupsDetails", new RouteValueDictionary( new {name = catalogName, pendingMachine = machineName}) );
                }

                return RedirectToAction("DesktopGroupsDetails", new RouteValueDictionary( new {name = catalogName}) );
            }
        }

        public ActionResult DesktopGroupsDelete(string name)
        {
            using (log4net.NDC.Push(Guid.NewGuid().ToString()))
            {
                logger.Debug("Deleting Desktop Group" + name);
                var delItems = Catalog.GetCatalog(name);
                if (delItems.Count > 0)
                {
                    Catalog.DeleteCatalog(name);
                    delItems[0].Status = DeleteStatus;
                    logger.Debug("Deleting Desktop Group" + name);

                    // Network names can include an apostrophe, which triggers code that screens for an SQL injection attack,
                    // so we null out the network
                    delItems[0].Network = null;
                    return RedirectToAction("DesktopGroups", delItems[0]);
                }
                logger.Warn("Desktop group " + name + " already deleted");
                return RedirectToAction("DesktopGroups");
            }
        }

        #endregion
    }
}