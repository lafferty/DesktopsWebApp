using System;
using System.Linq;
using System.Security.Cryptography;
using System.Web.Mvc.Html;
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
using Newtonsoft.Json;

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
            MailMessage mailMessage = new MailMessage("desktopwebapp@citrix.com", "donal.lafferty@citrix.com", "Test", "test transmission");
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
            // See Settings for activating mock data 

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
            try
            {
                var list = DT2.Models.XenDesktopInventoryItem.GetServiceOfferingList();
            }            
            catch (Exception ex)
            {
                string test = ex.Message;
                Assert.Fail();
            }

        }

        [TestMethod]
        public void TestUserList()
        {
            var list = DT2.Models.Catalog.GetActiveDirectoryUsers();
        }



        [TestMethod]
        public void TestProductBundleJsonParserCode()
        {
            dynamic response = JsonConvert.DeserializeObject(ProductBundle.SampleCatalogJson2);
            var decodedBundles = DT2.Models.ProductBundle.ParseJson(response);
        }

        [TestMethod]
        public void TestSubscriptionJsonParser()
        {
            dynamic response = JsonConvert.DeserializeObject(Subscription.SampleJson);
            DT2.Models.Subscription.ParseArrayJson(response);
        }

            [TestMethod]
        public void ListProductBundles()
        {

            var result = ProductBundle.GetBundles();
            Assert.IsNotNull(result, "Expecting a Subscription object on success");
        }

        [TestMethod]
        public void TestListSubscriptions()
        {
            List<Subscription> allSubs = Subscription.GetSubscriptions();
            Assert.IsTrue(allSubs.Count > 0, "Expecting a Subscription object on success");

        }

        [TestMethod]
        public void TestCreateSubscription()
        {
            string productBundleId = "19";

            var result = Subscription.Create(productBundleId, "TestGrp001", "TestGrp");
            Assert.IsNotNull(result, "Expecting a Subscription object on success");

            var allSubs = Subscription.GetSubscriptions();
            var newSubs = from sub in allSubs where sub.Uuid.Equals(result.Uuid) select sub ;
            var matches = newSubs.Count();
            Assert.IsTrue(matches == 1, "System should report the subscription just created");

        }

        [TestMethod]
        public void TestDeleteSubscription()
        {
            string productBundleId = "19";

            var result = Subscription.Create(productBundleId, "TestGrp001", "TestGrp");
            Assert.IsNotNull(result, "Expecting a Subscription object on success");
            List<Subscription> allSubs = Subscription.GetSubscriptions();
            var newSubs = from sub in allSubs where sub.Uuid.Equals(result.Uuid) select sub;
            int matches = newSubs.Count();
            Assert.IsTrue(matches == 1, "System should not report a deleted subscription");
            var newSub = newSubs.First();
            Assert.IsTrue(newSub.State.Equals("NEW") || newSub.State.Equals("ACTIVE"), 
                "New subscription should be NEW or ACTIVE");
            
            Subscription.Delete(result);


            allSubs = Subscription.GetSubscriptions();
            newSubs = from sub in allSubs where sub.Uuid.Equals(result.Uuid) select sub;
            matches = newSubs.Count();
            Assert.IsTrue(matches == 1, "Deleting subscription should leave it in place, albeit with a different state.");
            var newSubPrime = newSubs.First();
            Assert.IsTrue(newSubPrime.Uuid.Equals(newSub.Uuid),
                "Subscription should be unchanged");
            Assert.IsTrue(newSubPrime.State.Equals("EXPIRED"),
                "New subscription should be EXPIRED");
        }

        [TestMethod]
        public void TestAttachSubscriptionToVm()
        {
            string productBundleId = "19";

            var result = Subscription.Create(productBundleId, "TestGrp001", "TestGrp");
            Assert.IsNotNull(result, "Expecting a Subscription object on success");

            //List<Subscription> allSubs = Subscription.GetSubscriptions();
            //var newSubs = from sub in allSubs where sub.Uuid.Equals(result.Uuid) select sub;
            List<Subscription> newSubsList = Subscription.GetSubscriptions(result.Uuid);
            int matches = newSubsList.Count;
            Assert.IsTrue(matches == 1, "System should not report a deleted subscription");
            var newSub = newSubsList.First();
            Assert.IsTrue(newSub.State.Equals("NEW") || newSub.State.Equals("ACTIVE"),
                "New subscription should be NEW or ACTIVE");

            var machine = SampleMachines;

            Subscription.WaitForSubscriptionsToBeActive(newSubsList);

            Subscription revisedSub = null;
            try
            {
                // Use CloudStack API to get identifier for VM
                revisedSub = newSub.Attach(machine[0].VmId, machine[0].MachineName);
            }
            catch (Exception ex)
            {
                logger.Error("Failed to attach subscription ot a VM ", ex);
            }
            newSubsList = Subscription.GetSubscriptions(result.Uuid);

            Subscription.Delete(result);
            List<Subscription> allSubs = Subscription.GetSubscriptions();
            var newSubs = from sub in allSubs where sub.Uuid.Equals(result.Uuid) select sub;
            matches = newSubs.Count();
            Assert.IsTrue(matches == 1, "Deleting subscription should leave it in place, albeit with a different state.");
            var newSubPrime = newSubs.First();
            Assert.IsTrue(newSubPrime.Uuid.Equals(newSub.Uuid),
                "Subscription should be unchanged");
            Assert.IsTrue(newSubPrime.State.Equals("EXPIRED"),
                "New subscription should be EXPIRED");

            Assert.IsNotNull(revisedSub, "Could not attach VM to a subscription");
        }

        private static List<Machine> SampleMachines
        {
            get
            {
                List<Machine> sample = new List<Machine>();
                sample.Add(new Machine() { MachineName = "July23TestA", VmId = "61f59b17-5481-4521-9052-89498c7ca333" });
                return sample;
            }
        }

        [TestMethod]
        public void TestCatalogSubscribe()
        {
            Catalog cat = new Catalog() {Name = SampleMachines[0].MachineName, ProductBundleCode = "19", Count = 1};
            bool keepCleaning = true;
            do
            {
                keepCleaning = cat.Unsubscribe(SampleMachines);
            } while (keepCleaning);

            var catSubs = cat.Subscribe(SampleMachines);
            var allSubs = Subscription.GetSubscriptions();
            var newSubs = from sub in allSubs where (!string.IsNullOrEmpty(sub.HostName) && sub.HostName.StartsWith(cat.Name) && !sub.State.Equals("EXPIRED")) select sub;
            var matches = newSubs.Count();
            Assert.IsTrue(matches == cat.Count, "System should report the subscription just created");

            // NB: you should delete the test subscriptions, and you should add this step to catalog delete
            bool cleanedUp = cat.Unsubscribe(SampleMachines);
            Assert.IsTrue(cleanedUp, "System should have had to delete at least one subscription");
        }

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
