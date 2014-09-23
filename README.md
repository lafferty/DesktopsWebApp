## Self-Service WebApp for Managing Windows Virtual Desktops on CloudStack

This source is for a WebApp that allows CloudStack tenants to manage desktop groups and desktop images.  

The WebApp is implementated using ASP.NET MVC5 and Citrix XenDesktop 7.5.  XenDesktop 7.5 provides 
provisioning and broker services.  The provisioning creates VMs that run desktop work loads on 
CloudStack.  The broker services allow users to connect to these desktops using Citrix Receiver.  
Citrix Receiver uses the ICA remote access protocol


## Updates

This update demonstrates CloudPortal Business Manager (CPBM) integration.  Specifically, Desktop
Group creation allows you to select the CPBM billing package you want to use for the VMs being
created.  This functionality makes use of the BSS API available with CPBM (E.g. 
http://support.citrix.com/proddocs/topic/cpbm-22-map/cpbm-overview-con.html )


## Getting started

The source includes a Microsoft Visual Studio 2013 solution called DT2.sln.  Load this solution 
into Visual Studio 2013 to build the WebApp.

The solution consists of three projects.  A project called DT2.csproj that is used to build the 
WebApp.  DT2.csproj is an ASP.NET MVC project.  For a tutorial on how to work with ASP.NET MVC 
projects see http://www.asp.net/mvc/tutorials/mvc-5/introduction/getting-started  The WebApp uses 
the XenDesktop SDK and CloudStack API to create manage desktops and desktop images.  For details on 
the XenDesktop SDK, see http://support.citrix.com/proddocs/topic/xenapp-xendesktop-75/cds-sdk-wrapper-rho.html
For details on the CloudStack API, see http://cloudstack.apache.org/docs/api/

A WiX project called DT2.Setup generates a .MSI that is used to install the WebApp.  The installer 
has been tested with Windows Server 2012 R2 and IIS 8.5.  The installer project is automatically 
updated when the files in the DT2.csproj change, so it is maintenance free.  However, the install 
does not set the web.config values required to operate the WebApp.  Instead, placeholders are set 
for the web.config values.  These must be set by the application or individual who will install the
application.  See the section on Deployment below for details of the configuration settings.

However, the WiX project requires you install WiX Toolset for your Visual Studio.  See
http://wixtoolset.org/ for the download.

Next, there is a UnitTestProject that is used to experiment with the XenDesktop SDK and the 
backend objects used by the WebApp.  The project uses the unit test functionality of Visual Studio 
to allow the developer to test the SDK and backend objects.  However, it is not a true test project.

The solution makes use of a number of NuGet packages.  You may have to "Enable Nuget Package Restore"
in order to build properly.  Enabling restore causes Visual Studio to download NuGet package
dependencies.

Finally, the dashboard page is designed to provide a chart describing the desktops groups that
have been created including their name and the number of virtual machines used to server desktops.
The dashboard makes use of the Highcharts package to create the view in the client's browser.
Highcharts is free to use in some circumstances, but may require a license, see http://shop.highsoft.com/faq
With this in mind, the Highcharts package is not included with this source.  To use it, download
the source, add to the 'Scripts' folder, and uncomment references to HighCharts from the .cshtml
file where the above link is referred to.


## Deployment

Make sure that the Windows Server 2012 R2 is configured to run ASP.NET MVC 5 applications. This 
will involve turning on IIS and adding support for ASP.NET 4.5.  A rough guide is available at 
http://www.iis.net/learn/get-started/whats-new-in-iis-8/iis-80-using-aspnet-35-and-aspnet-45

Setup XenDesktop to your Windows Server 2012 R2.  Install XenDesktop.  Add a CloudStack site to the 
XenDesktop configuration.

Install the WebApp.  Run the .MSI  After this is complete, edit the configuration.   Details of 
each setting are given in the web.config section below.

Update the settings for the IIS settings that govern the WebApp's application pool.  Details of 
important settings are given in the IIS Settings section below.

Next, update the CloudPlatform templates and service offerings to limit what is presented to 
the webapp user.  See the Displaying CloudPlatform Information section below for details.

Finally, update your CloudPortal Business Manager (CPBM) to have product bundles that correspond
to the service offerings and templates your are going to offer the user.


### IIS Settings

Under the IIS Application Pool, the recommended settings are:

Process Model:
* Identity:  resource domain\administrator
* Idle Timeout (minutes): 0
* Load User profile: True

Recycling
* Regular Time Interval (minutes): 0

Under the webapp's site settings, the requried settings are:

IIS Authentication
* Enable Forms Authentication
* Enable Anonymous Authentication
* Disable other types of authentication

    
ASP.NET Config:

Enable WindowsIdentity flow between threads.  This allows the WebApp to execute scripts using an 
impersonated identity.

One approach is to update config with these settings:

```xml
<legacyImpersonationPolicy enabled="false"/>
<alwaysFlowImpersonationPolicy enabled="true"/>
```

For these files:

```powershell
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\aspnet.config
C:\Windows\Microsoft.NET\Framework\v4.0.30319\aspnet.config
```


Set PowerShell execution policy to allow execution of the unsigned scripts that accompany the webapp.  

The current execution policy can be checked using the Get-ExecutionPolicy command.

One approach is to set the execution policy to unrestricuted.  e.g. 

```powershell
Set-ExecutionPolicy unrestricted
```

However, such a broad policy is only suitable for a development or proof of concept environment.  
A tighter script policy is required for commercial deployments.



### web.config 

WebApp settings are contained in the <DT2.properties.Settings> XML element in the web.config found 
in the webapp's root folder.  Below is a description if each setting.


*CloudStackApiKey*

API key for CloudPlatform account that XenDesktop will use to provision desktops.  E.g. 

```xml
NTbOqdGSM2KWzS0GIMO9fBO6TiKb2oEKo59t7hmPWNna4rQtftX3sarCO-sAMXfL8l3zm55mND__53bV-wyZrA
```

*CloudStackSecretKey*

Secret key for CloudPlatform account that XenDesktop will use to provision desktops.  E.g. 

```xml
G379F22wYG_ISyG4Y-0saikSyUQNf9hVozwcep-LqsGNRvSBx81bN-mZ1bDyckMYNItYypIfzjU-MlFrS5IEIw
```

*CloudStackHypervisor*

Name of hypervisor type on which desktops are provisioned.  E.g. 

```xml
XenServer  
```

This setting is used when new images are uploaded to the CloudStack account.


*XenDesktopHostingUnitName*

The name of the XenDesktop HostingUnit corresponding to the CloudPlatform account.  E.g. 

```xml
CloudPlatformHost  
```

To keep WebApp deployment simple, the a XenDesktop HostingUnit corresponding to the CloudPlatform account must be configured before the webapp is deployed.


*XenDesktopDomain*

Domain for the XenDesktop controller.  E.g. 

```xml
desktopwebapp.cambourne.cloud.com
```


*XenDesktopAdminAddress*

The FQDN and port of the XenDesktop controller.  E.g. 

```xml
xdc1.desktopwebapp.cambourne.cloud.com:80
```


*XenDesktopDDC*

The FQDN of the XenDesktop controller.  E.g. 

```xml
desktopwebapp.cambourne.cloud.com
```

In hindsight, XenDesktopAdminAddress and XenDesktopDomain could have been derived from the XenDesktopDDC setting.


*XenDesktopAvailabilityZone*

Name of the CloudPlatform Availability zone that images will be uploaded to.  E.g. 

```xml
Zone1
```


*CloudStackZoneId*

The GUID for the CloudStack zone in which desktops will run.  Used for image management during the 
upload.


*XenDesktopStoreFrontUrl*

URL for web-based StoreFront GUI provided in email that tells a user that their desktop is ready.  E.g. 

```xml
http://192.168.0.50/Citrix/StoreWeb/
```


*CloudStackUrl*

URL for the CloudStack API.  E.g. 

```xml
http://192.168.2.1:8080/client/api</value>
```


*LdapPath*

Path to the resource AD, which is the AD controller for the domain in which the XenDesktop 
controller is installed.  The path is specified using LDAP standards.  E.g. 

```xml
LDAP://CN=users,DC=desktopwebapp,DC=cambourne,DC=cloud,DC=com
```

An explanation of the syntax used above can be found onlien.  e.g. http://social.technet.microsoft.com/wiki/contents/articles/1773.ldap-path-active-directory-distinguished-and-relative-distinguished-names.aspx


*SecurityGroups*

Deprecated!  For shared networking deployments, the security group must specified.  However, the 
final version of the webapp was only tested with isolated networking, which does not make use of 
security groups.  For this reason, the setting can be left blank.


*PowerShellScriptsFolder*

Folder containing the powershell scripts used to control XenDesktop.  E.g. 

```xml
C:\inetpub\wwwroot\Citrix\DesktopWebApp\Utils
```

Explicitly specifying the folder is useful when using a development web server that separates 
executable files from content in a non-standard fashion.


*CheckUserForCreatePrivileges*

Set to true to activate a test of the logged in user's AD privileges.  If active, the webapp will 
log details of check to see if the user has sufficient AD privileges to create AD accounts for new 
desktops.


*templatefilter*

This string is used to filter the results when querying CloudStack for a list of templates available 
to the user.  E.g. 

```xml
executable
```

The list of valid options is given by the CloudStack API.  
For example, see http://cloudstack.apache.org/docs/api/apidocs-4.3/user/listTemplates.html 


Next are configuration setting for integrating with CloudPortal Business Manager:


*CPBMEndPoint*

URL for CPBM endpoint.

```xml
http://pmlab.cpbm.citrite.net/portal/api
```


*CPBMApiKey*

API key for tenant's CPBM account.

```xml
wNRXbfi96S3rLknkmG3xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx1e7UDIw
```


*CPBMSecretKey*

Secret key for tenant's CPBM account.

```xml
6AYwvDsp9r6sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxL_vA
```


*CPBMServiceInstanceName*

Secret key for tenant's CPBM account.

```xml
IaaS US West
```


*TestDisableCatalogCreate*

Used by developers wishing to disable the execution of the desktop group creation script on XenDesktop.
 

*TestDisableImageFetch*

Used by developers wishing to disable the requests to CloudStack for template images.
 

*TestDisableProductBundleGet*

Used by developers wishing to disable the requests to CPBM for a product bundle.
 

*TestDisableServiceOfferingGet*

Used by developers wishing to disable the requests to XenDesktop for a details of a compute service
offering.


Finally, email alerts generated when a desktop group is created are sent using the SMTP settings in
the <system.net><mailSettings> element.  
For details, see http://msdn.microsoft.com/en-us/library/ms164240(v=vs.110).aspx


### Displaying CloudPlatform Information

The webapp uses tags to identify templates that will appear to the user

* Desktop templates with a VDA need to be tagged with the key “diaasImage” and Server templates with
 a VDA should be tagged with the key “diaasImageServer”.  Any value will work, e.g. “true”


Optionally the webapp can limit the Service Offerings displayed to a user when creating a Desktop
Group
* Label Service Offerings that we want the user to see by putting the text *DIaaS* somewhere in the 
title or description.

If the keywork DIaaS does not appear in any service offering, all service offerings will be
displayed.


## Troubleshooting

The WebApp and its scripts use Log4Net logging

To Enable logging:
* Give IIS_USRS write privilege to ".\log\webapp.log"

NB: during script calls, logging relies on the impersonated user having access to this file due 
their admin privileges



## License

(The MIT License)

Copyright (c) 2014 Citrix Systems, Inc

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.


## Trademark and Copyright Ownership Notice Statement:
© 2014 Citrix Systems, Inc.  All rights reserved.  Citrix, XenDesktop, XenServer and CloudPlatform  
are trademarks of Citrix Systems, Inc. and/or one or more of its subsidiaries, and may be registered 
in the United States Patent and Trademark Office and in other countries.
