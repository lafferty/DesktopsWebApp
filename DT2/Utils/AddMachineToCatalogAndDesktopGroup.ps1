<#

.SYNOPSIS

Long running script that adds an additional VM to an existing XenDesktop Catalog and corresponding XenDestkop Delivery group,
which results in adding a machine to a DIaaS Desktop Group


.DESCRIPTION

Uses XenDesktop SDK, see http://support.citrix.com/proddocs/topic/xendesktop-71/cds-sdk-example-create-catalog.html


.PARAMETER catalogName

Request results for a specific catalog. Optional

.PARAMETER TODO

Add help for all parameters

.PARAMETER log

log4net.ILog object used for logging calls. Optional, however the log4net.ILog type must be loaded
into the PowerShell RunSpace for this script to work.

Therefore, if you are running from a PowerShell, first call InitLog4net.ps1  This will load the 
necessary assemblies for the log4net.ILog type to resolve.


.NOTES
    (c)opyright Citrix Systems
    Version:        1.0
    Author:			Donal Lafferty
    Creation Date:	2014-06-02
    Purpose/Change: CloudDestkop
 
You need to run this function as a user with admin access to XenDesktop

#>

Param (
    [string]$ddcAddress,
    [string]$catalogName,
    [string]$desktopGrpName,
    [string]$newDesktopCount,
    [log4net.ILog]$log,
    [string]$ndcContext
)

<#
$controllerAddress="XDC01.tenant3.local"
$ddcAddress=$controllerAddress+":80"
$catalogName="June3TestA"
$desktopGrpName = $catalogName + "_desktopgrp"
$newDesktopCount=1
#>

$error.clear()

function LogDebug($msg) {
    if ($log){
        $log.Debug("$ndcContext $msg")
    } else {
        Write-Host($msg) 
    }
}
function LogInfo($msg) {
    if ($log){
        $log.Info("$ndcContext $msg")
    } else {
        Write-Host($msg) 
    }
}
function LogError($msg) {
    if ($log){
        $log.Error("$ndcContext $msg")
    } else {
    Write-Error($msg) 
    } 
}

$user = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
LogInfo("Current user is $user")

Add-pssnapin citrix.*

#------------------START CatalogAddMachine -------------------------------

LogInfo("Attempting to add machines to catalog $catalogName with $machineCount machines" )

$error.clear()
$svcStatus = Get-ConfigServiceStatus  -AdminAddress $ddcAddress
if ($svcStatus -ne "OK")
{
    LogError "Problem with $ddcAddress, ConfigServiceStatus is $svcStatus"
    Return
}

# Query availability of logging
# http://support.citrix.com/proddocs/topic/citrix-configurationlogging-admin-v1-xd75/get-logsite-xd75.html
# TODO: exit if logging is not available
$logState = Get-LogSite  -AdminAddress $ddcAddress
if ($logState.State -ne "Enabled")
{
    LogError "Problem with $ddcAddress, Logging state is $($logState.State)"
    Return
}

# http://support.citrix.com/proddocs/topic/citrix-configurationlogging-admin-v1-xd75/start-loghighleveloperation-xd75.html
$succeeded = $false #indicates if high level operation succeeded.
$highLevelOp =  Start-LogHighLevelOperation  -AdminAddress $ddcAddress -Source "AddMachineToCatalogAndDesktopGroup.ps1" -Text "WebApp adding to Catalog `'$catalogName`' "

