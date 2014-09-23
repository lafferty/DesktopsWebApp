using System.Security.Principal;
using System.Web.Security;
using DT2.Models;
using log4net;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Web;
using DT2.Utils;

namespace DT2.Models
{
    /// <summary>
    /// Uses XenDesktop Posh SDK to provide fine grained details for a specific desktop pool
    /// Implemented as index that combines machines and users
    /// </summary>
    /// <remarks>
    /// online help: http://support.citrix.com/proddocs/topic/xendesktop-71/cds-sdk-cmdlet-help.html 
    /// </remarks>
    public class Machine
    {
        private static ILog logger = LogManager.GetLogger(typeof(Machine));

        /// <summary>
        /// E.g. {SPONDULAS\Administrator}
        /// </summary>
        [Display(Name = "User")]
        [DataType(DataType.Text)] // TODO
        public String[] AssociatedUserNames{ get; set; }

        /// <summary>
        /// E.g. {CBP1_5}
        /// </summary>
        [Display(Name = "Capabilities")]
        [DataType(DataType.Text)]
        public String[] Capabilities { get; set; } // todo

        /// <summary>
        /// E.g. SampleDeskA
        /// </summary>
        [Display(Name = "CatalogName")]
        [DataType(DataType.Text)]
        public String CatalogName { get; set; }

        /// <summary>
        /// E.g. 1
        /// </summary>
        [Display(Name = "CatalogUid")]
        [DataType(DataType.Text)]
        public Int32 CatalogUid { get; set; }

        /// <summary>
        /// E.g. SampleDeskGrp
        /// </summary>
        [Display(Name = "Desktop Group Name")]
        [DataType(DataType.Text)]
        public String DesktopGroupName { get; set; }

        /// <summary>
        /// E.g. 1
        /// </summary>
        [Display(Name = "DesktopGroupUid")]
        [DataType(DataType.Text)]
        public Int32 DesktopGroupUid { get; set; }

        /// <summary>
        /// E.g. Private
        /// </summary>
        [Display(Name = "Desktop Kind")]
        [DataType(DataType.Text)]
        public String DesktopKind { get; set; }

        /// <summary>
        /// E.g. 1
        /// </summary>
        [Display(Name = "DesktopUid")]
        [DataType(DataType.Text)]
        public Int32 DesktopUid { get; set; }

        /// <summary>
        /// E.g. SampleDeskA01.spondulas.cloud
        /// </summary>
        [Display(Name = "DNSName")]
        [DataType(DataType.Text)]
        public String DNSName { get; set; }

        /// <summary>
        /// E.g. 2
        /// </summary>
        [Display(Name = "HypervisorConnectionUid")]
        [DataType(DataType.Text)]
        public Int32 HypervisorConnectionUid { get; set; }

        /// <summary>
        /// E.g. False
        /// </summary>
        [Display(Name = "Maintenance Mode?")]
        [DataType(DataType.Text)]
        public Boolean InMaintenanceMode { get; set; }

        /// <summary>
        /// E.g. SPONDULAS\SampleDeskA01
        /// </summary>
        [Display(Name = "Machine Name")]
        [DataType(DataType.Text)]
        public String MachineName { get; set; }

        /// <summary>
        /// E.g. OnLocal
        /// </summary>
        [Display(Name = "Persist User Changes")]
        [DataType(DataType.Text)]
        public String PersistUserChanges { get; set; }

        /// <summary>
        /// E.g. On
        /// </summary>
        [Display(Name = "Power State")]
        [DataType(DataType.Text)]
        public String PowerState { get; set; }

        /// <summary>
        /// E.g. Registered
        /// </summary>
        [Display(Name = "Registration State")]
        [DataType(DataType.Text)]
        public String RegistrationState { get; set; }

        /// <summary>
        /// E.g. 1
        /// </summary>
        [Display(Name = "Session Count")]
        [DataType(DataType.Text)]
        public Int32 SessionCount { get; set; }

        /// <summary>
        /// E.g. {Reset, Restart, Resume, Shutdown...}
        /// </summary>
        [Display(Name = "Supported Power Actions")]
        [DataType(DataType.Text)]
        public String[] SupportedPowerActions { get; set; }

        /// <summary>
        /// E.g. 5
        /// </summary>
        [Display(Name = "Id")]
        [DataType(DataType.Text)]
        public Int32 Uid { get; set; }


        [Display(Name = "HostVmId")]
        [DataType(DataType.Text)]
        public string VmId { get; set; }
        
