using System.Security.Cryptography;
using log4net;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Citrix.CPBM.Client.Sample;
using Microsoft.Ajax.Utilities;
using Microsoft.CSharp.RuntimeBinder;
using Newtonsoft.Json.Linq;

namespace DT2.Models
{
    /// <summary>
    ///  Product bundle listed in a catalog, see http://support.citrix.com/proddocs/topic/cpbm-22-map/cpbm-catalogs-ref.html 
    /// </summary>

    public class ProductBundle
    {
        private static ILog logger = LogManager.GetLogger(typeof(ProductBundle));

        [Display(Name = "Code")]
        [DataType(DataType.Text)]
        public string Code { get; set; }


        /// <summary>
        /// The name of the CloudStack ServiceInstance hosting the ProductBundles. 
        /// </summary>
        /// <remarks>
        /// Our webapp can only use one CloudStack.  ProductBundles for other services are  
        /// omitted from the ProductBundle lists.
        /// </remarks>
        [Display(Name = "Service Instance")]
        [DataType(DataType.Text)]
        public string ServiceInstanceName { get; set;}

        [Display(Name = "Service Offering Uuid List")]
        [DataType(DataType.Text)]
        public List<String> ServiceOfferingUuid { get; set; }

        [Display(Name = "Image Template Uuid List")]
        [DataType(DataType.Text)]
        public List<String> ImageTemplateUuid { get; set; }

        [Required]
        [Display(Name = "Name")]
        [DataType(DataType.Text)]
        public string Name { get; set; }

        [DataType(DataType.Text)]
        [Display(Name = "Description")]
        public string Description { get; set; }


        [Display(Name = "Id")]
        [DataType(DataType.Text)]
        public string Id { get; set; }

        [Display(Name = "Charge Frequency")]
        [DataType(DataType.Text)]
        public string ChargeFrequency { get; set; }

        /// <summary>
        /// Array of charges, first element is one-time setup, second is recurring charge. 
        /// </summary>
        /// <example>
        /// JSON representation: 
        ///  "rateCardCharges": [
        ///    {
        ///      "price": "5.00"
        ///    },
        ///    {
        ///      "price": "60.00"
        ///    }
        /// </example>
        [DataType(DataType.Text)] public List<string> RateCardCharges;


        /// <summary>
        /// Extracts bundle information, uses config file CPBM credentials.
        /// </summary>
        /// <returns></returns>
        public static List<ProductBundle> GetBundles()
        {
            var clientSession = new Client(
                DT2.Properties.Settings.Default.CPBMEndPoint,
                DT2.Properties.Settings.Default.CPBMApiKey,
                DT2.Properties.Settings.Default.CPBMSecretKey);

            return GetBundles(clientSession);
        }

        /// <summary>
        /// Extracts bundle information, assumes cpbmClient has sufficient access rights.
        /// </summary>
        /// <param name="cpbmClient"></param>
        /// <returns></returns>
        public static List<ProductBundle> GetBundles(Citrix.CPBM.Client.Sample.Client cpbmClient)
        {
            return GetBundles(cpbmClient, Guid.Empty);
        }


