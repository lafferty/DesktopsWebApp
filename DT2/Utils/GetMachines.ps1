<#

.SYNOPSIS

Return details of machines hosting desktops for a give catalog identified by name, or all catalogs 
if no name is given.  Maximum of 250 machines returned.


.DESCRIPTION

Uses Get-BrokerMachine from XenDesktop SDK, see 
http://support.citrix.com/proddocs/topic/citrix-broker-admin-v2-xd75/get-brokermachine-xd75.html


.PARAMETER catalogName

Request results for a specific catalog. Optional


.PARAMETER skipCount

Over comes max of 250 machines returned by changing the start index for the list returned.


.PARAMETER log

log4net.ILog object used for logging calls. Optional, however the log4net.ILog type must be loaded
into the PowerShell RunSpace for this script to work.

Therefore, if you are running from a PowerShell, first call InitLog4net.ps1  This will load the 
necessary assemblies for the log4net.ILog type to resolve.


.NOTES
    (c)opyright Citrix Systems
    Version:        1.0
    Author:			Donal Lafferty
    Creation Date:	2014-03-07
    Purpose/Change: CloudDestkop
 
You need to run this function as a user with admin access to XenDesktop

#>

Param (
    [string]$catalogName,
    [string]$skipCount,
    [log4net.ILog]$log
)

Add-pssnapin citrix.*

$error.clear()
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

LogInfo ("Get-BrokerMachine -CatalogName $catalogName -Skip $skipCount -SortBy '+DNSName'")
Get-BrokerMachine -CatalogName $catalogName -Skip $skipCount -SortBy '+DNSName'
LogInfo ("Query broker machines (Get-BrokerMachine ) complete")
