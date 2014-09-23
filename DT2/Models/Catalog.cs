using System.Threading;
using log4net;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Web;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Threading.Tasks;
using DT2.Utils;
using System.Web.Security;

namespace DT2.Models
{
    /// <summary>
    /// Models a Desktop Group
    /// </summary>
    public class Catalog
    {
        private static ILog logger = LogManager.GetLogger(typeof (Catalog));

        /// <summary>
        /// Available after Catalog is created
        /// </summary>
        [Display(Name = "Id")]
        [DataType(DataType.Text)]
        public string Id { get; set; }

        [Required(ErrorMessage = "Name required, limited to alphanumeric characters and select special chars ( $, - , _ )")]
        [Display(Name = "Name")]
        [DataType(DataType.Text)]
        [RegularExpression("([a-zA-Z0-9$-_]+)",
            ErrorMessage = "Name required, limited to alphanumeric characters and select special chars ( $, - , _ )")]
        [StringLength(12, ErrorMessage = "Group names limited to 12 characters for now")]
        public string Name { get; set; }

        [Required(ErrorMessage = "The description required, cannot be more than 140 characters.")]
        [StringLength(140, ErrorMessage = "The description required, cannot be more than 140 characters.")]
        [DataType(DataType.Text)]
        [Display(Name = "Description")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Number of desktops must be whole number greater than zero")]
        [Display(Name = "No. of Desktops")]
        [Range(0.99, Int32.MaxValue, ErrorMessage = "Number of desktops must be whole number greater than zero")]
        [RegularExpression("([1-9][0-9]*)", ErrorMessage = "Number of desktops must be whole number greater than zero")]
        public int Count { get; set; }

        [Required]
        [Display(Name = "Desktops in use")]
        public int CountInUse { get; set; }

        [Required]
        [DataType(DataType.Text)]
        [Display(Name = "Desktop Image")]
        public string Template { get; set; }

        [Required]
        [DataType(DataType.Text)]
        [Display(Name = "Desktop Type")]
        public string DesktopType { get; set; }

        [Required]
        [DataType(DataType.Text)]
        [Display(Name = "Desktop Compute Offering")]
        public string ComputeOffering { get; set; }

        [Display(Name = "Desktop Disk Size")]
        public int DiskSize { get; set; }

        [Required]
        [DataType(DataType.Text)]
        [Display(Name = "Network")]
        public string Network { get; set; }

        [Required]
        [DataType(DataType.Text)]
        [Display(Name = "SecurityGroup")]
        public string SecurityGroup { get; set; }

        // TODO: correct type for list of user strings?
        [Required(ErrorMessage = "Select at least one group or user")]
        [DataType(DataType.Text)]
        [Display(Name = "Assigned Users / Groups")]
        //[MinLength(1, ErrorMessage = "Select at least one group or user")]
        public string[] Users { get; set; }

        [DataType(DataType.Text)]
        [Display(Name = "ProvisioningSchemeId")]
        public string ProvisioningSchemeId { get; set; }

        // TODO: populate this with metadata 
        [DataType(DataType.Text)]
        [Display(Name = "Status")]
        public string Status { get; set; }

       
        [DataType(DataType.Text)]
        [Display(Name = "Code")]
        public string ProductBundleCode { get; set; }

        public static List<Catalog> GetCatalogs()
        {
            return GetCatalog(null);
        }

        public static List<Catalog> GetCatalogAndUserList(string catalogName)
        {
            var result = GetCatalog(catalogName);
            try
            {
                GetBrokerCatalogUsers(catalogName, result);
            }
            catch (Exception e)
            {
                var errMsg = e.Message;
                logger.Error(errMsg);
            }
            return result;
        }

        public static List<Catalog> GetCatalog(string catalogName)
        {
            var result = new List<Catalog>();
            Dictionary<string, object> psargs = new Dictionary<string, object>();
            psargs.Add("catalogName", catalogName);

            try
            {
                logger.Debug("Retrieve list of catalogs");
                GetBrokerCatalogInfo(catalogName, result);
                logger.Debug("GetBrokerCatalogInfo returned " + result.Count + " catalogs ");
            }
            catch (Exception e)
            {
                var errMsg = e.Message;
                logger.Error(errMsg);
                return result;
            }

            try
            {
                logger.Debug("Add provisioning scheme details");
                GetProvSchemeDetails(catalogName, result);
                logger.Debug("GetProvSchemeDetails updated " + result.Count + " catalogs ");
            }
            catch (Exception e)
            {
                var errMsg = "Exception while inspecting ProvScheme details" + e.Message;
                logger.Error(errMsg);
                return result;
            }
            try
            {
                logger.Debug("Add desktop deliver group details");
                GetDesktopGroupDetails(catalogName, result);
                logger.Debug("GetDesktopGroupDetails updated " + result.Count + " catalogs ");
            }
            catch (Exception e)
            {
                var errMsg = "Exception while inspecting desktop group details" + e.Message;
                logger.Error(errMsg);
            }

            return result;
        }

        private static void GetProvSchemeDetails(string catalogName, List<Catalog> result)
        {
            Dictionary<string, object> psargs = new Dictionary<string, object>();
            psargs.Add("catalogName", catalogName);

            var listProvScript =
                new PsWrapper(Path.Combine(DT2.Properties.Settings.Default.PowerShellScriptsFolder,
                    ScriptNames.GetProvSchemesScript));

            LoginViewModel clientId =
                LoginViewModel.JsonDeserialize(((FormsIdentity) HttpContext.Current.User.Identity).Ticket);
            var psProvs = listProvScript.RunPowerShell(psargs, clientId);

            foreach (PSObject item in psProvs)
            {
                string provName = (string) item.Members["ProvisioningSchemeName"].Value;
                Guid provId = (Guid) item.Members["ProvisioningSchemeUid"].Value;
                string templatePath = (string) item.Members["MasterImageVM"].Value;
                string serviceOffering = (string) item.Members["ServiceOffering"].Value;
                string[] securityGroups = (item.Members["SecurityGroups"] == null
                    ? new string[0]
                    : (string[]) item.Members["SecurityGroups"].Value);
                string securityGroup = securityGroups.Length > 0 ? securityGroups[0] : "";
                // Assert
                if (templatePath == null)
                {
                    var errMsg = "No template for provisioning scheme " + provName;
                    logger.Error(errMsg);
                    continue;
                }

                // Assert
                if (serviceOffering == null)
                {
                    var errMsg = "No Desktop compute offering for provisioning scheme " + provName;
                    logger.Error(errMsg);
                    continue;
                }

                string[] templateSplitPath = templatePath.Split(new char[] {'\\'});
                string template = templateSplitPath[templateSplitPath.Length - 1];

                // Assert
                if (!template.EndsWith(".template"))
                {
                    var errMsg = "Template ends with wrong extension, path is " + templatePath + " template name is " +
                                 template;
                    logger.Error(errMsg);
                    continue;
                }

                // Update corresponding Catalog
                int assertUsage = 0;
                foreach (var cat in result)
                {
                    if (cat.ProvisioningSchemeId == provId.ToString())
                    {
                        assertUsage++;
                        cat.Template = template.Substring(0, template.Length - ".template".Length);
                        cat.ComputeOffering = serviceOffering.Substring(0,
                            serviceOffering.Length - ".serviceoffering".Length);
                        cat.DiskSize = (int) item.Members["DiskSize"].Value;

                        // TODO: ask SDK team for alternative to dyanamic variables
                        dynamic networkList = item.Members["NetworkMaps"].Value;
                        dynamic netDetails = networkList[0];
                        string networkPath = (string) netDetails.NetworkPath;

                        string[] networkSplitPath = networkPath.Split(new char[] {'\\'});
                        string networkName = networkSplitPath[networkSplitPath.Length - 1];
                        cat.Network = networkName.Substring(0, networkName.Length - ".network".Length);
                        cat.SecurityGroup = securityGroup;
                    }
                }

                // Assert
                if (assertUsage > 1)
                {
                    var errMsg = "Provisioning scheme used in multiple catalogs: name " + provName + " UUID " + provId;
                    logger.Error(errMsg);
                }
            } // End foreach.
        }

        private static void GetDesktopGroupDetails(string catalogName, List<Catalog> result)
        {
            string desktopGrpName = string.IsNullOrEmpty(catalogName)
                ? null
                : catalogName + ScriptNames.DesktopGroupSuffix;
            Dictionary<string, object> psargs = new Dictionary<string, object>();
            psargs.Add("desktopGroupName", desktopGrpName);

            var listProvScript =
                new PsWrapper(Path.Combine(DT2.Properties.Settings.Default.PowerShellScriptsFolder,
                    ScriptNames.GetDesktopGroupDetailsScript));
            LoginViewModel clientId =
                LoginViewModel.JsonDeserialize(((FormsIdentity) HttpContext.Current.User.Identity).Ticket);
            var psProvs = listProvScript.RunPowerShell(psargs, clientId);

            foreach (PSObject item in psProvs)
            {
                var itemtype = item.BaseObject.GetType().FullName;
                logger.Debug("GetDesktopGroupDetails processing obj of type " + itemtype);
                int totalDesktops = (int) item.Members["TotalDesktops"].Value;
                int totalSessions = (int) item.Members["Sessions"].Value;
                string name = (string) item.Members["Name"].Value;
                // Update corresponding Catalog
                int assertUsage = 0;
                foreach (var cat in result)
                {
                    if (cat.Name + ScriptNames.DesktopGroupSuffix == name)
                    {
                        assertUsage++;
                        cat.CountInUse = totalSessions;
                        cat.Count = totalDesktops;
                    }
                }

                // Assert
                if (assertUsage > 1)
                {
                    var errMsg = "Provisioning scheme used in multiple catalogs: name " + name;
                    logger.Error(errMsg);
                }
            } // End foreach.
        }

        private static void GetBrokerCatalogInfo(string catalogName, List<Catalog> result)
        {
            Dictionary<string, object> psargs = new Dictionary<string, object>();
            psargs.Add("catalogName", catalogName);

            // Catalog query provides user-defined descriptions such as Name and Description
            var listCatScript =
                new PsWrapper(Path.Combine(DT2.Properties.Settings.Default.PowerShellScriptsFolder,
                    ScriptNames.GetCatalogsDetailsScript));
            LoginViewModel clientId =
                LoginViewModel.JsonDeserialize(((FormsIdentity) HttpContext.Current.User.Identity).Ticket);
            var psCats = listCatScript.RunPowerShell(psargs, clientId);

            foreach (PSObject item in psCats)
            {
                string name = (string) item.Members["Name"].Value;
                int id = (int) item.Members["Uid"].Value;
                string description = (string) (item.Members["Description"].Value ?? String.Empty);
                string provid = (item.Members["ProvisioningSchemeId"].Value ?? String.Empty).ToString();

                string sessionSupport = item.Members["SessionSupport"].Value.ToString();
                string allocationType = item.Members["AllocationType"].Value.ToString();
                string desktopType = GetXenDesktopDesktopType(allocationType, sessionSupport);

                var metadata = (System.Collections.Generic.Dictionary<string, string>) item.Members["MetadataMap"].Value;
                string diaasStatus = "Undetermined";
                if (metadata.ContainsKey("DIaaS_Status"))
                {
                    diaasStatus = metadata["DIaaS_Status"];
                }

                var newCat = new Catalog()
                {
                    Name = name,
                    Description = description,
                    Id = id.ToString(),
                    // TODO: verify that count is correct.
                    Count = (int) item.Members["UnassignedCount"].Value + (int) item.Members["AssignedCount"].Value,
                    ProvisioningSchemeId = provid,
                    DesktopType = desktopType,
                    Status = diaasStatus
                };

                result.Add(newCat);
            } // End foreach.
        }

        /// <summary>
        /// Use this to determine if the logged in user has sufficient permissions to create a catalog.
        /// Specifically, the New-AcctADAccount call will fail if the user does not have sufficient privilege for the 
        /// domain in which new desktop machine accounts are being registered.
        /// TODO: may need to write code to reset the PowerShell environment after the script has run.
        /// </summary>
        /// <param name="catalogName"></param>
        /// <param name="results"></param>
        public static void TestADaccess()
        {
            Dictionary<string, object> psargs = new Dictionary<string, object>();
            psargs.Add("ddcAddress", DT2.Properties.Settings.Default.XenDesktopAdminAddress);
            psargs.Add("desktopDomain", DT2.Properties.Settings.Default.XenDesktopDomain);

            var listProvScript =
                new PsWrapper(Path.Combine(DT2.Properties.Settings.Default.PowerShellScriptsFolder,
                    ScriptNames.TestADAccess));
            LoginViewModel clientId =
                LoginViewModel.JsonDeserialize(((FormsIdentity) HttpContext.Current.User.Identity).Ticket);
            var psProvs = listProvScript.RunPowerShell(psargs, clientId);
        }


        /// <summary>
        /// The list of users associated with a DesktopGroup is in the DesktopGroupAccessPolicy.  In our case, 
        /// there are two per desktop group.  One is used when accessing the group through an AG (Access Gateway).
        /// The other is used when accessing the desktop directly.
        /// </summary>
        /// <param name="catalogName"></param>
        /// <param name="results"></param>
        private static void GetBrokerCatalogUsers(string catalogName, List<Catalog> result)
        {
            string desktopGrpAccessPolicyName = string.IsNullOrEmpty(catalogName)
                ? null
                : catalogName + ScriptNames.DesktopGroupSuffix + ScriptNames.AccessPolicySuffixForDirect;
            Dictionary<string, object> psargs = new Dictionary<string, object>();
            psargs.Add("desktopGroupPolicyName", desktopGrpAccessPolicyName);

            var listProvScript =
                new PsWrapper(Path.Combine(DT2.Properties.Settings.Default.PowerShellScriptsFolder,
                    ScriptNames.GetDesktopGroupsAccessPolicy));
            LoginViewModel clientId =
                LoginViewModel.JsonDeserialize(((FormsIdentity) HttpContext.Current.User.Identity).Ticket);
            var psProvs = listProvScript.RunPowerShell(psargs, clientId);

            // TODO:  add the users to the list.
            foreach (PSObject item in psProvs)
            {
                dynamic includedUsers = item.Members["IncludedUsers"].Value;
                dynamic excludedUsers = item.Members["ExcludedUsers"].Value;
                string name = (string) item.Members["Name"].Value;

                // assertions
                if (includedUsers.Length < 1)
                {
                    var errMsg = "DesktopGroup for broker catalog " + catalogName + " has no included users!";
                    logger.Error(errMsg);
                }
                if (excludedUsers.Length > 0)
                {
                    var errMsg = "DesktopGroup for broker catalog " + catalogName + " *has* excluded users!";
                    logger.Error(errMsg);
                }

                List<string> userList = new List<string>();
                foreach (dynamic user in includedUsers)
                {
                    userList.Add(user.Name);
                }

                // Update corresponding Catalog
                int assertUsage = 0;
                foreach (var cat in result)
                {
                    if (catalogName + ScriptNames.DesktopGroupSuffix + ScriptNames.AccessPolicySuffixForDirect == name)
                    {
                        assertUsage++;
                        cat.Users = userList.ToArray();
                    }
                }

                // Make sure there is a value for 'Users'
                foreach (var cat in result)
                {
                    if (cat.Users == null)
                    {
                        cat.Users = new string[0];
                        var infoMsg = "DesktopGroup for broker catalog " + catalogName + " has NO users!";
                        logger.Error(infoMsg);
                    }
                }

                // Assert
                if (assertUsage > 1)
                {
                    var errMsg = "Provisioning scheme used in multiple catalogs: name " + name;
                    logger.Error(errMsg);
                }
            } // End foreach.
        }

        public static void CreateCatalog(Catalog newCat)
        {
            try
            {
                var jsonEquiv = Newtonsoft.Json.JsonConvert.SerializeObject(newCat);
                logger.Info("Creating Catalog corresponding to " + Newtonsoft.Json.JsonConvert.ToString(jsonEquiv));
                Dictionary<string, object> psargs = new Dictionary<string, object>();
                psargs.Add("ddcAddress", DT2.Properties.Settings.Default.XenDesktopAdminAddress);
                psargs.Add("catalogName", newCat.Name);
                psargs.Add("catalogDesc", newCat.Description);
                psargs.Add("catalogSessionSupport", GetXenDesktopDesktopSessionSupport(newCat.DesktopType));
                psargs.Add("desktopAllocationType", GetXenDesktopDesktopAllocationType(newCat.DesktopType));
                psargs.Add("persistUserChanges", GetXenDesktopDesktopPersistUserChanges(newCat.DesktopType));
                psargs.Add("desktopCleanOnBoot", GetXenDesktopDesktopCleanOnBoot(newCat.DesktopType));
                psargs.Add("desktopDomain", DT2.Properties.Settings.Default.XenDesktopDomain);
                psargs.Add("templatePath", newCat.Template);
                psargs.Add("networkPath", newCat.Network);
                psargs.Add("hostingUnitName", DT2.Properties.Settings.Default.XenDesktopHostingUnitName);
                psargs.Add("controllerAddress", DT2.Properties.Settings.Default.XenDesktopDDC);
                logger.Info("Catalog has " + newCat.Users.Length + " AD accounts and  " + newCat.Count + " machines.");
                psargs.Add("desktopCount", newCat.Count);
                psargs.Add("computerOffering", newCat.ComputeOffering);

                // Naming scheme: Remove spaces and other special characters from the pool name, truncate at 12 characters
                string desktopNamingScheme = newCat.Name.Replace(" ", string.Empty);
                desktopNamingScheme = desktopNamingScheme.Length > 12
                    ? desktopNamingScheme.Substring(0, 12)
                    : desktopNamingScheme;
                desktopNamingScheme += "###";
                psargs.Add("desktopNamingScheme", desktopNamingScheme);

                // Parameters required to setup the corresponding desktopgroup
                psargs.Add("userNames", newCat.Users);
                psargs.Add("desktopGrpName", newCat.Name + ScriptNames.DesktopGroupSuffix);
                psargs.Add("machineCount", newCat.Count);

                string secGrps = DT2.Properties.Settings.Default.SecurityGroups;
                secGrps = secGrps ?? String.Empty;
                if (!string.IsNullOrEmpty((newCat.SecurityGroup)))
                {
                    secGrps = newCat.SecurityGroup;
                }
                psargs.Add("securityGroups", secGrps);

                var poshScript =
                    new PsWrapper(Path.Combine(DT2.Properties.Settings.Default.PowerShellScriptsFolder,
                        ScriptNames.CreateDesktopGroupScript));
                var jsonEquiv2 = Newtonsoft.Json.JsonConvert.SerializeObject(psargs);
                logger.Info("Calling " + ScriptNames.CreateDesktopGroupScript + " with args: " + jsonEquiv2);

                var exToIgnore = "Citrix.Broker.Admin.SDK.SdkOperationException";
                poshScript.IgnoreExceptions.Add(exToIgnore);
                logger.Info("Ignoring exceptions of type " + exToIgnore +
                            " because the SDK generates them for Get-BrokerUser calls as part of normal operation.");


                // Assert
                if (newCat.DesktopType == PublishedDesktops)
                {
                    if ("Random" != (string) psargs["desktopAllocationType"])
                    {
                        var errMsg = "Wrong desktopAllocationType for " + newCat.Name + " of type " + newCat.DesktopType;
                        throw new ArgumentException(errMsg);
                    }
                    if ("MultiSession" != (string) psargs["catalogSessionSupport"])
                    {
                        var errMsg = "Wrong catalogSessionSupport for " + newCat.Name + " of type " + newCat.DesktopType;
                        throw new ArgumentException(errMsg);
                    }
                    if ("Discard" != (string) psargs["persistUserChanges"])
                    {
                        var errMsg = "Wrong persistUserChanges for " + newCat.Name + " of type " + newCat.DesktopType;
                        throw new ArgumentException(errMsg);
                    }
                    if (psargs["desktopCleanOnBoot"] == null)
                    {
                        var errMsg = "Wrong desktopCleanOnBoot  for " + newCat.Name + " of type " + newCat.DesktopType;
                        throw new ArgumentException(errMsg);
                    }
                }
                if (DT2.Properties.Settings.Default.TestDisableCatalogCreate)
                {
                    logger.Warn("Skipping Catalog create, as TestDisableCatalogCreate is set true");
                }
                else
                {

                    // Enable Windows Authentication in ASP.NET *and* IIS to ensure User.Identity is a FormsIdentity
                    LoginViewModel clientId =
                        LoginViewModel.JsonDeserialize(((FormsIdentity) HttpContext.Current.User.Identity).Ticket);
                    // add the ndc context
                    string ndcContext = log4net.NDC.Pop();
                    log4net.NDC.Push(ndcContext);

                    Task.Run(() => RunLongScript(poshScript, psargs, clientId, ndcContext,() =>
                    {
                        // If a subscription was selected, create one subscription per desktop.
                        // NB: in a real deployment, do not make this step optional.
                        if (newCat.ProductBundleCode != null)
                        {
                            newCat.Subscribe();
                        }

                        EmailAdmin(newCat, clientId);
                    }));
                }
            }
            catch (Exception e)
            {
                var errMsg = e.Message;
                logger.Error(errMsg);
            }
        }


        public List<Subscription> Subscribe()
        {
            List<Machine> vmInfo = Machine.GetMachines(this.Name);

            return this.Subscribe(vmInfo);
        }


        public List<Subscription> Subscribe(List<Machine> vmInfo)
        {
            logger.Debug("Create subscription " + this.Name + " for productBundleId " + this.ProductBundleCode);

            // assert
            if (vmInfo.Count != this.Count)
            {
                string errMsg = "Catalog Count and actual number of machines should be the same value.";
                var ex = new ArgumentOutOfRangeException(errMsg);
                logger.Error(errMsg, ex);
                throw ex;
            }

            // Create the subscriptions.
            var newSubs = new List<Subscription>();

            // Sometimes foreach is the simplest way
            // see http://blogs.msdn.com/b/ericlippert/archive/2009/05/18/foreach-vs-foreach.aspx
            foreach (var vm in vmInfo)
            {
                logger.Debug("Creating subscription to productBundleId " + this.ProductBundleCode +
                             " for desktop vm " + vm.MachineName);
                var result = Subscription.Create(this.ProductBundleCode, vm.MachineName, this.Name);
                newSubs.Add(result);
            }

            // Wait for subscriptions to ACTIVATE
            Subscription.WaitForSubscriptionsToBeActive(newSubs);

            // use 'Zip' to avoid need for tracking iterators in a loop...
            // See http://stackoverflow.com/a/2249415/939250
            var attachedSubs = newSubs.Zip(vmInfo, (sub, machine) => sub.Attach(machine.VmId, machine.MachineName));

            // assert
            if (attachedSubs.Count() != newSubs.Count)
            {
                string errMsg = "Zipped subscriptions and machine IDs, output subscription count did not matching input count.";
                var ex =  new ArgumentOutOfRangeException(errMsg);
                logger.Error(errMsg, ex);
                throw ex;
            }
            return newSubs;
        }
        

        public static List<string> EnumDesktopNames(Catalog catalog)
        {
            List<string> desktopNames = new List<string>();
            for (int i = 0; i < catalog.Count; i++)
            {
                string desktopName = String.Format("{0}{1,3:D3}",
                                         catalog.Name,
                                         i);
                desktopNames.Add(desktopName);
            }
            return desktopNames;
        }

        public bool Unsubscribe()
        {
            List<Machine> vmInfo = Machine.GetMachines(this.Name);

            return this.Unsubscribe(vmInfo);
        }


        public bool Unsubscribe(List<Machine> vmInfo)
        {
            bool deletedSubs = false;
            logger.Debug("Remove subscriptions for  " + this.Name);

            var allSubs = Subscription.GetSubscriptions();

            // assert that we have all the subscriptions
            // TODO: Change StartsWith, use CatalogName instead.
            var newSubs = from sub in allSubs where (!string.IsNullOrEmpty(sub.HostName) && sub.HostName.StartsWith(this.Name) && !sub.State.Equals("EXPIRED")) select sub;
            var matches = newSubs.Count();

            if (matches != vmInfo.Count)
            {
                logger.Error("Miss match: " + vmInfo.Count + " machines in catalog and " + matches + " subscriptions: ");
            }

            foreach (var machine in vmInfo)
            {
                var oldSubs = from sub in newSubs where sub.HostName.Equals(machine.MachineName) select sub;
                
                // assert
                matches = oldSubs.Count();
                if (matches != 1)
                {
                    logger.Error("Found " + matches + " for the desktop " + machine.MachineName);
                }
                if (matches == 0)
                {
                    continue;
                }

                Subscription.Delete(oldSubs.First());
                deletedSubs = true;
            }

            return deletedSubs;
        }

        public static void AddMachineToCatalog(Catalog newCat)
        {
            try
            {
                var jsonEquiv = Newtonsoft.Json.JsonConvert.SerializeObject(newCat);
                logger.Info("Adding Machine to Catalog corresponding to " +
                            Newtonsoft.Json.JsonConvert.ToString(jsonEquiv));
                Dictionary<string, object> psargs = new Dictionary<string, object>();


                psargs.Add("ddcAddress", DT2.Properties.Settings.Default.XenDesktopAdminAddress);
                psargs.Add("catalogName", newCat.Name);
                psargs.Add("newDesktopCount", newCat.Count);

                // Parameters required to setup the corresponding desktopgroup
                psargs.Add("desktopGrpName", newCat.Name + ScriptNames.DesktopGroupSuffix);

                var poshScript =
                    new PsWrapper(Path.Combine(DT2.Properties.Settings.Default.PowerShellScriptsFolder,
                        ScriptNames.AddToDesktopGroupScript));
                var jsonEquiv2 = Newtonsoft.Json.JsonConvert.SerializeObject(psargs);
                logger.Info("Calling " + ScriptNames.AddToDesktopGroupScript + " with args: " + jsonEquiv2);

                var exToIgnore = "Citrix.Broker.Admin.SDK.SdkOperationException";
                poshScript.IgnoreExceptions.Add(exToIgnore);
                logger.Info("Ignoring exceptions of type " + exToIgnore +
                            " because the SDK generates them for Get-BrokerUser calls as part of normal operation.");

                // Enable Windows Authentication in ASP.NET *and* IIS to ensure User.Identity is a FormsIdentity
                LoginViewModel clientId =
                    LoginViewModel.JsonDeserialize(((FormsIdentity) HttpContext.Current.User.Identity).Ticket);
                // add the ndc context
                string ndcContext = log4net.NDC.Pop();
                log4net.NDC.Push(ndcContext);

                Task.Run(() => RunLongScript(poshScript, psargs, clientId, ndcContext, () =>
                {
                    EmailAdmin(newCat, clientId);
                }));
            }
            catch (Exception e)
            {
                var errMsg = e.Message;
                logger.Error(errMsg);
            }
        }

        public static void EmailAdmin(Catalog newCat, LoginViewModel clientId)
        {
            try
            {
                // Send an email to each of the users from the signed on admin
                var adminEmail = GetActiveDirectoryUserEmail(clientId.UserName, clientId);
                if (string.IsNullOrEmpty(adminEmail))
                {
                    logger.Error("No email for admin " + clientId.UserName);
                    return;
                }

                logger.Info("Email " + clientId.UserName + " to say that " + newCat.Name + " is done. ");
                var userAddr = new System.Net.Mail.MailAddress(adminEmail);
                var mailMsg = new System.Net.Mail.MailMessage();
                mailMsg.Subject = "Your " + newCat.Name + " desktop is ready!";
                mailMsg.Body = "Your " + newCat.Name + " desktop is available for use.    \nClick on the link below to access your desktop.  \n" +
                               DT2.Properties.Settings.Default.XenDesktopStoreFrontUrl.ToString();
                mailMsg.From = userAddr;
                mailMsg.CC.Add(adminEmail);

                // Build list of users to send email to.
                foreach (var user in newCat.Users)
                {
                    var userEmail = GetActiveDirectoryUserEmail(user, clientId);

                    if (string.IsNullOrEmpty(adminEmail))
                    {
                        logger.Error("No email for desktop group user " + user);
                        continue;
                    }

                    logger.Debug("Alert to be sent to " + userEmail + " for " + user);
                    mailMsg.To.Add(userEmail);
                }
                logger.Debug("Sending alert for catalog " + newCat.Name);
                Utils.Utils.SendEmail(mailMsg);
            }
            catch (Exception e)
            {
                var errMsg = "Could not email desktop group creation alert";
                logger.Error(errMsg, e);
           }
        }

        private static void RunLongScript(PsWrapper script, Dictionary<string, object> psargs, LoginViewModel clientId,
            string ndcContext,
            Action callAction = null)
        {
            using (log4net.NDC.Push(ndcContext))
            {
                try
                {
                    script.RunPowerShell(psargs, clientId);
                    logger.Info("Completed RunPowerShell ");
                    if (callAction != null)
                    {
                        logger.Info("Completed RunPowerShell, and calling callAction");
                        callAction();
                    }
                }
                catch (Exception e)
                {
                    var errMsg = e.Message;
                    logger.Error("In RunLongScript we saw exception with message " + errMsg);
                }
            }
        }

        public static void DeleteCatalog(string name)
        {
            try
            {
                List<Catalog> catToDelList = Catalog.GetCatalog(name);
                // assert
                if (catToDelList.Count != 1)
                {
                    logger.Error("Problem deleting catalog " + name + ", because there are " + catToDelList.Count +
                                 " catalogs of that name");
                }
                var catToDel = catToDelList.First();
                string script = ScriptNames.DeleteDesktopGroupScript;
                logger.Info("Deleting Catalog corresponding with name " + name);
                Dictionary<string, object> psargs = new Dictionary<string, object>();
                psargs.Add("ddcAddress", DT2.Properties.Settings.Default.XenDesktopAdminAddress);
                psargs.Add("catalogName", name);
                psargs.Add("desktopGrpName", name + ScriptNames.DesktopGroupSuffix);
                var jsonEquiv2 = Newtonsoft.Json.JsonConvert.SerializeObject(psargs);
                logger.Info("Calling " + script + " with args: " + jsonEquiv2);
                var poshScript =
                    new PsWrapper(Path.Combine(DT2.Properties.Settings.Default.PowerShellScriptsFolder, script));
                poshScript.IgnoreExceptions.Add(PoshSdkConsts.PartialDataException);

                LoginViewModel clientId =
                    LoginViewModel.JsonDeserialize(((FormsIdentity) HttpContext.Current.User.Identity).Ticket);
                string ndcContext = log4net.NDC.Pop();
                log4net.NDC.Push(ndcContext);

                // Remove subscriptions for the desktops before the machine list disappears
                catToDel.Unsubscribe();

                Task.Run(() => RunLongScript(poshScript, psargs, clientId, ndcContext));
            }
            catch (Exception e)
            {
                var errMsg = e.Message;
                logger.Error(errMsg);
            }
        }


        /// <summary>
        /// Identifier for DesktopType taken from powershell SDK, see AllocationType under 
        /// http://support.citrix.com/proddocs/topic/citrix-broker-admin-v2-xd75/new-brokercatalog-xd75.html
        /// </summary>
        public static List<XenDesktopInventoryItem> DesktopTypeList
        {
            get
            {
                var list = new List<XenDesktopInventoryItem>();
                // TODO: add back commented out catalog types when these have passed QA.
                list.Add(new XenDesktopInventoryItem
                {
                    Id = VirtualDesktopsPooled,
                    Name = VirtualDesktopsPooled
                });
                list.Add(new XenDesktopInventoryItem
                {
                    Id = VirtualDesktopsDedicated,
                    Name = VirtualDesktopsDedicated
                });
                list.Add(new XenDesktopInventoryItem
                {
                    Id = PublishedDesktops,
                    Name = PublishedDesktops
                });
                //list.Add(new XenDesktopInventoryItem
                //{
                //    Id = VirtualDesktopsPooledStatic,
                //    Name = VirtualDesktopsPooledStatic
                //});
                //list.Add(new XenDesktopInventoryItem
                //{
                //    Id = VirtualDesktopsPersonalVDisk,
                //    Name = VirtualDesktopsPersonalVDisk
                //});
                return list;
            }
        }

        public const string VirtualDesktopsPooled = "Pooled Random";
        public const string VirtualDesktopsPooledStatic = "Pooled Static";
        public const string VirtualDesktopsDedicated = "Dedicated";
        public const string VirtualDesktopsPersonalVDisk = "Pooled with Personal vDisk";
        public const string PublishedDesktops = "Hosted Shared";


        /// <summary>
        /// Converts DaaS values for DesktopType to XenDesktop values.
        /// TODO: update for new desktop types
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        /// <remarks>
        /// PublishedDesktops and VirtualDesktopsPooled -> "Random"
        /// VirtualDesktopsDedicated -> "Permanent"
        /// </remarks>
        public static string GetXenDesktopDesktopAllocationType(string desktopType)
        {
            switch (desktopType)
            {
                case PublishedDesktops:
                case VirtualDesktopsPooled:
                    return "Random";
            }

            return "Permanent";
        }

        /// <summary>
        /// TODO: update for new desktop types
        /// </summary>
        /// <param name="allocationType"></param>
        /// <param name="sessionSupport"></param>
        /// <returns></returns>
        public static string GetXenDesktopDesktopSessionSupport(string desktopType)
        {
            switch (desktopType)
            {
                case PublishedDesktops:
                    return "MultiSession";
            }

            return "SingleSession";
        }

        /// <summary>
        /// TODO: update for new desktop types
        /// </summary>
        /// <param name="allocationType"></param>
        /// <param name="sessionSupport"></param>
        /// <returns></returns>
        private static string GetXenDesktopDesktopPersistUserChanges(string desktopType)
        {
            switch (desktopType)
            {
                case PublishedDesktops:
                    return "Discard";
            }

            return "Onlocal";
        }

        /// <summary>
        /// TODO: update for new desktop types
        /// </summary>
        /// <param name="allocationType"></param>
        /// <param name="sessionSupport"></param>
        /// <returns></returns>
        private static string GetXenDesktopDesktopType(string allocationType, string sessionSupport)
        {
            if (sessionSupport == "MultiSession")
            {
                return PublishedDesktops;
            }
            else if (allocationType == "Static")
            {
                return VirtualDesktopsDedicated;
            }
            return VirtualDesktopsPooled;
        }

        private static string GetXenDesktopDesktopCleanOnBoot(string desktopType)
        {
            switch (desktopType)
            {
                case PublishedDesktops:
                    return "True";
            }

            return null;
        }

        public static List<XenDesktopInventoryItem> GetActiveDirectoryUsers()
        {
            var ldapPath = DT2.Properties.Settings.Default.LdapPath;
            var users = new List<XenDesktopInventoryItem>();
            try
            {
                // Search for all users in domain
                // see http://msdn.microsoft.com/en-us/library/ms180885(v=vs.80).aspx
                //
                //this is the connection to your active directory
                DirectoryEntry entry = new DirectoryEntry(ldapPath);
                // Create a DirectorySearcher object.
                DirectorySearcher mySearcher = new DirectorySearcher(entry);
                // Create a SearchResultCollection object to hold a collection of SearchResults
                // returned by the FindAll method.
                SearchResultCollection searchResult = mySearcher.FindAll();

                foreach (SearchResult item in searchResult)
                {
                    if (item.Properties["samaccountname"].Count == 0)
                    {
                        continue;
                    }

                    int samaccounttype = (int) item.Properties["samaccounttype"][0];
                    if (samaccounttype != 0x30000000 && samaccounttype != 0x10000000)
                    {
                        continue;
                    }

                    var acctName = (string) item.Properties["samaccountname"][0];
                    if (UsersSkipList.Contains(acctName))
                    {
                        continue;
                    }

                    var user = new XenDesktopInventoryItem
                    {
                        Id = acctName,
                        Name = acctName
                    };
                    users.Add(user);
                }
                users.Sort(new Comparison<XenDesktopInventoryItem>(XenDesktopInventoryItem.Compare));
            }
            catch (System.Exception ex)
            {
                var errmsg = "Problem searching for users in " + ldapPath + "  Details: " + ex.Message;
                logger.Error(errmsg);
            }
            return users;
        }

        public static List<XenDesktopInventoryItem> GetActiveDirectoryUsers(string query)
        {
            var users = new List<XenDesktopInventoryItem>();
            var grps = new List<XenDesktopInventoryItem>();
            var usersFinal = new List<XenDesktopInventoryItem>();

            LoginViewModel clientId =
                LoginViewModel.JsonDeserialize(((FormsIdentity)HttpContext.Current.User.Identity).Ticket);
            logger.Debug("Find users and groups in domain of " + clientId.UserName);

            string searchDomain = clientId.DomainName;

            try
            {
                PrincipalContext adController = new PrincipalContext(ContextType.Domain, searchDomain, clientId.UserName, clientId.Password );

                UserPrincipal user = new UserPrincipal(adController);
                user.SamAccountName = query;
                PrincipalSearcher usrSrch = new PrincipalSearcher(user);
                PrincipalSearchResult<Principal> userSearchResult = usrSrch.FindAll();
                logger.Debug("Search for users netted " + userSearchResult.Count() + " results.");

                GroupPrincipal grp = new GroupPrincipal(adController);
                grp.SamAccountName = query;
                PrincipalSearcher grpSrch = new PrincipalSearcher(grp);
                PrincipalSearchResult<Principal> grpSearchResult =grpSrch.FindAll();
                logger.Debug("Search for groups netted " + grpSearchResult.Count() + " results.");

                int grpCount = 20;
                foreach (var currGrp in grpSearchResult)
                {
                    if (UsersSkipList.Contains(currGrp.SamAccountName))
                    {
                        continue;
                    }

                    var userRsrc = new XenDesktopInventoryItem
                    {
                        Id = searchDomain + "\\" + currGrp.SamAccountName,
                        Name = searchDomain + "\\" + currGrp.SamAccountName + "(group)"
                    };

                    grps.Add(userRsrc);

                    grpCount--;
                    if (grpCount < 0)
                    {
                        break;
                    }
                }
                grps.Sort(new Comparison<XenDesktopInventoryItem>(XenDesktopInventoryItem.Compare));

                int userCount = 100;
                foreach (var currUser in userSearchResult)
                {
                    if (UsersSkipList.Contains(currUser.SamAccountName))
                    {
                        continue;
                    }

                    var userRsrc = new XenDesktopInventoryItem
                        {
                            Id = searchDomain + "\\" + currUser.SamAccountName,
                            Name = searchDomain + "\\" + currUser.SamAccountName
                        };

                    users.Add(userRsrc);

                    userCount--;
                    if (userCount < 0)
                    {
                        break;
                    }
                }

                users.Sort(new Comparison<XenDesktopInventoryItem>(XenDesktopInventoryItem.Compare));
                usersFinal = grps.Concat(users).ToList();
            }
            catch (System.Exception ex)
            {
                var errmsg = "Problem searching for users in " + searchDomain + "  Details: " + ex.Message;
                logger.Error(errmsg);
                logger.Error(ex);
            }
            return usersFinal;
        }

        // TODO: update to search in domain of logged-in user
        public static String GetActiveDirectoryUserEmail(string domainQualifiedSAMAccountName, LoginViewModel clientId)
        {
            var splitUserName = domainQualifiedSAMAccountName.Split(new char[] { '\\' });
            var samAccountName = splitUserName[splitUserName.Length - 1];
            String email = null;

            try
            {
                logger.Debug("Find users and groups in domain of " + clientId.UserName);
                PrincipalContext adController = new PrincipalContext(ContextType.Domain, clientId.DomainName, clientId.UserName, clientId.Password);


                // Could be a user or a group:
                UserPrincipal user = new UserPrincipal(adController);
                user.SamAccountName = samAccountName;
                PrincipalSearcher usrSrch = new PrincipalSearcher(user);
                Principal userSearchResult = usrSrch.FindOne();

                if (userSearchResult == null)
                {
                    GroupPrincipal grp = new GroupPrincipal(adController);
                    grp.SamAccountName = samAccountName;
                    PrincipalSearcher grpSrch = new PrincipalSearcher(grp);
                    userSearchResult = grpSrch.FindOne();
                }

                if (userSearchResult == null)
                {
                    logger.Error("Could not find Principal in AD for " + domainQualifiedSAMAccountName);
                    return email;
                }

                DirectoryEntry dirEntry = (DirectoryEntry)userSearchResult.GetUnderlyingObject();
                // TODO:  is this property guaranteed to be present, e.g. if I create a user called donall, will the email property still be present?
                email = dirEntry.Properties["mail"] == null ? null : (string)(dirEntry.Properties["mail"].Value);

                if (email == null)
                {
                    logger.Info("No email for user" + domainQualifiedSAMAccountName);
                }
            }
            catch (System.Exception ex)
            {
                var errmsg = "Problem searching for users. Details: " + ex.Message;
                logger.Error(errmsg);
            }
            return email;
        }

        private static List<string> usersSkipList;

        public static List<string> UsersSkipList 
        {
            get {
                if (null == usersSkipList)
                {
                    var redListUsers = new string[]
                    {
                        "krbtgt",
                        "Domain Computers",
                        "Domain Controllers",
                        "Schema Admins",
                        "Group Policy Creator Owners",
                        "Read-only Domain Controllers",
                        "DnsUpdateProxy",
                        "Enterprise Read-only Domain Controllers",
                        "Cloneable Domain Controllers",
                        "Protected Users",
                        "Certificate Service DCOM Access",
                        "Cryptographic Operators",
                        "Distributed COM Users",
                        "Event Log Readers",
                        "Hyper-V Administrators",
                        "IIS_IUSRS",
                        "Network Configuration Operators",
                        "Performance Log Users",
                        "Performance Monitor Users",
                        "Print Operators",
                        "RDS Endpoint Servers",
                        "RDS Management Servers",
                        "RDS Remote Access Servers",
                        "Replicator",
                        "WinRMRemoteWMIUsers__",
                        "Allowed RODC Password Replication Group",
                        "Backup Operators",
                        "Cert Publishers",
                        "Denied RODC Password Replication Group",
                        "Incoming Forest Trust Builders",
                        "Pre-Windows 2000 Compatible Access",
                        "RAS and IAS Servers",
                        "Remote Management Users",
                        "Terminal Server License Servers",
                        "Users",
                        "Windows Authorization Access Group",
                        "Access Control Assistance Operators",
                        "DnsAdmins",    
                        "Server Operators"
                    };                    usersSkipList = redListUsers.ToList<string>();
                }
                return usersSkipList;
            }
        }
    }
}