        // TODO: are Catalogs available limited to this user?
        public static List<Machine> GetMachines(string catalogName)
        {
            var result = new List<Machine>();

            try
            {
                // Catalog query provides user-defined descriptions such as Name and Description
                Dictionary<string, object> psargs = new Dictionary<string, object>();
                var getDetailsScript = new PsWrapper(Path.Combine(DT2.Properties.Settings.Default.PowerShellScriptsFolder,ScriptNames.GetMachines));

                logger.Info("Getting the details for Catalog corresponding to " + catalogName );
                psargs.Add("catalogName", catalogName);
                var jsonEquiv = Newtonsoft.Json.JsonConvert.SerializeObject(psargs);
                logger.Info("Calling " + ScriptNames.GetMachines + " with args: " + jsonEquiv);

                getDetailsScript.IgnoreExceptions.Add(PoshSdkConsts.PartialDataException);

                LoginViewModel clientId =
                    LoginViewModel.JsonDeserialize(((FormsIdentity)HttpContext.Current.User.Identity).Ticket);
                var psCats = getDetailsScript.RunPowerShell(psargs, clientId);

                foreach (PSObject item in psCats)
                {
                    var newMachine = new Machine();
                    newMachine.AssociatedUserNames = (string[])(item.Members["AssociatedUserNames"].Value ?? new String[0]);
                    newMachine.Capabilities = (string[])(item.Members["Capabilities"].Value ?? new String[0]);
                    newMachine.CatalogName = (string)(item.Members["CatalogName"].Value ?? string.Empty);
                    newMachine.CatalogUid = (int)(item.Members["CatalogUid"].Value ?? string.Empty);
                    newMachine.DesktopGroupName = (string)(item.Members["DesktopGroupName"].Value ?? string.Empty);
                    newMachine.DesktopGroupUid = (int)(item.Members["DesktopGroupUid"].Value ?? string.Empty);
                    newMachine.DesktopKind = (item.Members["DesktopKind"].Value ?? string.Empty).ToString();
                    newMachine.DesktopUid = (int)(item.Members["DesktopUid"].Value ?? string.Empty);
                    newMachine.DNSName = (string)(item.Members["DNSName"].Value ?? string.Empty);
                    newMachine.HypervisorConnectionUid = (int)(item.Members["HypervisorConnectionUid"].Value ?? string.Empty);
                    newMachine.InMaintenanceMode = (bool)(item.Members["InMaintenanceMode"].Value ?? string.Empty);
                    newMachine.MachineName = (string)(item.Members["MachineName"].Value ?? string.Empty);
                    newMachine.PersistUserChanges = (item.Members["PersistUserChanges"].Value ?? string.Empty).ToString();
                    newMachine.PowerState = (item.Members["PowerState"].Value ?? string.Empty).ToString(); ;
                    newMachine.RegistrationState = (item.Members["RegistrationState"].Value ?? string.Empty).ToString();
                    newMachine.SessionCount = (int)(item.Members["SessionCount"].Value ?? string.Empty);
                    newMachine.SupportedPowerActions = (string[])(item.Members["SupportedPowerActions"].Value ?? new String[0]);
                    newMachine.Uid = (int)(item.Members["Uid"].Value ?? string.Empty);
                    newMachine.VmId = (string)(item.Members["HostedMachineId"].Value ?? string.Empty);

                    var newMachineJson = Newtonsoft.Json.JsonConvert.SerializeObject(newMachine);
                    logger.Info("Discovered Machine: " + newMachineJson);
                    result.Add(newMachine);
                } // End foreach.
            }
            catch (Exception e)
            {
                var errMsg = e.Message;
                logger.Error(errMsg);
            }

            return result;
        }

        internal static void Restart(string machineName)
        {
            try
            {
                Dictionary<string, object> psargs = new Dictionary<string, object>();
                var getDetailsScript = new PsWrapper(Path.Combine(DT2.Properties.Settings.Default.PowerShellScriptsFolder, ScriptNames.SetMachinePowerState));


                string powerAction = "Restart";
                logger.Info("Restarting machine " + machineName + " by sending poweraction " + powerAction);
                psargs.Add("ddcAddress", DT2.Properties.Settings.Default.XenDesktopAdminAddress);
                psargs.Add("machineName", machineName);
                psargs.Add("powerAction", powerAction);


                var jsonEquiv = Newtonsoft.Json.JsonConvert.SerializeObject(psargs);
                logger.Info("Calling " + ScriptNames.GetMachines + " with args: " + jsonEquiv);

                getDetailsScript.IgnoreExceptions.Add(PoshSdkConsts.PartialDataException);

                LoginViewModel clientId =
                    LoginViewModel.JsonDeserialize(((FormsIdentity)HttpContext.Current.User.Identity).Ticket);
                var psCats = getDetailsScript.RunPowerShell(psargs, clientId);
            }
            catch (Exception e)
            {
                var errMsg = e.Message;
                logger.Error(errMsg);
            }
        }

    }
}