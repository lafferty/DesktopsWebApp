using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Management.Automation;
using System.Web;
using System.Web.Security;
using DT2.Utils;
using log4net;

namespace DT2.Models
{
    /// <summary>
    /// Encapsulates inventory items, provides helpers for obtaining lists of items and their XenDesktop
    /// inventory item paths.
    /// </summary>
    public class XenDesktopInventoryItem
    {
        private static ILog logger = LogManager.GetLogger(typeof (Catalog));

        /// <summary>
        /// InventoryItem's path.  However, the field was defined before we settled on these semantics,
        /// and may be used elsewhere with different semantics.
        /// </summary>
        [Display(Name = "Id")]
        [DataType(DataType.Text)]
        public string Id { get; set; }

        [Required]
        [Display(Name = "Name")]
        [DataType(DataType.Text)]
        public string Name { get; set; }

        [Display(Name = "Uuid")]
        [DataType(DataType.Text)]
        public string Uuid { get; set; }


        public static int Compare(XenDesktopInventoryItem x, XenDesktopInventoryItem y)
        {
            return x.Name.CompareTo(y.Name);
        }

        /// <summary>
        /// Deprecated, use Template.GetTemplates() instead
        /// </summary>
        public static List<XenDesktopInventoryItem> GetTemplateList
        {
            get { return GetInventoryItems(ScriptNames.GetTemplatesScript, XenDesktopZonePath); }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// Faster when data retrieved directly through CSSDK
        /// </remarks>
        public static string GetTemplatePathFromName(string templatename)
        {
            return Path.Combine(XenDesktopZonePath, templatename + ".template");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// Faster when data retrieved directly through CSSDK
        /// </remarks>
        public static List<XenDesktopInventoryItem> GetServiceOfferingList()
        {
            var result = new List<XenDesktopInventoryItem>();

            if (DT2.Properties.Settings.Default.TestDisableServiceOfferingGet)
            {
                result.Add(new XenDesktopInventoryItem() { Name = "TinyInstance", Uuid = "f1a234ae-1d5a-4fbc-817f-40c0c65e4e32", Id = @"XDHyp:\HostingUnits\CloudPlatformHost\TinyInstance.serviceoffering" });
                result.Add(new XenDesktopInventoryItem() { Name = "SmallInstance", Uuid = "68416055-1d04-4e30-a290-740b8871fab0", Id = @"XDHyp:\HostingUnits\CloudPlatformHost\SmallInstance.serviceoffering" });
                result.Add(new XenDesktopInventoryItem() { Name = "MediumInstance", Uuid = "9ad659d8-fe83-409f-8f65-2af4e4c7efb9", Id = @"XDHyp:\HostingUnits\CloudPlatformHost\MediumInstance.serviceoffering" });
                result.Add(new XenDesktopInventoryItem() { Name = "LargeInstance", Uuid = "567c52c9-0e1e-4def-ac49-396d9f1c2c98", Id = @"XDHyp:\HostingUnits\CloudPlatformHost\LargeInstance.serviceoffering" });
                return result;
            }
            try
            {
                var psNets = InvokeScript(ScriptNames.GetServiceOfferings, XenDesktopHostingUnitPath);

                // Use service offerings with DIaaS in name or description
                foreach (PSObject item in psNets)
                {
                    string desc = (string) item.Members["Description"].Value;
                    string name = (string)item.Members["Name"].Value;

                    if (!desc.Contains("DIaaS") && !name.Contains("DIaaS"))
                    {
                        continue;
                    }
                    string uuid = (string)item.Members["Id"].Value;
                    string id = (string)item.Members["FullPath"].Value;
                    var newRsrc = new XenDesktopInventoryItem()
                    {
                        Name = name,
                        Id = id,
                        Uuid = uuid
                    };

                    logger.Debug("Adding " + name + " Id " + id + " to theb list of resources");
                    result.Add(newRsrc);
                } // End foreach.

                if (result.Count == 0)
                {
                    foreach (PSObject item in psNets)
                    {
                        string name = (string) item.Members["Name"].Value;
                        string uuid = (string)item.Members["Id"].Value;
                        string id = (string)item.Members["FullPath"].Value;
                        var newRsrc = new XenDesktopInventoryItem()
                        {
                            Name = name,
                            Id = id,
                            Uuid = uuid
                        };

                        logger.Debug("Adding " + name + " Id " + id + " to the list of resources");
                        result.Add(newRsrc);
                    } // End foreach.
                }
            }
            catch (Exception e)
            {
                var errMsg = e.Message;
                logger.Error(errMsg);
            }
            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// Faster when data retrieved directly through CSSDK
        /// </remarks>
        public static List<XenDesktopInventoryItem> GetNetworkList()
        {
            return GetInventoryItems(ScriptNames.GetNetworksScript, XenDesktopZonePath);
        }

        /// <summary>
        /// Beware that security group items exist even for zones that do not support (and allow) security groups
        /// specification when creating a VM.
        /// </summary>
        public static List<XenDesktopInventoryItem> GetSecurityGroupList()
        {
            return GetInventoryItems(ScriptNames.GetSecurityGroupsScript, XenDesktopHostingUnitPath);
        }


        public static string XenDesktopZonePath
        {
            get
            {
                return Path.Combine(XenDesktopHostingUnitPath,
                    DT2.Properties.Settings.Default.XenDesktopAvailabilityZone + ".availabilityzone");
            }
        }

        public static string XenDesktopHostingUnitPath
        {
            get
            {
                return Path.Combine("XDHyp:\\", "HostingUnits",
                    DT2.Properties.Settings.Default.XenDesktopHostingUnitName);
            }
        }

        public static List<XenDesktopInventoryItem> GetInventoryItems(string scriptName, string xenDestkopPath)
        {
            var result = new List<XenDesktopInventoryItem>();
            try
            {
                var psNets = InvokeScript(scriptName, xenDestkopPath);

                foreach (PSObject item in psNets)
                {
                    string name = (string)item.Members["Name"].Value;
                    string id = (string)item.Members["FullPath"].Value;
                    var newRsrc = new XenDesktopInventoryItem()
                    {
                        Name = name,
                        Id = id
                    };

                    logger.Debug("Adding " + name + " Id " + id + " to the list of resources");
                    result.Add(newRsrc);
                } // End foreach.
            }
            catch (Exception e)
            {
                var errMsg = e.Message;
                logger.Error(errMsg);
            }
            return result;
        }

        private static Collection<PSObject> InvokeScript(string scriptName, string xenDestkopPath)
        {
            Dictionary<string, object> psargs = new Dictionary<string, object>();
            psargs.Add("path", xenDestkopPath);
            var listNetsScript = new PsWrapper(Path.Combine(DT2.Properties.Settings.Default.PowerShellScriptsFolder, scriptName));
            LoginViewModel clientId = LoginViewModel.JsonDeserialize(((FormsIdentity)HttpContext.Current.User.Identity).Ticket);
            
            var psNets = listNetsScript.RunPowerShell(psargs, clientId);
            return psNets;
        }

        internal static bool ZoneSupportsSecurityGroups()
        {
            try
            {
                // assert
                var zoneItems = InvokeScript(ScriptNames.GetItemScript, XenDesktopZonePath);

                if (zoneItems.Count > 1 && zoneItems.Count < 1)
                {
                    var errMsg = "Could not find a single item for " + XenDesktopZonePath + 
                                    " (found " + zoneItems.Count + ")";
                    logger.Error(errMsg);

                    // TODO: may want to throw
                    return false;
                }

                PSObject item = zoneItems[0];
                var supportSecGrps = (bool) item.Members["SupportsSecurityGroups"].Value;

                return supportSecGrps;
            }
            catch (Exception e)
            {
                var errMsg = e.Message;
                logger.Error(errMsg);
            }
            return false;
        }
    }
}