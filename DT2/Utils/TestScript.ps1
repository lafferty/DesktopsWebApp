<#

.SYNOPSIS

Return XenDesktop inventory item at a specific path


.DESCRIPTION

Uses Get-Item


.PARAMETER path

Path for item, e.g. 'XDHyp:\HostingUnits\CloudPlatformHost\'


.NOTES
	(c)opyright Citrix Systems
    Version:        1.0
    Author:			Donal Lafferty
    Creation Date:	2014-03-13
    Purpose/Change: CloudDestkop
 
You need to run this function as a user with admin access to XenDesktop

#>

Param (
    [string]$ddcAddress,
	[string]$desktopDomain,
	[log4net.ILog]$log
)

Add-pssnapin citrix.*

<# test sample
$controllerAddress="xdc1.clouddesktop.cambourne.cloud.com"
$ddcAddress=$controllerAddress+":80"
$desktopDomain="clouddesktop.cambourne.cloud.com"
#>

$error.clear()

function LogDebug($msg) {
    if ($log){
	    $log.Debug($msg)
    } else {
        Write-Host($msg) 
    }
}
function LogInfo($msg) {
    if ($log){
	    $log.Info($msg)
    } else {
        Write-Host($msg) 
    }
}
function LogError($msg) {
    if ($log){
        $log.Error("$msg")
    } else {
	Write-Error($msg) 
    } 
}


if ([string]::IsNullOrEmpty($catalogName))
{
	$catalogName = "*"
}

LogInfo ("Test script call")

try 
{
    LogInfo ("Get-AcctIdentityPool -IdentityPoolName TestTest")
    $acctIdPool = Get-AcctIdentityPool -IdentityPoolName "TestTest"

	if ($acctIdPool)
	{
        LogDebug ("Previous test failed, removing IdentityPool")
        LogInfo ("Remove-AcctIdentityPool -AdminAddress $ddcAddress -IdentityPoolUid $acctIdPool.IdentityPoolUid")
        Remove-AcctIdentityPool -AdminAddress $ddcAddress -IdentityPoolUid $acctIdPool.IdentityPoolUid 
    }

	$acctIdPool = New-AcctIdentityPool  -AdminAddress $ddcAddress -AllowUnicode -Domain $desktopDomain -IdentityPoolName "TestTest" -NamingScheme TestTest### -NamingSchemeType 'Numeric' -Scope @()
	LogInfo ("New-AcctIdentityPool returned $acctIdPool")
	$newMachineAccts = New-AcctADAccount  -AdminAddress $ddcAddress -Count 1 -IdentityPoolUid $acctIdPool.IdentityPoolUid 
    LogInfo ("New-AcctADAccount complete")

    $acctIdPool = Get-AcctIdentityPool -IdentityPoolName "TestTest"
    $accts = Get-AcctADAccount -IdentityPoolName "TestTest"

	if (!$accts)
	{
        LogError ("User lacks sufficient AD privileges for New-AcctADAccount to work properly")
	}

    LogDebug ("Remove-AcctADAccount  -ADAccountSid @( $accts[0].ADAccountSid ) -AdminAddress $ddcAddress -Force -IdentityPoolUid $acctIdPool.IdentityPoolUid -RemovalOption 'Delete'");
    $delAcctResult = Remove-AcctADAccount  -ADAccountSid @( $accts[0].ADAccountSid ) -AdminAddress $ddcAddress -Force -IdentityPoolUid $acctIdPool.IdentityPoolUid -RemovalOption 'Delete'
    LogDebug ("Result: $delAcctResult")
    
    LogInfo ("Remove-AcctIdentityPool -AdminAddress $ddcAddress -IdentityPoolUid $acctIdPool.IdentityPoolUid")
    Remove-AcctIdentityPool -AdminAddress $ddcAddress -IdentityPoolUid $acctIdPool.IdentityPoolUid 
}
catch [System.Exception] {
    LogError("Problem with API call creation " + $_ )
    throw $_
}

