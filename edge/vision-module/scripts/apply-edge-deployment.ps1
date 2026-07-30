<#
.SYNOPSIS
    Renders deployment.json with real secrets and applies it to an IoT Edge device.

.DESCRIPTION
    The committed manifest carries <PLACEHOLDERS> rather than secrets. This reads the camera
    service-account credentials from Key Vault, fills them in, applies the result, and deletes
    the rendered copy — so the password exists on disk only for the duration of the call and
    never lands in git.

    Credentials go into module *environment variables*, never the module twin: twin desired
    properties are readable in the portal by anyone with reader access on the IoT Hub.

.PARAMETER DeviceId
    The IoT Edge device to deploy to. Must already be registered on the hub.

.EXAMPLE
    ./apply-edge-deployment.ps1 -DeviceId dock-cam-ldn1
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$DeviceId,
    [string]$HubName = 'iot-tracktrash-dev-4ymqn2',
    [string]$VaultName = 'kv-tracktrashdev-4ymqn2',
    [string]$ResourceGroup = 'rg-tracktrash-dev',
    [string]$AcrName,
    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'
$manifest = Join-Path $PSScriptRoot '..\deployment.json' | Resolve-Path

# --- the device has to exist; a deployment to a missing device fails obscurely -----------
$known = az iot hub device-identity show --hub-name $HubName --device-id $DeviceId --query "deviceId" -o tsv 2>$null
if (-not $known) {
    throw "Device '$DeviceId' is not registered on $HubName. Register it first (see PROVISIONING.md), then re-run."
}

# --- secrets ------------------------------------------------------------------------------
Write-Host "Reading camera credentials from $VaultName..."
$camUser = az keyvault secret show --vault-name $VaultName --name camera-agent-username --query value -o tsv
$camPass = az keyvault secret show --vault-name $VaultName --name camera-agent-password --query value -o tsv
if (-not $camUser -or -not $camPass) {
    throw "camera-agent-username / camera-agent-password not readable from $VaultName. " +
          "They are declared in infra/bicep/main.bicep; deploy that (or the scoped secrets template) first."
}

$blobConn = az storage account show-connection-string --resource-group $ResourceGroup `
    --name (az storage account list --resource-group $ResourceGroup --query "[?starts_with(name,'st')] | [0].name" -o tsv) `
    --query connectionString -o tsv

# --- render -------------------------------------------------------------------------------
$rendered = Join-Path ([System.IO.Path]::GetTempPath()) "tnt-edge-$DeviceId-$PID.json"
try {
    $text = Get-Content $manifest -Raw
    $text = $text.Replace('<TNT_API_USERNAME>', $camUser)
    $text = $text.Replace('<TNT_API_PASSWORD>', $camPass)
    $text = $text.Replace('<BLOB_CONNECTION_STRING>', $blobConn)
    if ($AcrName) {
        $acrPass = az acr credential show --name $AcrName --query "passwords[0].value" -o tsv
        $text = $text.Replace('<ACR>', $AcrName).Replace('<ACR_USERNAME>', $AcrName).Replace('<ACR_PASSWORD>', $acrPass)
    }

    # Fail loudly rather than shipping a manifest with a literal placeholder in it.
    $left = [regex]::Matches($text, '<[A-Z_]+>') | ForEach-Object { $_.Value } | Sort-Object -Unique
    if ($left) {
        throw "Unfilled placeholders remain: $($left -join ', '). Pass -AcrName, or fill them in deployment.json."
    }

    Set-Content -Path $rendered -Value $text -Encoding utf8

    if ($WhatIf) {
        Write-Host "WhatIf: would apply $rendered to $DeviceId on $HubName" -ForegroundColor Yellow
        Write-Host "  modules : $(((Get-Content $rendered -Raw | ConvertFrom-Json).modulesContent.'$edgeAgent'.'properties.desired'.modules).PSObject.Properties.Name -join ', ')"
        return
    }

    Write-Host "Applying to $DeviceId on $HubName..."
    az iot edge set-modules --hub-name $HubName --device-id $DeviceId --content $rendered --output none
    Write-Host "Applied. Confirm the module picks up the credentials with:" -ForegroundColor Green
    Write-Host "  az iot hub module-twin show --hub-name $HubName --device-id $DeviceId --module-id dockvision --query 'properties.reported'"
}
finally {
    # The rendered file holds the password in clear text; do not leave it lying around.
    if (Test-Path $rendered) { Remove-Item $rendered -Force }
}