try 
{
    LogDebug ("$newCatalog =  Get-BrokerCatalog -Name $catalogName")
    $newCatalog =  Get-BrokerCatalog -Name $catalogName
    LogInfo("Obtained broker catalog UID $($newCatalog.Uid) Full variable is $newCatalog")

    Set-BrokerCatalogMetadata  -AdminAddress $ddcAddress -CatalogName $catalogName -LoggingId $highLevelOp.Id -Name 'DIaaS_Status' -Value "Adding Machine Accounts"

    # http://support.citrix.com/proddocs/topic/citrix-adidentity-admin-v2-xd75/get-acctadaccount-xd75.html
    # check to see if there are already account for the account pool able satisfy this request

    LogDebug ("Get-AcctIdentityPool  -AdminAddress $ddcAddress -IdentityPoolName $catalogName -MaxRecordCount 2147483647")
    $acctIdPool = Get-AcctIdentityPool  -AdminAddress $ddcAddress -IdentityPoolName $catalogName -MaxRecordCount 2147483647
    LogInfo("Obtained identity pool for $catalogName, which as UID $($acctIdPool.IdentityPoolUid) Full variable is $acctIdPool")

    # http://support.citrix.com/proddocs/topic/citrix-adidentity-admin-v2-xd75/get-acctadaccount-xd75.html
    # check to see if there are already account for the account pool able satisfy this request
    # Get-AcctADAccount  -AdminAddress $ddcAddress -IdentityPoolUid $acctIdPool.IdentityPoolUid -Lock $False -MaxRecordCount 2147483647 -State 'Available'


    # http://support.citrix.com/proddocs/topic/citrix-adidentity-admin-v2-xd75/new-acctadaccount-xd75.html
    LogDebug("calling $newMachineAccts = New-AcctADAccount  -AdminAddress $ddcAddress -Count $newDesktopCount -IdentityPoolUid $($acctIdPool.IdentityPoolUid) -LoggingId $highLevelOp.Id")
    $newMachineAccts = New-AcctADAccount  -AdminAddress $ddcAddress -Count $newDesktopCount -IdentityPoolUid $acctIdPool.IdentityPoolUid -LoggingId $highLevelOp.Id
    LogInfo("Result is ...")
    LogInfo("$($newMachineAccts)")
    LogInfo("Generated $($newMachineAccts.SuccessfulAccounts) new accounts, for a variable of $($newMachineAccts)")

    if ([string]::IsNullOrEmpty($newMachineAccts)) {
        LogError("Error with New-AcctADAccount, result is null!")
    } 

    Set-BrokerCatalogMetadata  -AdminAddress $ddcAddress -CatalogName $catalogName -LoggingId $highLevelOp.Id -Name 'DIaaS_Status' -Value "Creating New Desktops"

    # http://support.citrix.com/proddocs/topic/citrix-machinecreation-admin-v2-xd75/new-provvm-xd75.html
    LogDebug("Calling New-ProvVM  -ADAccountName $($newMachineAccts.SuccessfulAccounts) -AdminAddress $ddcAddress -LoggingId $($highLevelOp.Id)  -MaxAssistants 5 -ProvisioningSchemeName $catalogName")
    $newVMs = New-ProvVM  -ADAccountName $newMachineAccts.SuccessfulAccounts -AdminAddress $ddcAddress -LoggingId $highLevelOp.Id  -MaxAssistants 5 -ProvisioningSchemeName $catalogName
    LogInfo("New-ProvVM reported $($newVMs)")

    foreach ( $newVM in $newVMs.CreatedVirtualMachines )
    {
        # Lock-ProvVM http://support.citrix.com/proddocs/topic/citrix-machinecreation-admin-v2-xd75/lock-provvm-xd75.html
        LogDebug ("Lock-ProvVM  -AdminAddress $ddcAddress -LoggingId $($highLevelOp.Id) -ProvisioningSchemeName $catalogName -Tag 'Brokered' -VMID @($newVM.VMId)")
        Lock-ProvVM  -AdminAddress $ddcAddress -LoggingId $highLevelOp.Id -ProvisioningSchemeName $catalogName -Tag 'Brokered' -VMID @($newVM.VMId)

        # New-BrokerMachine http://support.citrix.com/proddocs/topic/citrix-broker-admin-v2-xd75/new-brokermachine-xd75.html
        LogDebug ("New-BrokerMachine  -AdminAddress $ddcAddress -CatalogUid $newCatalog.Uid -LoggingId $($highLevelOp.Id) -MachineName $newVM.ADAccountSid")
        $newBrokeredMachine = New-BrokerMachine  -AdminAddress $ddcAddress -CatalogUid $newCatalog.Uid -LoggingId $highLevelOp.Id -MachineName $newVM.ADAccountSid
        LogDebug ("Result from New-BrokerMachine was $newBrokeredMachine")
    }
    $succeeded = $true
}
catch [System.Exception] {
    Set-BrokerCatalogMetadata  -AdminAddress $ddcAddress -CatalogName $catalogName -LoggingId $highLevelOp.Id -Name 'DIaaS_Status' -Value "Add new Desktops failed"
    LogError("Problem with catalog creation " + $_ )
    throw $_
}
finally
{
    # Log high level operation stop, and indicate its success
    # http://support.citrix.com/proddocs/topic/citrix-configurationlogging-admin-v1-xd75/start-loghighleveloperation-xd75.html
    Stop-LogHighLevelOperation  -AdminAddress $ddcAddress -HighLevelOperationId $highLevelOp.Id -IsSuccessful $succeeded
}
#------------------END CatalogAddMachine -------------------------------

