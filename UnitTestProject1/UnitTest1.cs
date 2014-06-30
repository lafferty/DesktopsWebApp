using System;
using DT2.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using log4net;
using System.Reflection;
using System.IO;
using DT2.Models;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using DT2.Controllers;

namespace UnitTestProject1
{
    [TestClass]
    public class UnitTest1
    {
        static public ILog logger;

        static UnitTest1()
        {
            // log4net has trouble finding the configuration.  We fix that here
            var logConfigFilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "app.config");
            log4net.Config.XmlConfigurator.ConfigureAndWatch(new FileInfo(logConfigFilePath));
            logger = LogManager.GetLogger(typeof(UnitTest1));
            logger.Info("Started Unit test Service");
        }

        [TestMethod]
        public void TestUserQuery()
        {
            try
            {
                var test = DT2.Models.Catalog.GetActiveDirectoryUsers("*");
                logger.Info("'*' returned " + test.Count + " items.");

            }
            catch (Exception ex)
            {
                string test = ex.Message;
            }
        }


        [TestMethod]
        public void TestMailSend()
        {
            MailMessage mailMessage = new MailMessage("clouddesktop@citrix.com", "donal.lafferty@citrix.com", "Test", "test transmission");
            try
            {
                DT2.Utils.Utils.SendEmail(mailMessage);
            }
            catch (Exception ex)
            {
                string test = ex.Message;
            }
        }

        [TestMethod]
        public void TestGetTemplates()
        {
            // Setup mock data 


            try
            {
                List<Template> templateList = Template.GetTemplates(ServicesController.CloudStackClient);
                Assert.IsTrue(templateList.Count > 0, "Should have at least one template");
            }
            catch (Exception ex)
            {
                string test = ex.Message;
                Assert.Fail();
            }
        }

        [TestMethod]
        public void TestGetCatalogs()
        {
            //List<Catalog> result = new List<Catalog>();
            DT2.Models.Catalog.GetCatalogs();
        }

        //[TestMethod]
        //public void TestGetCatalogs()
        //{
        //    List<Catalog> result = new List<Catalog>();
        //    DT2.Models.Catalog.GetCatalogs(null, result);
        //    DT2.Models.Catalog.GetDesktopGroupDetails(null, result)
        //}


        [TestMethod]
        public void TestGetCatalogDetails()
        {
           DT2.Models.Machine.GetMachines("SampleDeskA");
        }

        [TestMethod]
        public void TestNetworkList()
        {
            var list = DT2.Models.XenDesktopInventoryItem.GetNetworkList();
        }

        [TestMethod]
        public void TestTemplateList()
        {
            var list = DT2.Models.Template.GetTemplates(DT2.Controllers.ServicesController.CloudStackClient);
        }

        [TestMethod]
        public void TestServiceOfferingList()
        {
            var list = DT2.Models.XenDesktopInventoryItem.GetServiceOfferingList();
        }

        [TestMethod]
        public void TestUserList()
        {
            var list = DT2.Models.Catalog.GetActiveDirectoryUsers();
        }

        //[TestMethod]
        //public void TestGetEmail()
        //{
        //    var email = DT2.Models.Catalog.GetActiveDirectoryUserEmail("donall");

        //    Assert.IsNotNull(email);
        //}

        [TestMethod]
        public void TestCreateCatalog()
        {
            // Sample needs updating when used against new deployment
            var newCat = new DT2.Models.Catalog()
            { 
                ComputeOffering = "f7dbb80c-f7a1-4fe4-b86a-9004360ac1e3",
                Count = 1,
                Description = "Unit Test Catalog",
                DesktopType = "Random",
                // Id = not availabe until after the catalog is created
                Name = "SamplePool",
                Network = "55bfaeb5-bbca-bf83-99e0-5a985c2ecfa5",                          // change to inventory item
                // ProvisioningSchemeId = not available until after the catalog is created
                Template = "902b218e-6281-cab3-accb-66a84a44309d",                          // change to inventory item
                Users = new string[] { "Administrator" }
            };

            // Sample needs updating when used against new deployment
            var xenCat = new DT2.Models.Catalog()
            {
                // ComputeOffering not used, for XenServer XD sets the CPU and RAM
                Count = 1,
                Description = "Unit Test Xen Catalog",
                DesktopType = "Random",
                // Id is available after the catalog is created
                Name = "SmplXenPool",
                Network = "XDHyp:\\Connections\\CC-SVR09\\main.network",
                // ProvisioningSchemeId is available after the catalog is created
                Template = "XDHyp:\\HostingUnits\\XenServerSample\\Windows8.1VDA.vm",
                Users = new string[] { "Administrator" }
            };

            var csCat = new DT2.Models.Catalog()
            {
                ComputeOffering = "XDHyp:\\HostingUnits\\CloudPlatformHost\\Power User.serviceoffering",
                Count = 1,
                CountInUse = 0,
                Description = "Test Delete me",
                DesktopType = "Permanent",
                Name = "UTFeb05E",
                Network = "XDHyp:\\HostingUnits\\CloudPlatformHost\\Zone1.availabilityzone\\GuestSharedNetwork(172.16.200.0`/24).network",
                Template = "XDHyp:\\HostingUnits\\CloudPlatformHost\\Zone1.availabilityzone\\CloudDesktopVDA.template",
                Users = new string[] { "donall" }
            };
            DT2.Models.Catalog.CreateCatalog(csCat);
        }
    }
}