        /// <summary>
        /// Extracts bundle information from a specific catalog
        /// </summary>
        /// <param name="cpbmClient"></param>
        /// <param name="catalogUuid">Optional. Uuid for specific tenant [check]/param>
        /// <returns></returns>
        public static List<ProductBundle> GetBundles(Citrix.CPBM.Client.Sample.Client cpbmClient, Guid uuid)
        {
            logger.Debug("Ask CPBM for the productBundleRevisions object at /account/catalog");

            // Get the Catalog  
            // e.g. /account/catalogs
            APIRequest request = new APIRequest("/account/catalog", "GET");


            if (!uuid.Equals(Guid.Empty))
            {
                logger.Debug("productBundleRevisions for specific UUID " + uuid.ToString());
                request = new APIRequest("/account/" + uuid + "/catalog", "GET");
            }
//            request.SetParameterValue("expand", "productBundleRevisions,productBundle.entitlements");
            string expandSpec = 
                "productBundleRevisions.entitlements.product," +
                "productBundleRevisions.provisionConstraints," +
                "productBundleRevisions.productBundle," +
                "productBundleRevisions.rateCardCharges.rateCardComponent.rateCard," +
                "productRevisions.mediationRules.mediationRuleDiscriminators.serviceDiscriminator";

            request.SetParameterValue("expand", expandSpec);


            // Default position is to return a List of length zero.
            List<ProductBundle> bundles = new List<ProductBundle>();
            try
            {
                logger.Debug("CPBM request " + request.ToJSON());
                dynamic result = cpbmClient.SendRequest(request);

                bundles = ParseJson(result);
            }
            catch (Exception ex)
            {
                String errMsg = "Exception retrieving bundle: " + ex.Message + ex.StackTrace;
                logger.Error(errMsg, ex);
            }

            return bundles;
        }
        public static List<ProductBundle> ParseJson(dynamic result)
        {
            List<ProductBundle> bundles = new List<ProductBundle>();
            string jsonSerialisedResult = result.ToString();
            logger.Debug("CPBM response " + jsonSerialisedResult);

            // Don't use Linq until you need to select specific objects in a colletion based on a specific condition.

            // Exception handling v checking property exists examined here:
            // http://stackoverflow.com/a/20001358/939250
            //
            dynamic catalog;
            try
            {
                catalog = result.catalog;
            }
            catch (RuntimeBinderException e)
            {
                String errMsg = "Missing catalog object in " + jsonSerialisedResult;
                logger.Error(errMsg, e);
                throw;
            }

            JArray productBundleRevisions;
            try
            {
                productBundleRevisions = catalog.productBundleRevisions;
            }
            catch (RuntimeBinderException e)
            {
                String errMsg = "Missing productBundleRevisions object in " + jsonSerialisedResult;
                logger.Error(errMsg, e);
                throw;
            }

            foreach (dynamic productBundleInfo in productBundleRevisions)
            {
                ProductBundle productBundle = ParseProductBundle(productBundleInfo);

                // Screen out service instances by name
                if (productBundle.ServiceInstanceName != DT2.Properties.Settings.Default.CPBMServiceInstanceName)
                {
                    logger.Debug("Ignoring product bundle " + productBundle.Name + " due to service instance, which is " + productBundle.ServiceInstanceName);
                    continue;
                }

                ParseBundleConstraint(productBundleInfo, productBundle);

                ParseBundleRates(productBundle, productBundleInfo);

                logger.Debug("Added ProductBundle code" + productBundle.Code);
                bundles.Add(productBundle);
            }
            return bundles;
        }

        /// <summary>
        /// Extract charges.  This implementation assumes there is one non-recuring and one recurring charge,
        /// and that hte non-recurring occurs first.
        /// </summary>
        /// <param name="productBundle"></param>
        /// <param name="productBundleInfo"></param>
        public static void ParseBundleRates(ProductBundle productBundle, dynamic productBundleInfo)
        {
            productBundle.RateCardCharges = new List<string>();

            try
            {
                foreach (dynamic rateCardCharge in productBundleInfo.rateCardCharges)
                {
                    ///        "rateCardCharges": [
                    ///          {
                    ///            "price": "5.00"
                    ///          },
                    ///          {
                    ///            "price": "0.00"
                    ///          }
                    ///        ],
                    productBundle.RateCardCharges.Add((string) rateCardCharge.price);
                    productBundle.ChargeFrequency = (string)rateCardCharge.rateCardComponent.rateCard.chargeType.displayName;
                    
                }
            }
            catch (RuntimeBinderException e)
            {
                String errMsg = "Malformed productBundle object" + productBundleInfo.ToString();
                logger.Error(errMsg, e);
                throw;
            }
        }

        public static void ParseBundleConstraint(dynamic productBundleInfo, ProductBundle productBundle)
        {
            try
            {
                productBundle.ServiceOfferingUuid = new List<string>();
                productBundle.ImageTemplateUuid = new List<string>();

                foreach (dynamic constraint in productBundleInfo.provisioningConstraints)
                {
                    //  "provisioningConstraints": [
                    //  {
                    //    "association": "INCLUDES",
                    //    "componentName": "serviceOfferingUuid",
                    //    "value": "f1a234ae-1d5a-4fbc-817f-40c0c65e4e32"
                    //  }
                    //]
                    string contraintType = (string) constraint.componentName;
                    if (contraintType == "serviceOfferingUuid")
                    {
                        string constraintUuid = (string)constraint.value;
                        logger.Debug("Adding ServiceOfferingUuid constraint" + constraintUuid);
                        productBundle.ServiceOfferingUuid.Add(constraintUuid);
                    }
                    else if (contraintType == "templateUuid")
                    {
                        string constraintUuid = (string)constraint.value;
                        logger.Debug("Adding templateUuid constraint" + constraintUuid);
                        productBundle.ImageTemplateUuid.Add(constraintUuid);
                    }
                }
            }
            catch (RuntimeBinderException e)
            {
                String errMsg = "ProductBundle object with no constraints" + productBundleInfo.ToString();
                logger.Error(errMsg, e);
            }
        }