#------------------START DesktopGroupAddMachine -------------------------------

# verify services available, initiate logging
$error.clear()
$svcStatus = Get-ConfigServiceStatus  -AdminAddress $ddcAddress
if ($svcStatus -ne "OK")
{
    LogError ("Problem with $ddcAddress, ConfigServiceStatus is $svcStatus")
    Return
}

# Query availability of logging
# http://support.citrix.com/proddocs/topic/citrix-configurationlogging-admin-v1-xd75/get-logsite-xd75.html
# TODO: exit if logging is not available
$logState = Get-LogSite -AdminAddress $ddcAddress
if ($logState.State -ne "Enabled")
{
    LogError ( "Problem with $ddcAddress, Logging state is $($logState.State)" )
    Return
}

# http://support.citrix.com/proddocs/topic/citrix-configurationlogging-admin-v1-xd75/start-loghighleveloperation-xd75.html
$succeeded = $false #indicates if high level operation succeeded.
$highLevelOp =  Start-LogHighLevelOperation  -AdminAddress $ddcAddress -Source "AddMachineToCatalogAndDesktopGroup.ps1" -Text "WebApp adding to DeliveryGroup `'$desktopGrpName`' "

try 
{
    LogDebug("Add-BrokerMachinesToDesktopGroup  -AdminAddress $ddcAddress -Catalog $catalogName -Count $newDesktopCount -DesktopGroup $desktopGrpName -LoggingId $highLevelOp.Id")
    Add-BrokerMachinesToDesktopGroup  -AdminAddress $ddcAddress -Catalog $catalogName -Count $newDesktopCount -DesktopGroup $desktopGrpName -LoggingId $highLevelOp.Id

    Set-BrokerCatalogMetadata  -AdminAddress $ddcAddress -CatalogName $catalogName -LoggingId $highLevelOp.Id -Name 'DIaaS_Status' -Value "Ready"
    $succeeded = $true
}
catch [System.Exception] {
    Set-BrokerCatalogMetadata  -AdminAddress $ddcAddress -CatalogName $catalogName -LoggingId $highLevelOp.Id -Name 'DIaaS_Status' -Value "Add machine to desktop group failed"
    LogError("Problem with desktop delivery group creation " + $_ )
    throw $_
}
finally
{
    # Log high level operation stop, and indicate its success
    # http://support.citrix.com/proddocs/topic/citrix-configurationlogging-admin-v1-xd75/start-loghighleveloperation-xd75.html
    Stop-LogHighLevelOperation  -AdminAddress $ddcAddress -HighLevelOperationId $highLevelOp.Id -IsSuccessful $succeeded
}
LogInfo("Succeeded in creating catalog $catalogName with $machineCount machines" )

