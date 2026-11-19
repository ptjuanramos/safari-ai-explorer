<#
.SYNOPSIS
Uploads the jobs' files from data\ to the storage account's blob container job-data, under the same keys as the
folders (ADR-0004).

.DESCRIPTION
Signs in with your `az login` (Microsoft Entra ID): the account has shared keys switched off, so no key or connection
string is used or needed.

You need Storage Blob Data Contributor on the account or the container (and Storage Table Data Contributor for
-UserPaths). Owner or Contributor alone are not enough: they manage the account but can't read or write its data.

Order, as the jobs write (ADR-0002): each run's files first, then the latest.json that points at them, so the API never
follows a latest.json to a run that isn't there yet. Uploading again replaces the same blobs.

.EXAMPLE
.\infra\upload-job-data.ps1 -DryRun       # list what would be uploaded, upload nothing
.\infra\upload-job-data.ps1               # road risk + the current sightings run
.\infra\upload-job-data.ps1 -UserPaths    # also copy data\user-paths\*.json into the table userpaths
#>
param(
    [switch]$DryRun,
    [switch]$UserPaths,
    [string]$ResourceGroup = 'rg-safari-explorer',
    [string]$Account = 'safariaiexplorer',
    [string]$Container = 'job-data',
    [string]$DataDir = 'data'
)

# Not 'Stop': in Windows PowerShell 5.1 that turns any line az writes to stderr (warnings, progress) into a terminating
# error. Each az call is checked through its exit code instead.
$ErrorActionPreference = 'Continue'

function Write-Step([string]$Text) { Write-Host "`n== $Text" -ForegroundColor Cyan }

function Invoke-Az([string[]]$Arguments) {
    & az @Arguments
    if ($LASTEXITCODE -ne 0) { throw "az $($Arguments[0..2] -join ' ') failed (exit code $LASTEXITCODE)." }
}

function Send-Folder([string]$Source, [string]$Prefix, [string]$Pattern) {
    $arguments = @('storage', 'blob', 'upload-batch', '--account-name', $Account, '--destination', $Container,
        '--source', $Source, '--destination-path', $Prefix, '--overwrite', '--max-connections', '8', '--auth-mode', 'login')
    if ($Pattern) { $arguments += @('--pattern', $Pattern) }
    if ($DryRun) { $arguments += @('--dryrun', '--output', 'table') } else { $arguments += @('--output', 'none') }
    Invoke-Az $arguments
}

function Send-File([string]$File, [string]$Name) {
    if ($DryRun) { Write-Host "would upload $File -> $Name"; return }
    Invoke-Az @('storage', 'blob', 'upload', '--account-name', $Account, '--container-name', $Container,
        '--file', $File, '--name', $Name, '--overwrite', '--auth-mode', 'login', '--output', 'none')
}

try {
    Write-Step 'Checking the sign-in and the storage account'
    $subscription = (az account show --query '[name, id]' -o tsv) -join ' '
    if ($LASTEXITCODE -ne 0) {
        throw "Not signed in. Run: az login   (if it says 'Decryption failed': az account clear, then az login)"
    }
    Write-Host "signed in to subscription $subscription"
    # `az account show` only reads the profile file; getting a storage token proves the sign-in itself works.
    az account get-access-token --resource https://storage.azure.com/ --query expiresOn -o tsv | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw @"
az can't get a token (see the error above). If it says 'Decryption failed', its token cache is unreadable:
  az account clear
  Remove-Item "`$env:USERPROFILE\.azure\msal_token_cache.*"
  az login
"@
    }
    # The upload talks to the blob endpoint directly, so it doesn't need to look the account up in a resource group.
    $blobUrl = "https://$Account.blob.core.windows.net/"
    Write-Host $blobUrl

    # A cheap data-plane call first, so a missing role or a wrong name fails here with a clear message, not halfway
    # through 3 GB. Its own error is printed too.
    az storage blob list --account-name $Account --container-name $Container --num-results 1 --auth-mode login --output none
    if ($LASTEXITCODE -ne 0) {
        $me = az ad signed-in-user show --query id -o tsv 2>$null
        $accountId = az storage account list --query "[?name=='$Account'].id | [0]" -o tsv 2>$null
        if (-not $accountId) {
            throw @"
No storage account '$Account' in subscription $subscription.
  Is it created? Has the Provision Azure workflow finished? (resource group '$ResourceGroup')
  Wrong subscription? az account list -o table, then az account set --subscription <id>
"@
        }
        throw @"
Can't read container '$Container' in $accountId with your sign-in.
If the error above is AuthorizationPermissionMismatch, give yourself the data role (it can take a few minutes to apply):
  az role assignment create --assignee-object-id $me --assignee-principal-type User --role "Storage Blob Data Contributor" --scope $accountId
If it's ContainerNotFound, run the Provision Azure workflow (it creates '$Container').
"@
    }

    Write-Step "Road risk: every park's runs ($DataDir\road-risk)"
    # <place>/<yyyy-mm-dd>/<file>: the runs first, then each park's state.json and latest.json.
    $roadRisk = Join-Path $DataDir 'road-risk'
    Send-Folder $roadRisk 'road-risk' '*/????-??-??/*'
    Send-Folder $roadRisk 'road-risk' '*/state.json'
    Send-Folder $roadRisk 'road-risk' '*/latest.json'

    Write-Step 'Sightings: the run latest.json points at'
    $latest = Join-Path $DataDir 'sightings\latest.json'
    $run = (Get-Content $latest -Raw | ConvertFrom-Json).run
    Write-Host "run $run"
    Send-Folder (Join-Path $DataDir "sightings\$run") "sightings/$run" $null
    Send-File $latest 'sightings/latest.json'

    if ($UserPaths) {
        Write-Step "Saved paths: $DataDir\user-paths -> table userpaths"
        if ($DryRun) {
            Get-ChildItem (Join-Path $DataDir 'user-paths') -Filter *.json | ForEach-Object { Write-Host "would import $($_.Name)" }
        }
        else {
            # The API's own import (DefaultAzureCredential picks up the az login). Running it again replaces the same rows.
            $env:STORAGE_ACCOUNT_URL = $blobUrl
            try {
                dotnet run --project web/src/SafariExplorer.Api -- import-user-paths $DataDir
                if ($LASTEXITCODE -ne 0) { throw "import-user-paths failed (exit code $LASTEXITCODE)." }
            }
            finally { Remove-Item Env:STORAGE_ACCOUNT_URL -ErrorAction SilentlyContinue }
        }
    }

    Write-Step 'Done'
    if ($DryRun) { Write-Host '(dry run: nothing was uploaded)' }
    Write-Host "Check: az storage blob list --account-name $Account -c $Container --auth-mode login --prefix sightings/ --num-results 5 -o table"
}
catch {
    Write-Host "`n$($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
