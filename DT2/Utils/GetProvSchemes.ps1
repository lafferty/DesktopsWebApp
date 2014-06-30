<#

.SYNOPSIS

Return provisioning schemes for a give catalog identified by name, or all catalogs if no name is given.


.DESCRIPTION

Uses Get-ProvScheme from XenDesktop SDK, see 
http://support.citrix.com/proddocs/topic/citrix-machinecreation-admin-v2-xd75/get-provscheme-xd75.html

.PARAMETER catalogName

Request results for a specific catalog. Optional


.PARAMETER log

log4net.ILog object used for logging calls. Optional


.NOTES
    (c)opyright Citrix Systems
    Version:        1.0
    Author:			Donal Lafferty
    Creation Date:	2014-07-07
    Purpose/Change: CloudDestkop
 
You need to run this function as a user with admin access to XenDesktop

#>

Param (
    [string]$catalogName,
    [log4net.ILog]$log,
    [string]$ndcContext
)

Add-pssnapin citrix.*

function LogInfo($msg) {
    if ($log){
        $log.Info($msg)
    } else {
        Write-Host($msg) 
    }
}

if ([string]::IsNullOrEmpty($catalogName))
{
    $catalogName = "*"
}

LogInfo("Query provscheme details for $catalogName using Get-ProvScheme")
Get-ProvScheme -ProvisioningSchemeName $catalogName
LogInfo("Query provscheme complete")