        public static ProductBundle ParseProductBundle(dynamic productBundleInfo)
        {
            ProductBundle productBundle = new ProductBundle();
            
            try
            {
                ///  "productBundle": {
                ///    "id": "19",
                ///    "name": "Large Windows Server 2012 R2",
                ///    "description": "This is a Large Windows Server 2012 R2 instance (2 CPU @1GHz, 2GB RAM) charged at monthly rate.",
                ///    "code": "CCPDixonLargeWindowsServer2012R2"
                ///  },
                dynamic prodBundleJson = productBundleInfo.productBundle;
                productBundle.Name = (string) prodBundleJson.name;
                productBundle.Description = (string) prodBundleJson.description;
                productBundle.Code = (string) prodBundleJson.code;
                productBundle.Id = (string) prodBundleJson.id;
                productBundle.ServiceInstanceName = (string)prodBundleJson.serviceInstanceId.name;

            }
            catch (RuntimeBinderException e)
            {
                String errMsg = "Malformed productBundle object" + productBundle.ToString();
                logger.Error(errMsg, e);
                throw;
            }
            return productBundle;
        }


        #region TestSamples
        public const string SampleCatalogJson2 = @"
            {
              ""catalog"": {
                ""channel"": null,
                ""productRevisions"": [
                  {},
                  {}
                ],
                ""productBundleRevisions"": [
                  {
                    ""productBundle"": {
                      ""id"": 29,
                      ""name"": ""Tiny Instance Monthly"",
                      ""description"": ""This is a Tiny VM monthly bundle."",
                      ""code"": ""IaaSUSWest_TinyInstance_CCP-Dixon_TinyInstance_Monthly"",
                      ""serviceInstanceId"": {
                        ""uuid"": ""766c00fe-7431-405a-8cfb-513465373718"",
                        ""name"": ""IaaS US West"",
                        ""category"": ""IAAS""
                      },
                      ""resourceType"": {
                        ""resourceTypeName"": ""VirtualMachine""
                      },
                      ""updatedBy"": {}
                    },
                    ""rateCardCharges"": [
                      {
                        ""price"": 5,
                        ""currencyValue"": {
                          ""currencyCode"": ""USD"",
                          ""currencyName"": ""US Dollar""
                        },
                        ""rateCardComponent"": {
                          ""isRecurring"": false,
                          ""rateCard"": {
                            ""description"": ""Initial version"",
                            ""startDate"": ""2014-07-22T01:00:00+01:00"",
                            ""chargeType"": {
                              ""name"": ""MONTHLY"",
                              ""displayName"": ""Monthly"",
                              ""description"": ""Monthly subscription charges(Prorate for first month)"",
                              ""frequencyInMonths"": ""1""
                            }
                          }
                        }
                      },
                      {
                        ""price"": 18,
                        ""currencyValue"": {
                          ""currencyCode"": ""USD"",
                          ""currencyName"": ""US Dollar""
                        },
                        ""rateCardComponent"": {
                          ""isRecurring"": true,
                          ""rateCard"": {
                            ""description"": ""Initial version"",
                            ""startDate"": ""2014-07-22T01:00:00+01:00"",
                            ""chargeType"": {
                              ""name"": ""MONTHLY"",
                              ""displayName"": ""Monthly"",
                              ""description"": ""Monthly subscription charges(Prorate for first month)"",
                              ""frequencyInMonths"": ""1""
                            }
                          }
                        }
                      }
                    ],
                    ""entitlements"": [
                      {
                        ""includedUnits"": -1,
                        ""product"": {
                          ""name"": ""Tiny Instance"",
                          ""code"": ""ccp-dixon_TINY_INSTANCE"",
                          ""category"": {
                            ""name"": ""Infrastructure: Compute""
                          },
                          ""uom"": ""Compute-Hours"",
                          ""imagePath"": null,
                          ""serviceInstance"": {
                            ""uuid"": ""766c00fe-7431-405a-8cfb-513465373718"",
                            ""name"": ""IaaS US West"",
                            ""category"": ""IAAS""
                          }
                        }
                      }
                    ],
                    ""provisioningConstraints"": [
                      {
                        ""association"": ""INCLUDES"",
                        ""componentName"": ""serviceOfferingUuid"",
                        ""value"": ""f1a234ae-1d5a-4fbc-817f-40c0c65e4e32""
                      }
                    ]
                  },
                  {
                    ""productBundle"": {
                      ""id"": 30,
                      ""name"": ""Small Instance Monthly"",
                      ""description"": ""This is a small VM monthly bundle."",
                      ""code"": ""IaaSUSWest_SmallInstance_CCP-Dixon_SmallInstance_Monthly"",
                      ""serviceInstanceId"": {
                        ""uuid"": ""766c00fe-7431-405a-8cfb-513465373718"",
                        ""name"": ""IaaS US West"",
                        ""category"": ""IAAS""
                      },
                      ""resourceType"": {
                        ""resourceTypeName"": ""VirtualMachine""
                      },
                      ""updatedBy"": {}
                    },
                    ""rateCardCharges"": [
                      {
                        ""price"": 5,
                        ""currencyValue"": {
                          ""currencyCode"": ""USD"",
                          ""currencyName"": ""US Dollar""
                        },
                        ""rateCardComponent"": {
                          ""isRecurring"": false,
                          ""rateCard"": {
                            ""description"": ""Initial version"",
                            ""startDate"": ""2014-07-22T01:00:00+01:00"",
                            ""chargeType"": {
                              ""name"": ""MONTHLY"",
                              ""displayName"": ""Monthly"",
                              ""description"": ""Monthly subscription charges(Prorate for first month)"",
                              ""frequencyInMonths"": ""1""
                            }
                          }
                        }
                      },
                      {
                        ""price"": 24,
                        ""currencyValue"": {
                          ""currencyCode"": ""USD"",
                          ""currencyName"": ""US Dollar""
                        },
                        ""rateCardComponent"": {
                          ""isRecurring"": true,
                          ""rateCard"": {
                            ""description"": ""Initial version"",
                            ""startDate"": ""2014-07-22T01:00:00+01:00"",
                            ""chargeType"": {
                              ""name"": ""MONTHLY"",
                              ""displayName"": ""Monthly"",
                              ""description"": ""Monthly subscription charges(Prorate for first month)"",
                              ""frequencyInMonths"": ""1""
                            }
                          }
                        }
                      }
                    ],
                    ""entitlements"": [
                      {
                        ""includedUnits"": -1,
                        ""product"": {
                          ""name"": ""Small Instance"",
                          ""code"": ""CCP-Dixon_SMALL_INSTANCE"",
                          ""category"": {
                            ""name"": ""Infrastructure: Compute""
                          },
                          ""uom"": ""Compute-Hours"",
                          ""imagePath"": null,
                          ""serviceInstance"": {
                            ""uuid"": ""766c00fe-7431-405a-8cfb-513465373718"",
                            ""name"": ""IaaS US West"",
                            ""category"": ""IAAS""
                          }
                        }
                      }
                    ],
                    ""provisioningConstraints"": [
                      {
                        ""association"": ""INCLUDES"",
                        ""componentName"": ""serviceOfferingUuid"",
                        ""value"": ""68416055-1d04-4e30-a290-740b8871fab0""
                      }
                    ]
                  },
                  {
                    ""productBundle"": {
                      ""id"": 31,
                      ""name"": ""Medium Instance Monthly"",
                      ""description"": ""This is a medium VM monthly bundle."",
                      ""code"": ""IaaSUSWest_MediumInstance_CCP-Dixon_MediumInstance_Monthly"",
                      ""serviceInstanceId"": {
                        ""uuid"": ""766c00fe-7431-405a-8cfb-513465373718"",
                        ""name"": ""IaaS US West"",
                        ""category"": ""IAAS""
                      },
                      ""resourceType"": {
                        ""resourceTypeName"": ""VirtualMachine""
                      },
                      ""updatedBy"": {}
                    },
                    ""rateCardCharges"": [
                      {
                        ""price"": 5,
                        ""currencyValue"": {
                          ""currencyCode"": ""USD"",
                          ""currencyName"": ""US Dollar""
                        },
                        ""rateCardComponent"": {
                          ""isRecurring"": false,
                          ""rateCard"": {
                            ""description"": ""Initial version"",
                            ""startDate"": ""2014-07-22T01:00:00+01:00"",
                            ""chargeType"": {
                              ""name"": ""MONTHLY"",
                              ""displayName"": ""Monthly"",
                              ""description"": ""Monthly subscription charges(Prorate for first month)"",
                              ""frequencyInMonths"": ""1""
                            }
                          }
                        }
                      },
                      {
                        ""price"": 33,
                        ""currencyValue"": {
                          ""currencyCode"": ""USD"",
                          ""currencyName"": ""US Dollar""
                        },
                        ""rateCardComponent"": {
                          ""isRecurring"": true,
                          ""rateCard"": {
                            ""description"": ""Initial version"",
                            ""startDate"": ""2014-07-22T01:00:00+01:00"",
                            ""chargeType"": {
                              ""name"": ""MONTHLY"",
                              ""displayName"": ""Monthly"",
                              ""description"": ""Monthly subscription charges(Prorate for first month)"",
                              ""frequencyInMonths"": ""1""
                            }
                          }
                        }
                      }
                    ],
                    ""entitlements"": [
                      {
                        ""includedUnits"": -1,
                        ""product"": {
                          ""name"": ""Medium Instance"",
                          ""code"": ""CCP-Dixon_MEDIUM_INSTANCE"",
                          ""category"": {
                            ""name"": ""Infrastructure: Compute""
                          },
                          ""uom"": ""Compute-Hours"",
                          ""imagePath"": null,
                          ""serviceInstance"": {
                            ""uuid"": ""766c00fe-7431-405a-8cfb-513465373718"",
                            ""name"": ""IaaS US West"",
                            ""category"": ""IAAS""
                          }
                        }
                      }
                    ],
                    ""provisioningConstraints"": [
                      {
                        ""association"": ""INCLUDES"",
                        ""componentName"": ""serviceOfferingUuid"",
                        ""value"": ""9ad659d8-fe83-409f-8f65-2af4e4c7efb9""
                      }
                    ]
                  },
                  {
                    ""productBundle"": {
                      ""id"": 32,
                      ""name"": ""Large Instance Monthly"",
                      ""description"": ""This is a large VM monthly bundle."",
                      ""code"": ""IaaSUSWest_LargeInstance_CCP-Dixon_LargeInstance_Monthly"",
                      ""serviceInstanceId"": {
                        ""uuid"": ""766c00fe-7431-405a-8cfb-513465373718"",
                        ""name"": ""IaaS US West"",
                        ""category"": ""IAAS""
                      },
                      ""resourceType"": {
                        ""resourceTypeName"": ""VirtualMachine""
                      },
                      ""updatedBy"": {}
                    },
                    ""rateCardCharges"": [
                      {
                        ""price"": 5,
                        ""currencyValue"": {
                          ""currencyCode"": ""USD"",
                          ""currencyName"": ""US Dollar""
                        },
                        ""rateCardComponent"": {
                          ""isRecurring"": false,
                          ""rateCard"": {
                            ""description"": ""Initial version"",
                            ""startDate"": ""2014-07-22T01:00:00+01:00"",
                            ""chargeType"": {
                              ""name"": ""MONTHLY"",
                              ""displayName"": ""Monthly"",
                              ""description"": ""Monthly subscription charges(Prorate for first month)"",
                              ""frequencyInMonths"": ""1""
                            }
                          }
                        }
                      },
                      {
                        ""price"": 55,
                        ""currencyValue"": {
                          ""currencyCode"": ""USD"",
                          ""currencyName"": ""US Dollar""
                        },
                        ""rateCardComponent"": {
                          ""isRecurring"": true,
                          ""rateCard"": {
                            ""description"": ""Initial version"",
                            ""startDate"": ""2014-07-22T01:00:00+01:00"",
                            ""chargeType"": {
                              ""name"": ""MONTHLY"",
                              ""displayName"": ""Monthly"",
                              ""description"": ""Monthly subscription charges(Prorate for first month)"",
                              ""frequencyInMonths"": ""1""
                            }
                          }
                        }
                      }
                    ],
                    ""entitlements"": [
                      {
                        ""includedUnits"": -1,
                        ""product"": {
                          ""name"": ""Large Instance"",
                          ""code"": ""ccp-dixon_LARGE_INSTANCE"",
                          ""category"": {
                            ""name"": ""Infrastructure: Compute""
                          },
                          ""uom"": ""Compute-Hours"",
                          ""imagePath"": null,
                          ""serviceInstance"": {
                            ""uuid"": ""766c00fe-7431-405a-8cfb-513465373718"",
                            ""name"": ""IaaS US West"",
                            ""category"": ""IAAS""
                          }
                        }
                      }
                    ],
                    ""provisioningConstraints"": [
                      {
                        ""association"": ""INCLUDES"",
                        ""componentName"": ""serviceOfferingUuid"",
                        ""value"": ""567c52c9-0e1e-4def-ac49-396d9f1c2c98""
                      }
                    ]
                  },
                  {
                    ""productBundle"": {
                      ""id"": 33,
                      ""name"": ""Tiny Instance Annually"",
                      ""description"": ""This is a tiny instance annual bundle."",
                      ""code"": ""IaaSUSWest_TinyInstance_CCP-Dixon_TinyInstance_Annually"",
                      ""serviceInstanceId"": {
                        ""uuid"": ""766c00fe-7431-405a-8cfb-513465373718"",
                        ""name"": ""IaaS US West"",
                        ""category"": ""IAAS""
                      },
                      ""resourceType"": {
                        ""resourceTypeName"": ""VirtualMachine""
                      },
                      ""updatedBy"": {}
                    },
                    ""rateCardCharges"": [
                      {
                        ""price"": 0,
                        ""currencyValue"": {
                          ""currencyCode"": ""USD"",
                          ""currencyName"": ""US Dollar""
                        },
                        ""rateCardComponent"": {
                          ""isRecurring"": false,
                          ""rateCard"": {
                            ""description"": ""Initial version"",
                            ""startDate"": ""2014-07-22T01:00:00+01:00"",
                            ""chargeType"": {
                              ""name"": ""ANNUAL"",
                              ""displayName"": ""Annual"",
                              ""description"": ""Yearly subscription charges"",
                              ""frequencyInMonths"": ""12""
                            }
                          }
                        }
                      },
                      {
                        ""price"": 200,
                        ""currencyValue"": {
                          ""currencyCode"": ""USD"",
                          ""currencyName"": ""US Dollar""
                        },
                        ""rateCardComponent"": {
                          ""isRecurring"": true,
                          ""rateCard"": {
                            ""description"": ""Initial version"",
                            ""startDate"": ""2014-07-22T01:00:00+01:00"",
                            ""chargeType"": {
                              ""name"": ""ANNUAL"",
                              ""displayName"": ""Annual"",
                              ""description"": ""Yearly subscription charges"",
                              ""frequencyInMonths"": ""12""
                            }
                          }
                        }
                      }
                    ],
                    ""entitlements"": [
                      {
                        ""includedUnits"": -1,
                        ""product"": {
                          ""name"": ""Tiny Instance"",
                          ""code"": ""ccp-dixon_TINY_INSTANCE"",
                          ""category"": {
                            ""name"": ""Infrastructure: Compute""
                          },
                          ""uom"": ""Compute-Hours"",
                          ""imagePath"": null,
                          ""serviceInstance"": {
                            ""uuid"": ""766c00fe-7431-405a-8cfb-513465373718"",
                            ""name"": ""IaaS US West"",
                            ""category"": ""IAAS""
                          }
                        }
                      }
                    ],
                    ""provisioningConstraints"": [
                      {
                        ""association"": ""INCLUDES"",
                        ""componentName"": ""serviceOfferingUuid"",
                        ""value"": ""f1a234ae-1d5a-4fbc-817f-40c0c65e4e32""
                      }
                    ]
                  },
                  {
                    ""productBundle"": {
                      ""id"": 34,
                      ""name"": ""Small Instance Annually"",
                      ""description"": ""This is a small instance annual bundle."",
                      ""code"": ""IaaSUSWest_SmallInstance_CCP-Dixon_SmallInstance_Annually"",
                      ""serviceInstanceId"": {
                        ""uuid"": ""766c00fe-7431-405a-8cfb-513465373718"",
                        ""name"": ""IaaS US West"",
                        ""category"": ""IAAS""
                      },
                      ""resourceType"": {
                        ""resourceTypeName"": ""VirtualMachine""
                      },
                      ""updatedBy"": {}
                    },
                    ""rateCardCharges"": [
                      {
                        ""price"": 0,
                        ""currencyValue"": {
                          ""currencyCode"": ""USD"",
                          ""currencyName"": ""US Dollar""
                        },
                        ""rateCardComponent"": {
                          ""isRecurring"": false,
                          ""rateCard"": {
                            ""description"": ""Initial version"",
                            ""startDate"": ""2014-07-22T01:00:00+01:00"",
                            ""chargeType"": {
                              ""name"": ""ANNUAL"",
                              ""displayName"": ""Annual"",
                              ""description"": ""Yearly subscription charges"",
                              ""frequencyInMonths"": ""12""
                            }
                          }
                        }
                      },
                      {
                        ""price"": 260,
                        ""currencyValue"": {
                          ""currencyCode"": ""USD"",
                          ""currencyName"": ""US Dollar""
                        },
                        ""rateCardComponent"": {
                          ""isRecurring"": true,
                          ""rateCard"": {
                            ""description"": ""Initial version"",
                            ""startDate"": ""2014-07-22T01:00:00+01:00"",
                            ""chargeType"": {
                              ""name"": ""ANNUAL"",
                              ""displayName"": ""Annual"",
                              ""description"": ""Yearly subscription charges"",
                              ""frequencyInMonths"": ""12""
                            }
                          }
                        }
                      }
                    ],
                    ""entitlements"": [
                      {
                        ""includedUnits"": -1,
                        ""product"": {
                          ""name"": ""Small Instance"",
                          ""code"": ""CCP-Dixon_SMALL_INSTANCE"",
                          ""category"": {
                            ""name"": ""Infrastructure: Compute""
                          },
                          ""uom"": ""Compute-Hours"",
                          ""imagePath"": null,
                          ""serviceInstance"": {
                            ""uuid"": ""766c00fe-7431-405a-8cfb-513465373718"",
                            ""name"": ""IaaS US West"",
                            ""category"": ""IAAS""
                          }
                        }
                      }
                    ],
                    ""provisioningConstraints"": [
                      {
                        ""association"": ""INCLUDES"",
                        ""componentName"": ""serviceOfferingUuid"",
                        ""value"": ""68416055-1d04-4e30-a290-740b8871fab0""
                      }
                    ]
                  },
                  {
                    ""productBundle"": {
                      ""id"": 35,
                      ""name"": ""Medium Instance Annually"",
                      ""description"": ""This is a medium VM annual bundle."",
                      ""code"": ""IaaSUSWest_MediumInstance_CCP-Dixon_MediumInstance_Annually"",
                      ""serviceInstanceId"": {
                        ""uuid"": ""766c00fe-7431-405a-8cfb-513465373718"",
                        ""name"": ""IaaS US West"",
                        ""category"": ""IAAS""
                      },
                      ""resourceType"": {
                        ""resourceTypeName"": ""VirtualMachine""
                      },
                      ""updatedBy"": {}
                    },
                    ""rateCardCharges"": [
                      {
                        ""price"": 0,
                        ""currencyValue"": {
                          ""currencyCode"": ""USD"",
                          ""currencyName"": ""US Dollar""
                        },
                        ""rateCardComponent"": {
                          ""isRecurring"": false,
                          ""rateCard"": {
                            ""description"": ""Initial version"",
                            ""startDate"": ""2014-07-22T01:00:00+01:00"",
                            ""chargeType"": {
                              ""name"": ""ANNUAL"",
                              ""displayName"": ""Annual"",
                              ""description"": ""Yearly subscription charges"",
                              ""frequencyInMonths"": ""12""
                            }
                          }
                        }
                      },
                      {
                        ""price"": 350,
                        ""currencyValue"": {
                          ""currencyCode"": ""USD"",
                          ""currencyName"": ""US Dollar""
                        },
                        ""rateCardComponent"": {
                          ""isRecurring"": true,
                          ""rateCard"": {
                            ""description"": ""Initial version"",
                            ""startDate"": ""2014-07-22T01:00:00+01:00"",
                            ""chargeType"": {
                              ""name"": ""ANNUAL"",
                              ""displayName"": ""Annual"",
                              ""description"": ""Yearly subscription charges"",
                              ""frequencyInMonths"": ""12""
                            }
                          }
                        }
                      }
                    ],
                    ""entitlements"": [
                      {
                        ""includedUnits"": -1,
                        ""product"": {
                          ""name"": ""Medium Instance"",
                          ""code"": ""CCP-Dixon_MEDIUM_INSTANCE"",
                          ""category"": {
                            ""name"": ""Infrastructure: Compute""
                          },
                          ""uom"": ""Compute-Hours"",
                          ""imagePath"": null,
                          ""serviceInstance"": {
                            ""uuid"": ""766c00fe-7431-405a-8cfb-513465373718"",
                            ""name"": ""IaaS US West"",
                            ""category"": ""IAAS""
                          }
                        }
                      }
                    ],
                    ""provisioningConstraints"": [
                      {
                        ""association"": ""INCLUDES"",
                        ""componentName"": ""serviceOfferingUuid"",
                        ""value"": ""9ad659d8-fe83-409f-8f65-2af4e4c7efb9""
                      }
                    ]
                  },
                  {
                    ""productBundle"": {
                      ""id"": 36,
                      ""name"": ""Large Instance Annually"",
                      ""description"": ""This is a large VM annual bundle."",
                      ""code"": ""IaaSUSWest_LargeInstance_CCP-Dixon_LargeInstance_Annually"",
                      ""serviceInstanceId"": {
                        ""uuid"": ""766c00fe-7431-405a-8cfb-513465373718"",
                        ""name"": ""IaaS US West"",
                        ""category"": ""IAAS""
                      },
                      ""resourceType"": {
                        ""resourceTypeName"": ""VirtualMachine""
                      },
                      ""updatedBy"": {}
                    },
                    ""rateCardCharges"": [
                      {
                        ""price"": 0,
                        ""currencyValue"": {
                          ""currencyCode"": ""USD"",
                          ""currencyName"": ""US Dollar""
                        },
                        ""rateCardComponent"": {
                          ""isRecurring"": false,
                          ""rateCard"": {
                            ""description"": ""Initial version"",
                            ""startDate"": ""2014-07-22T01:00:00+01:00"",
                            ""chargeType"": {
                              ""name"": ""ANNUAL"",
                              ""displayName"": ""Annual"",
                              ""description"": ""Yearly subscription charges"",
                              ""frequencyInMonths"": ""12""
                            }
                          }
                        }
                      },
                      {
                        ""price"": 550,
                        ""currencyValue"": {
                          ""currencyCode"": ""USD"",
                          ""currencyName"": ""US Dollar""
                        },
                        ""rateCardComponent"": {
                          ""isRecurring"": true,
                          ""rateCard"": {
                            ""description"": ""Initial version"",
                            ""startDate"": ""2014-07-22T01:00:00+01:00"",
                            ""chargeType"": {
                              ""name"": ""ANNUAL"",
                              ""displayName"": ""Annual"",
                              ""description"": ""Yearly subscription charges"",
                              ""frequencyInMonths"": ""12""
                            }
                          }
                        }
                      }
                    ],
                    ""entitlements"": [
                      {
                        ""includedUnits"": -1,
                        ""product"": {
                          ""name"": ""Large Instance"",
                          ""code"": ""ccp-dixon_LARGE_INSTANCE"",
                          ""category"": {
                            ""name"": ""Infrastructure: Compute""
                          },
                          ""uom"": ""Compute-Hours"",
                          ""imagePath"": null,
                          ""serviceInstance"": {
                            ""uuid"": ""766c00fe-7431-405a-8cfb-513465373718"",
                            ""name"": ""IaaS US West"",
                            ""category"": ""IAAS""
                          }
                        }
                      }
                    ],
                    ""provisioningConstraints"": [
                      {
                        ""association"": ""INCLUDES"",
                        ""componentName"": ""serviceOfferingUuid"",
                        ""value"": ""567c52c9-0e1e-4def-ac49-396d9f1c2c98""
                      }
                    ]
                  }
                ]
              }
            }";
        #endregion
    }
}