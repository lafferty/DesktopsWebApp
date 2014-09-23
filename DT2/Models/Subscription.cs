using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Web.DynamicData;
using System.Web.Helpers;
using log4net;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Citrix.CPBM.Client.Sample;
using Microsoft.Ajax.Utilities;
using Microsoft.CSharp.RuntimeBinder;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DT2.Models
{
    /// <summary>
    /// Model for CPBM subcription limited to fields required for non-provisioned bundles.
    /// </summary>
    /// <example>
    /// Example JSON from BSS API: 
    ///  "subscription": {
    ///    "uuid": "47d8109b-50ca-4c97-9e69-10479f58aefe",
    ///    "activationDate": null,
    ///    "state": "NEW",
    ///    "productBundle": {
    ///      "name": "Silver Network Package",
    ///      "description": "This provisions a network with 10GB of Network Bytes Sent and 10GB of Network Bytes Received included per month.",
    ///      "code": "CCP-Dixon_SilverNetworkPackage"
    ///    },
    ///    "tenant": {
    ///      "uuid": "5b281e73-f7a2-4df6-a17e-85f32f61de28",
    ///      "name": "Finance Department",
    ///      "accountId": "AA000002",
    ///      "state": "ACTIVE"
    ///    },
    ///    "newSubscription": null,
    ///    "preAuthTransId": null,
    ///    "preAuthAmount": 0,
    ///    "configurationData": {},
    ///    "terminationDate": null
    ///  }
    ///}]    
    ///</example>
    public class Subscription
    {
        private static ILog logger = LogManager.GetLogger(typeof(Subscription));


        [Display(Name = "Uuid")]
        [DataType(DataType.Text)]
        public Guid Uuid { get; set; }

        [Display(Name = "State")]
        [DataType(DataType.Text)]
        public string State { get; set; }

        [Display(Name = "HostName")]
        [DataType(DataType.Text)]
        public string HostName { get; set; }

        [Display(Name = "CatalogName")]
        [DataType(DataType.Text)]
        public string CatalogName { get { return HostName.Substring(0, HostName.Length - 3); }}


        public ProductBundle Product { get; set; }

        /// <summary>
        /// Extracts bundle information, uses config file CPBM credentials.
        /// </summary>
        /// <returns></returns>
        public static Subscription Create(string productbundleid, string hostName, string catalogName)
        {
            var clientSession = new Client(
                DT2.Properties.Settings.Default.CPBMEndPoint,
                DT2.Properties.Settings.Default.CPBMApiKey,
                DT2.Properties.Settings.Default.CPBMSecretKey);

            return Create(clientSession, DT2.Properties.Settings.Default.CPBMServiceInstanceName, productbundleid, hostName, catalogName);
        }

        /// <summary>
        /// Extracts bundle information, uses config file CPBM credentials.
        /// </summary>
        /// <returns></returns>
        /// <remarks>
  // * Attach a resource to {@link Subscription} for particular uuid.
  // *
  // * @param subscriptionId
  // * @param resourceId
  // * @param resourceName
  // * @return
  // */
  //@RequestMapping(value = {
  //  "/subscriptions/{uuid}/attachResource"
  //}, method = RequestMethod.POST)
//  public Subscription attachResource(@PathVariable String uuid,
//      @RequestParam(value = "resourceId", required = true) String resourceId,
//      @RequestParam(value = "resourceName", required = true) String resourceName)

        ///</remarks>
        public Subscription Attach(String resourceId, String resourceName)
        {
            var clientSession = new Client(
                DT2.Properties.Settings.Default.CPBMEndPoint,
                DT2.Properties.Settings.Default.CPBMApiKey,
                DT2.Properties.Settings.Default.CPBMSecretKey);

            return Attach(clientSession, resourceId, resourceName);
        }

        public Subscription Attach(Client clientSession, string resourceId, string resourceName)
        {
            // assert
            if (this.Uuid == Guid.Empty)
            {
                string errMsg = "Cannot attach subscription with Empty Uuid";
                logger.Error(errMsg);
                throw new ArgumentNullException(errMsg);
            }

            string resourcePath = string.Format("/subscriptions/{0}/attachresource", this.Uuid);
            APIRequest request = new APIRequest(resourcePath, "PUT");

            // assert 
            if (string.IsNullOrEmpty(resourceId) )
            {
                string errMsg = "Non-null resourceId required for VM attach to succeed.";
                logger.Error(errMsg);
                throw new ArgumentNullException(errMsg);
            }

            // assert 
            if (string.IsNullOrEmpty(resourceName))
            {
                string errMsg = "Non-null resourceName required for VM attach to succeed.";
                logger.Error(errMsg);
                throw new ArgumentNullException(errMsg);
            }

            request.SetParameterValue("resourceid", resourceId);
            request.SetParameterValue("resourcename", resourceName);

            Subscription newSub = null;
            try
            {
                dynamic result = clientSession.SendRequest(request);
                dynamic newSubJson = result.subscription;
                string dbgTmp = result.ToString();
                logger.Debug("Attach subscription returned: " + dbgTmp);
                newSub = ParseSubscriptionJson(newSubJson);
            }
            catch (CPBMException ex)
            {
                String errMsg = "Exception creating subscription: " + ex.Message + ex.StackTrace;
                logger.Error(errMsg, ex);
            }

            return newSub;
        }


        public static void WaitForSubscriptionsToBeActive(List<Subscription> newSubs)
        {
            // Wait for subscriptions to become active
            // Pause until subscription becomes active
            int count = newSubs.Count();
            do
            {
                // Pause for subscriptions to update
                Thread.Sleep(500);

                var allSubs = Subscription.GetSubscriptions();

                // get list of Catalog's subscriptions by way of a join
                // Linq syntax is more readable than the lambdas' required for List.Join
                // see http://stackoverflow.com/a/5038416/939250
                var catSubs = from sub in allSubs
                              join newSub in newSubs
                                  on sub.Uuid equals newSub.Uuid
                              select sub;

                var unfinishedSubs = from sub in catSubs
                                     where sub.State == "NEW"
                                     select sub;

                count = unfinishedSubs.Count();
                //newSubs = from sub in allSubs where sub.State.Equals(result.Uuid) select sub;
                //newSub = newSubs.First();
                //subActive = newSub.State == "ACTIVE";
            } while (count != 0);
        }


        public static Subscription Create(Citrix.CPBM.Client.Sample.Client clientSession, string serviceInstance, string productbundleid, string hostName, string catalogName)
        {
            Subscription newSub = null;
            // Creation happens when you POST to /subscriptions
            APIRequest request = new APIRequest("/subscriptions", "POST");

            request.SetParameterValue("serviceinstanceuuid", serviceInstance);

            // Optional for our purposes, but for completeness it helps to know that the options are 
            // "VirtualMachine","Volume","Network"
            request.SetParameterValue("resourcetype", "VirtualMachine");

            if (string.IsNullOrEmpty(hostName))
            {
                hostName = String.Empty;
            }
            // configurationdata field is required.  Can be left as empty, but we add  but it can be left as an empty JSON string.
            string jsonConfig = @"{""hostName"":""" + hostName + @"""}";
//            string jsonConfig2 = @"{""displayName"":""" + catalogName + @"""}";
//            string jsonConfig = @"{""hostName"":""" + hostName + @""", ""displayName"":""" + catalogName + @"""}";

            // assert
            //dynamic confgTest = JsonConvert.DeserializeObject(jsonConfig);
            //if (!hostName.Equals((string)confgTest.hostName) || !catalogName.Equals((string)confgTest.displayName))
            //{
            //    var errMsg = "jsonConfig should be JSON formatted text.";
            //    var ex = new ArgumentException(errMsg);
            //    logger.Error(errMsg, ex);
            //    throw ex;
            //}

            request.SetParameterValue("configurationdata", jsonConfig);
//            request.SetParameterValue("configurationdata", jsonConfig2);

            // 
            request.SetParameterValue("provision", "false");  // bill for items already created, i.e. don't have CPBM provision

            // how will I cross reference a productbundle and it's "productbundleid" ?
            // TBA
            request.SetParameterValue("productbundleid", productbundleid);// "CCP-Dixon_MEDIUM_INSTANCE");


            // Q: What is the purpose of entitlements?
            try
            {
                dynamic result = clientSession.SendRequest(request);
                dynamic newSubJson = result.subscription;
                string dbgTmp = result.ToString();
                logger.Debug("Create subscription returned: " + dbgTmp);
                newSub = ParseSubscriptionJson(newSubJson);
            }
            catch (CPBMException ex)
            {
                String errMsg = "Exception creating subscription: " + ex.Message + ex.StackTrace;
                logger.Error(errMsg, ex);
            }

            // Cross reference?  
            return newSub;
        }

        public static List<Subscription> GetSubscriptions(Guid uuid)
        {
            var clientSession = new Client(
                DT2.Properties.Settings.Default.CPBMEndPoint,
                DT2.Properties.Settings.Default.CPBMApiKey,
                DT2.Properties.Settings.Default.CPBMSecretKey);

            return GetSubscriptions(clientSession, uuid);
        }

        public static List<Subscription> GetSubscriptions()
        {
            var clientSession = new Client(
                DT2.Properties.Settings.Default.CPBMEndPoint,
                DT2.Properties.Settings.Default.CPBMApiKey,
                DT2.Properties.Settings.Default.CPBMSecretKey);

            return GetSubscriptions(clientSession, Guid.Empty);
        }

        /// <summary>
        /// Returns all subscriptions regardless of the number.
        /// </summary>
        /// <param name="cpbmClient"></param>
        /// <returns></returns>
        /// <remarks>
        /// Separate method requried to select a specific subscriptions, e.g. GET /subscriptions/{uuid}
        /// Separate method required to delete a specific subscription, e.g. DELETE /subscriptions/{uuid}
        /// </remarks>
        public static List<Subscription> GetSubscriptions(Client cpbmClient, Guid uuid)
        {
            // Default position is to return a List of length zero.
            String resourcePath = "/subscriptions";

            if (uuid != Guid.Empty)
            {
                resourcePath = resourcePath + "/" + uuid.ToString();
            }
            APIRequest request = new APIRequest(resourcePath, "GET");

            List<Subscription> subs = new List<Subscription>();
            request.SetParameterValue("pagesize", int.MaxValue);
            request.SetParameterValue("page", "1");
            request.SetParameterValue("expand", "attachResource");

            try
            {
                logger.Debug("CPBM request " + request.ToJSON());
                dynamic result = cpbmClient.SendRequest(request);
                string tmpdbg = result.ToString();
                logger.Debug("CPBM response" + tmpdbg);
                if (uuid != Guid.Empty)
                {
                    subs = ParseObjectJson(result);
                }
                else
                {
                    subs = ParseArrayJson(result);
                }
            }
            catch (Exception ex)
            {
                String errMsg = "Exception retrieving subscriptions: " + ex.Message + ex.StackTrace;
                logger.Error(errMsg, ex);
            }

            return subs;
        }

        public static List<Subscription> ParseArrayJson(dynamic result)
        {
            List<Subscription> subList = new List<Subscription>();
            string jsonSerialisedResult = result.ToString();
            logger.Debug("Parsing array of Subscription objects: " + jsonSerialisedResult);

            // Don't use Linq until you need to select specific objects in a colletion based on a specific condition.

            // Exception handling v checking property exists examined here:
            // http://stackoverflow.com/a/20001358/939250
            //
            JArray subscriptions;
            try
            {
                subscriptions = (JArray)result.subscriptions;
            }
            catch (RuntimeBinderException e)
            {
                String errMsg = "Missing subscriptions array in " + result;
                logger.Error(errMsg, e);
                throw;
            }

            foreach (dynamic subJson in subscriptions)
            {
                Subscription subObj = ParseSubscriptionJson(subJson);

                logger.Debug("Added subscription uuid" + subObj.Uuid);
                subList.Add(subObj);
            }
            return subList;
        }

        public static List<Subscription> ParseObjectJson(dynamic result)
        {
            List<Subscription> subList = new List<Subscription>();
            string jsonSerialisedResult = result.ToString();
            logger.Debug("Parsing single Subscription objects: " + jsonSerialisedResult);

            // Don't use Linq until you need to select specific objects in a colletion based on a specific condition.

            // Exception handling v checking property exists examined here:
            // http://stackoverflow.com/a/20001358/939250
            //
            dynamic subJson;
            try
            {
                subJson = result.subscription;
                Subscription subObj = ParseSubscriptionJson(subJson);
                subList.Add(subObj);
            }
            catch (RuntimeBinderException e)
            {
                String errMsg = "Missing subscription object in " + result;
                logger.Error(errMsg, e);
                throw;
            }

            return subList;
        }

        public static Subscription ParseSubscriptionJson(dynamic newSubJson)
        {
            Subscription newSub = new Subscription();
            string tmp = newSubJson.ToString();
            newSub.Uuid = new Guid((String)(newSubJson.uuid));
            newSub.State = (String)(newSubJson.state);
            newSub.Product = new ProductBundle();
            newSub.HostName = (string)newSubJson.configurationData.hostName;

            try
            {
                newSub.Product = ProductBundle.ParseProductBundle(newSubJson);
            }
            catch (RuntimeBinderException e)
            {
                String errMsg = "No product bundle for subscription " + newSub.Uuid;
                logger.Debug(errMsg, e);
            }
            return newSub;
        }

        public static string SampleJson = @"
        {
  ""subscriptions"": [
    {
      ""uuid"": ""f66740fb-ec93-4c46-91f1-1fb6fb967850"",
      ""activationDate"": ""2014-02-18T22:23:17+00:00"",
      ""state"": ""EXPIRED"",
      ""productBundle"": {
        ""id"": ""5"",
        ""name"": ""Small CentOS Instance"",
        ""description"": ""This is a small CentOS instance (1 CPU@500MHz, 512MB RAM) charged at monthly rate."",
        ""code"": ""CCP-Dixon_Small_CentOS_Instance""
      },
      ""tenant"": {
        ""uuid"": ""5b281e73-f7a2-4df6-a17e-85f32f61de28"",
        ""name"": ""Finance Department"",
        ""accountId"": ""AA000002"",
        ""state"": ""ACTIVE""
      },
      ""newSubscription"": null,
      ""preAuthTransId"": null,
      ""preAuthAmount"": 0,
      ""configurationData"": {
        ""hostName"": ""SugarCRM"",
        ""displayName"": """",
        ""group"": """",
        ""diskOfferingUuid"": """",
        ""keypair"": """",
        ""diskSize"": """",
        ""keyboard"": """",
        ""networkIds"": ""736bd2c6-8d57-4765-bfb7-d411a22d43d8"",
        ""securitygroupIds"": """",
        ""userData"": """",
        ""ipAddress"": """",
        ""ipToNetworkList"": """",
        ""zoneUuid_name"": ""Advanced-zone"",
        ""templateUuid_name"": ""CentOS 5.6(64-bit) no GUI (XenServer)"",
        ""hypervisorType_name"": ""XenServer"",
        ""serviceOfferingUuid_name"": ""SmallInstance (1x128Mhz, 128MB)"",
        ""networkIds_name"": ""jiefeng-default Network"",
        ""zoneUuid"": ""c079a19f-7eb1-463c-b5bc-13d508584d5a"",
        ""templateUuid"": ""83801f54-9333-11e3-b0df-029358153595"",
        ""serviceOfferingUuid"": ""68416055-1d04-4e30-a290-740b8871fab0"",
        ""hypervisorType"": ""XenServer""
      },
      ""terminationDate"": ""2014-05-06T18:13:37+01:00""
    },
    {
      ""uuid"": ""401877d0-99eb-4f9b-8e18-5dfb772fc225"",
      ""activationDate"": ""2014-02-18T22:23:18+00:00"",
      ""state"": ""ACTIVE"",
      ""productBundle"": {
        ""id"": ""7"",
        ""name"": ""Tiny CentOS Instance"",
        ""description"": ""This is a medium CentOS instance (1 CPU@100MHz, 64MB RAM) charged at monthly rate."",
        ""code"": ""CCP-Dixon_TinyCentOSInstance""
      },
      ""tenant"": {
        ""uuid"": ""5b281e73-f7a2-4df6-a17e-85f32f61de28"",
        ""name"": ""Finance Department"",
        ""accountId"": ""AA000002"",
        ""state"": ""ACTIVE""
      },
      ""newSubscription"": null,
      ""preAuthTransId"": null,
      ""preAuthAmount"": 0,
      ""configurationData"": {
        ""hostName"": ""WordPress"",
        ""displayName"": """",
        ""group"": """",
        ""diskOfferingUuid"": ""270bade0-01a7-4c2e-bae2-47dd57c3b19f"",
        ""keypair"": """",
        ""diskSize"": """",
        ""keyboard"": """",
        ""networkIds"": ""736bd2c6-8d57-4765-bfb7-d411a22d43d8"",
        ""securitygroupIds"": """",
        ""userData"": """",
        ""ipAddress"": """",
        ""ipToNetworkList"": """",
        ""zoneUuid_name"": ""Advanced-zone"",
        ""templateUuid_name"": ""CentOS 5.6(64-bit) no GUI (XenServer)"",
        ""hypervisorType_name"": ""XenServer"",
        ""serviceOfferingUuid_name"": ""TinyInstance (1x100Mhz, 64MB)"",
        ""diskOfferingUuid_name"": ""Small : 5 GB"",
        ""networkIds_name"": ""jiefeng-default Network"",
        ""zoneUuid"": ""c079a19f-7eb1-463c-b5bc-13d508584d5a"",
        ""templateUuid"": ""83801f54-9333-11e3-b0df-029358153595"",
        ""serviceOfferingUuid"": ""f1a234ae-1d5a-4fbc-817f-40c0c65e4e32"",
        ""hypervisorType"": ""XenServer""
      },
      ""terminationDate"": null
    }
  ]
}";

        public static bool Delete(Subscription oldSub)
        {
            var clientSession = new Client(
                DT2.Properties.Settings.Default.CPBMEndPoint,
                DT2.Properties.Settings.Default.CPBMApiKey,
                DT2.Properties.Settings.Default.CPBMSecretKey);

            return Delete(clientSession, oldSub);
        }


        public static bool Delete(Citrix.CPBM.Client.Sample.Client clientSession, Subscription oldSub)
        {
            // Creation happens when you POST to /subscriptions
            APIRequest request = new APIRequest("/subscriptions/" + oldSub.Uuid.ToString(), "DELETE");
            bool success = false;

            // Q: What is the purpose of entitlements?
            try
            {
                dynamic result = clientSession.SendRequest(request);
                success = true;
            }
            catch (CPBMException ex)
            {
                String errMsg = "Exception deleting subscriptions: " + ex.Message + ex.StackTrace;
                logger.Error(errMsg, ex);
            }

            return success;
        }
    }
}