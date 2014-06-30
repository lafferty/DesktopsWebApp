<#

.SYNOPSIS

Return XenDesktop inventory item at a specific path in the XDHYP: volume that the XenDesktop SDK
creates.


.DESCRIPTION

Uses Get-Item cmdlet to retrieve an object at a given path.


.PARAMETER path

Path for item, e.g. 'XDHyp:\HostingUnits\CloudPlatformHost\'


.PARAMETER log

log4net.ILog object used for logging calls. Optional, however the log4net.ILog type must be loaded
into the PowerShell RunSpace for this script to work.

Therefore, if you are running from a PowerShell, first call InitLog4net.ps1  This will load the 
necessary assemblies for the log4net.ILog type to resolve.


.NOTES
    (c)opyright Citrix Systems
    Version:        1.0
    Author:			Donal Lafferty
    Creation Date:	2014-03-13
    Purpose/Change: CloudDestkop
 
You need to run this function as a user with admin access to XenDesktop

#>

Param (
    [string]$path,
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

LogInfo ("calling Get-Item -Path $path")
Get-Item -Path $path
LogInfo ("Get-Item copmlete complete")
