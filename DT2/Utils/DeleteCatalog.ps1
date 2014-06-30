# (c)opyright Citrix Systems
# Author: Donal Lafferty
# Date: 02.01.2014
# Version 0.1.0
#------------------START PARAMETERS-------------------------------
Param (
    [string]$ddcAddress,
    [string]$catalogName,
	[log4net.ILog]$log
)
#------------------END PARAMETERS-------------------------------

#--- TEST SAMPLE ---

$ddcAddress="spondulasddc.spondulas.cloud:80"
$catalogName="TwoMachines"

#------------------START MODULES-------------------------------

Add-pssnapin citrix.*

#------------------END MODULES-------------------------------



#------------------START FUNCTIONS-------------------------------

#------------------END FUNCTIONS-------------------------------

#------------------START MAIN-------------------------------

$error.clear()
$svcStatus = Get-ConfigServiceStatus  -AdminAddress $ddcAddress
if ($svcStatus -ne "OK")
{
	Write-Error "Problem with $ddcAddress, ConfigServiceStatus is $svcStatus"
    Return
}

# Query availability of logging
# http://support.citrix.com/proddocs/topic/citrix-configurationlogging-admin-v1-xd71/get-logsite-xd71.html
# TODO: exit if logging is not available
$logState = Get-LogSite  -AdminAddress $ddcAddress
if ($logState.State -ne "Enabled")
{
	Write-Error "Problem with $ddcAddress, Logging state is $($logState.State)"
    Return
}

# http://support.citrix.com/proddocs/topic/citrix-configurationlogging-admin-v1-xd71/start-loghighleveloperation-xd71.html
$succeeded = $false #indicates if high level operation succeeded.
$highLevelOp =  Start-LogHighLevelOperation  -AdminAddress $ddcAddress -Source "CreateCatalog.ps1" -Text "Delete Machine Catalog `'$catalogName`' "

try 
{
    # http://support.citrix.com/proddocs/topic/citrix-broker-admin-v2-xd71/get-brokercatalog-xd71.html
    $delCatalog =  Get-BrokerCatalog  -AdminAddress $ddcAddress -Name $catalogName
	$catalogIds = @($delCatalog.Uid)

    try {
        # Determine if any machines needs to be placed in maintenance mode and deleted
        $catMachines = Get-BrokerMachine  -AdminAddress $ddcAddress -Filter "CatalogUid -in $CatalogIds" -MaxRecordCount 2147483647 -ReturnTotalRecordCount -Skip 0 
    }
    catch [Citrix.Broker.Admin.SDK.PartialDataException] {
        # TODO: consume the exception so that it does not appear to a C#-based caller
        $error.clear()
    }

# Remove the machines using different settings in @catMachines:

# TODO: does this work for multiple machines?
$MCSVirtualMachineNames = @($catMachines.SID)

$provisionedVMs = Get-ProvVM  -AdminAddress $ddcAddress -MaxRecordCount 2147483647 -ReturnTotalRecordCount -Skip 0
#$provisionedVMs = Get-ProvVM  -AdminAddress $ddcAddress -Filter 'ADAccountSid -in $MCSVirtualMachineNames' -MaxRecordCount 2147483647 -ReturnTotalRecordCount -Skip 0

if ($provisionedVMs)
{
Remove-BrokerMachine  -AdminAddress $ddcAddress -Force -InputObject $catMachines -LoggingId $highLevelOp.Id 

$vmIds = @($provisionedVMs.VMId)
Unlock-ProvVM  -AdminAddress $ddcAddress -LoggingId $highLevelOp.Id -ProvisioningSchemeUid $delCatalog.ProvisioningSchemeId -VMID $vmIds

$vmNames = @($provisionedVMs.VMName)
Remove-ProvVM  -AdminAddress $ddcAddress -ForgetVM -LoggingId $highLevelOp.Id -ProvisioningSchemeUid $delCatalog.ProvisioningSchemeId -RunAsynchronously -VMName $vmNames
}

$provScheme = Get-ProvScheme  -AdminAddress $ddcAddress -MaxRecordCount 2147483647 -ProvisioningSchemeUid $delCatalog.ProvisioningSchemeId

$pcAcct = Get-BrokerRemotePCAccount  -AdminAddress $ddcAddress -CatalogUid $delCatalog.Uid -MaxRecordCount 2147483647

$provSchemeUid = $delCatalog.ProvisioningSchemeId
$provisionedVMs = Get-ProvVM  -AdminAddress $ddcAddress -Filter {ProvisioningSchemeUid -eq $provSchemeUid} -MaxRecordCount 0 -ProvisioningSchemeUid $delCatalog.ProvisioningSchemeId -ReturnTotalRecordCount

# Get-ProvVM : Returned 0 of 0 items
# 
# 	+ CategoryInfo : OperationStopped: (:) [Get-ProvVM], PartialDataException
# 	+ FullyQualifiedErrorId : Citrix.XDPowerShell.Status.PartialData,Citrix.MachineCreation.Sdk.Commands.GetProvVMCommand

Remove-ProvScheme  -AdminAddress $ddcAddress  -LoggingId $highLevelOp.Id -ProvisioningSchemeUid $delCatalog.ProvisioningSchemeId


# Where does the IdentityPoolUid come from?
$acctIdPool = Get-AcctIdentityPool  -AdminAddress $ddcAddress -IdentityPoolName $catalogName

# What is this for?
$acctADAccounts = Get-AcctADAccount  -AdminAddress $ddcAddress -IdentityPoolUid $acctIdPool.IdentityPoolUid -MaxRecordCount 2147483647

if ($acctADAccounts) {
    $acctSIDS = @($acctADAccounts.ADAccountSid)
    Remove-AcctADAccount  -ADAccountSid @($acctSIDS) -AdminAddress $ddcAddress -Force -IdentityPoolUid $acctIdPool.IdentityPoolUid -LoggingId $highLevelOp.Id -RemovalOption 'None'
}

Remove-AcctIdentityPool  -AdminAddress $ddcAddress -IdentityPoolUid $acctIdPool.IdentityPoolUid -LoggingId $highLevelOp.Id 

Remove-BrokerCatalog  -AdminAddress $ddcAddress -InputObject @($delCatalog.Uid) -LoggingId $highLevelOp.Id 

$succeeded = $true
}
catch [System.Exception] {
    throw $_
}
finally
{
    # Log high level operation stop, and indicate its success
    # http://support.citrix.com/proddocs/topic/citrix-configurationlogging-admin-v1-xd71/start-loghighleveloperation-xd71.html
    Stop-LogHighLevelOperation  -AdminAddress $ddcAddress -HighLevelOperationId $highLevelOp.Id -IsSuccessful $succeeded
}
#------------------END MAIN-------------------------------
