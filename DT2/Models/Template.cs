using System.IO;
using System.Security.Cryptography;
using System.Xml.Linq;
using log4net;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace DT2.Models
{
    public class Template
    {
        private static ILog logger = LogManager.GetLogger(typeof(Template));
        
        [Display(Name = "Id")]
        [DataType(DataType.Text)]
        public string Id { get; set; }

        [Required]
        [Display(Name = "Name")]
        [DataType(DataType.Text)]
        public string Name { get; set; }

        [Display(Name = "Size")]
        public string Size { get; set; }

        [Display(Name = "Owner")]
        public string Owner { get; set; }

        [Display(Name = "InventoryPath")]
        public string InventoryPath { get; set; }

        [Required]
        [StringLength(100, ErrorMessage = "The description cannot be at most 100 characters.")]
        [DataType(DataType.Text)]
        [Display(Name = "Description")]
        public string Description { get; set; }

        [Required]
        [DataType(DataType.Text)]
        [Display(Name = "Status")]
        public string Ready { get; set; }

        [Required]
        [DataType(DataType.Text)]
        [Display(Name = "Operating System")]
        public string OsType { get; set; }

        [Required]
        [DataType(DataType.Text)]
        [Display(Name = "Image Type")]
        public string DesktopType { get; set; }

        // TODO: correct type for a URL
        [Url(ErrorMessage = "valid URL is required")]
        [Required]
        [DataType(DataType.Url)]
        [RegularExpression(".*vhd$", ErrorMessage = "Please upload valid format, images must be a .vhd")]
        [Display(Name = "URL")]
        public string Url { get; set; }

        public class OsTypeInfo
        {
            [Display(Name = "Id")]
            [DataType(DataType.Text)]
            public string Id { get; set; }

            [Required]
            [Display(Name = "Name")]
            [DataType(DataType.Text)]
            public string Name { get; set; }
        }

        public const string VirtualDesktopType = "Desktop";
        public const string PublishedDesktopType = "Server";
        public static string DIaaSDesktopImage = "diaasImage";
        public static string DIaaSServerImage = "diaasImageServer";

        // TODO: OsTypeInfo needs to be revised to be a more generic sounding name, because it is used in a couple of places.
        public static List<OsTypeInfo> DesktopTypeOptions
        {
            get
            {
                var opts = new OsTypeInfo[]
                {
                    new OsTypeInfo() {Id = VirtualDesktopType, Name = VirtualDesktopType},
                    new OsTypeInfo() {Id = PublishedDesktopType, Name = PublishedDesktopType},
                };
                return new List<OsTypeInfo>(opts);
            }
        }

        public static List<OsTypeInfo> OsTypeOptions
        {
            get
            {
                var opts = new OsTypeInfo[] {
                    new OsTypeInfo(){ Id = "48", Name="Windows 7 (32-bit)" },
                    new OsTypeInfo(){ Id = "49", Name="Windows 7 (64-bit)" },
                    new OsTypeInfo(){ Id = "165", Name="Windows 8 (32-bit)" },
                    new OsTypeInfo(){ Id = "166", Name="Windows 8 (64-bit)" },
                    new OsTypeInfo(){ Id = "166", Name="Windows 8.1"},
                    new OsTypeInfo(){ Id = "52", Name="Windows Server 2008 (32-bit)" },
                    new OsTypeInfo(){ Id = "53", Name="Windows Server 2008 (64-bit)" },
                    new OsTypeInfo(){ Id = "54", Name="Windows Server 2008 R2 (64-bit)" },
                    new OsTypeInfo(){ Id = "167", Name="Windows Server 2012 (64-bit)" },
                    new OsTypeInfo(){ Id = "167", Name="Windows Server 2012 R2 (64-bit)" }
                 };
                return new List<OsTypeInfo>(opts);
            }
        }

        public static List<Template> GetTemplates(CloudStack.SDK.Client csClient)
        {
            return GetTemplates(csClient, null);
        }

        public static List<Template> GetTemplates(CloudStack.SDK.Client csClient, string templateid)
        {
            logger.Debug("Enumerate templates, filter according to tags");

            // TODO: can we filter by tag?
            var req = new CloudStack.SDK.ListTemplatesRequest();
            req.Parameters.Add("templatefilter", DT2.Properties.Settings.Default.templatefilter);

            List<Template> templates = new List<Template>();
            if (!string.IsNullOrEmpty(templateid))
            {
                logger.Debug("Enumerate a single template, " + templateid);
                req.Parameters.Add("id", templateid);
            }

            try
            {
                // Raw result from ListTemplates queried using Linq, used to obtain tag information
                var resp = csClient.SendRequest(req);

                var taggedTemplates = from template in resp.Root.Elements("template")
                    where template.Elements("tags").Count() > 0
                    select template;

                var targetZone = DT2.Properties.Settings.Default.XenDesktopAvailabilityZone;
                var imagesInZone = from template in taggedTemplates
                                   where template.Elements("zonename").Any(x => x.Value == targetZone)
                                   select template;

                var desktopImagesInZone = from template in imagesInZone
                                    where template.Elements("tags").Any(x => x.Element("key").Value == DIaaSDesktopImage)
                                    select template;
                var serverImagesInZone = from template in imagesInZone
                                    where template.Elements("tags").Any(x => x.Element("key").Value == DIaaSServerImage)
                                    select template;

                ConvertTemplate(desktopImagesInZone, templates, VirtualDesktopType);
                ConvertTemplate(serverImagesInZone, templates, PublishedDesktopType);

                //foreach (var item in rply.Template)
                //{
                //    // Linq query:#
                //    // 1. enumerate 'template' XML elements (from template in resp.Root.Elements("template")
                //    // 2. filter out all elements save that with same name as template we are inspecteding (where template.Elements("name").First().Value == item.Name
                //    // 3. from this set select items with "tags" element
                    
                //    // Look at XML Use tags to spot XD's templates, which we do not list for the user
                //    var templateTags = from template in resp.Root.Elements("template") where template.Elements("name").First().Value == item.Name select template.Elements("tags");

                //    if (templateTags.First().Count() < 1)
                //    {
                //        var tmplt = new Template() { Name = item.Name, Description = item.DisplayText, Id = item.Id, Ready = item.Status.ToString(), OsType = item.OsTypeId };
                //        logger.Debug("Enumerated template " + item.ToString());
                //        templates.Add(tmplt);
                //    }
                //}
            }
            catch (Exception e)
            {
                String errMsg = "Exception on Template indexing: " + e.Message + e.StackTrace;
                logger.Error(errMsg);
            }
            return templates;
        }

        private static void ConvertTemplate(IEnumerable<XElement> images, List<Template> templates, string desktopType)
        {
            foreach (var item in images)
            {
                var name = (string) item.Element("name").Value;
                var description = (string) item.Element("displaytext").Value;
                var id = (string) item.Element("id").Value;
                var ready = (string) item.Element("status").Value;
                var ostypename = (string) item.Element("ostypename").Value;
                var size = item.Element("size") == null ? "Calculating" : (string) item.Element("size").Value;
                var owner = "Self";
                var inventoryPath = XenDesktopInventoryItem.GetTemplatePathFromName(name);
                var tmplt = new Template()
                {
                    DesktopType =  desktopType,
                    InventoryPath = inventoryPath,
                    Name = name,
                    Description = description,
                    Id = id,
                    Ready = ready,
                    OsType = ostypename,
                    Size = size,
                    Owner = owner,
                };

                logger.Debug("Enumerated template " + tmplt.ToString());
                templates.Add(tmplt);
            }
        }

        public static bool CreateTemplate(Template newItem, CloudStack.SDK.Client client)
        {
            try
            {
                var path = newItem.Url;
                var format = System.IO.Path.GetExtension(path);
                //if (format == ".zip" || format == ".bz2" || format == ".gz")
                //{
                //    path = path.Remove(path.Length - format.Length);
                //    format = System.IO.Path.GetExtension(path);
                //}

                // strip '.'
                format = format.Remove(0, ".".Length);

                // TODO: deal with async nature of create, currently assume template will no longer be 'ready'
                CloudStack.SDK.APIRequest req = new CloudStack.SDK.APIRequest("registerTemplate");
                req.Parameters.Add("displaytext", newItem.Description);
                req.Parameters.Add("format", format.ToUpperInvariant());
                req.Parameters.Add("hypervisor", DT2.Properties.Settings.Default.CloudStackHypervisor);
                //config: what hypervisor type are we targetting?
                req.Parameters.Add("ispublic", "false");
                req.Parameters.Add("name", newItem.Name);
                req.Parameters.Add("ostypeid", newItem.OsType);
                req.Parameters.Add("url", newItem.Url);
                req.Parameters.Add("zoneid", DT2.Properties.Settings.Default.CloudStackZoneId); // config
                var rply = client.SendRequest(req);
                logger.Info("Create template request complete" + rply.ToString());

                // TODO: Deal upload fail scenarios
                var template = rply.Root.Element("template");
                var templateId = template.Element("id").Value;

                CloudStack.SDK.APIRequest reqTags = new CloudStack.SDK.APIRequest("createTags");
                reqTags.Parameters.Add("resourceids", templateId);
                reqTags.Parameters.Add("resourcetype", "template");
                if (!String.IsNullOrEmpty(newItem.DesktopType) && newItem.DesktopType == PublishedDesktopType)
                {
                    reqTags.Parameters.Add("tags[0].key", DIaaSServerImage);
                    reqTags.Parameters.Add("tags[0].value", "true");
                }
                else
                {
                    reqTags.Parameters.Add("tags[0].key", DIaaSDesktopImage);
                    reqTags.Parameters.Add("tags[0].value", "true");
                }
                var rply2 = client.SendRequest(reqTags);
            }
            catch (Exception e)
            {
                logger.Error("Create template failed!", e);
                return false;
            }
            logger.Info("Created template " + newItem.Name);
            return true;
        }


        public static bool DeleteTemplate(string id, CloudStack.SDK.Client client)
        {
            try
            {
                // TODO: deal with async nature of delete, currently assume template will no longer be 'ready'
                CloudStack.SDK.APIRequest req = new CloudStack.SDK.APIRequest("deleteTemplate");
                req.Parameters.Add("id", id);
                client.SendRequest(req);
                return false;
            }
            catch (Exception e)
            {
                String errMsg = "Exception on Deleting template " + id + ", message:" + e.Message + e.StackTrace;
                logger.Error(errMsg);
            }
            return true;
        }
    }
}