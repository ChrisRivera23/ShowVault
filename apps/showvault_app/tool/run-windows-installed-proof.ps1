[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [string]$InnoSetupCompiler
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($PSVersionTable.PSVersion.Major -lt 7) {
    throw 'The installed Windows proof requires PowerShell 7 or newer.'
}
if (-not $IsWindows) {
    throw 'The installed Windows proof must run on Windows.'
}
if (-not [System.IO.Path]::IsPathFullyQualified($OutputDirectory) -or
    $OutputDirectory -notmatch '^[A-Za-z]:[\\/].+') {
    throw 'OutputDirectory must be a local absolute Windows drive path.'
}
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) {
    throw "OutputDirectory already exists: $OutputDirectory"
}
if (Test-Path -LiteralPath 'Registry::HKEY_CURRENT_USER\Software\Classes\showvault') {
    throw 'The ShowVault callback scheme is already registered; use an isolated controlled Windows user.'
}

$ScriptDirectory = Split-Path -Parent $PSCommandPath
$AppDirectory = [System.IO.Path]::GetFullPath((Join-Path $ScriptDirectory '..'))
$BuildScript = Join-Path $AppDirectory 'packaging\windows\build-app.ps1'
$WorkRoot = Join-Path ([System.IO.Path]::GetTempPath()) (
    'showvault-windows-proof-' + [guid]::NewGuid().ToString('N')
)
$OwnershipMarker = Join-Path $WorkRoot '.showvault-windows-proof-owned'
$InstallDirectory = Join-Path $WorkRoot 'Installed\ShowVault'
$FixtureName = 'showvault-upgrade-windows-' + [guid]::NewGuid().ToString('N')
$BeforeOutput = Join-Path $WorkRoot 'before-package'
$AfterOutput = Join-Path $WorkRoot 'after-package'
$InstalledExecutable = Join-Path $InstallDirectory 'ShowVault.exe'
$CleanupExecutable = $null

New-Item -ItemType Directory -Path $WorkRoot | Out-Null
Set-Content -LiteralPath $OwnershipMarker -Value 'showvault.windows-proof.v1' -Encoding ascii

function Invoke-CheckedProcess {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$ArgumentList
    )
    $Process = Start-Process -FilePath $FilePath -ArgumentList $ArgumentList -Wait -PassThru
    if ($Process.ExitCode -ne 0) {
        throw "A controlled process failed with exit code $($Process.ExitCode)."
    }
}

function Invoke-UpgradePhase {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][ValidateSet('prepare', 'verify', 'cleanup')][string]$Phase,
        [Parameter(Mandatory = $true)][string]$CaptureDirectory
    )
    $StandardOutput = Join-Path $CaptureDirectory "$Phase.stdout"
    $StandardError = Join-Path $CaptureDirectory "$Phase.stderr"
    $ResultFile = Join-Path $CaptureDirectory (
        'showvault-upgrade-result-' + [guid]::NewGuid().ToString('N') + '.txt'
    )
    if ((Test-Path -LiteralPath $StandardOutput) -or
        (Test-Path -LiteralPath $StandardError) -or
        (Test-Path -LiteralPath $ResultFile)) {
        throw 'The upgrade phase capture destination is invalid.'
    }
    $Process = Start-Process -FilePath $FilePath -ArgumentList @(
        '--showvault-upgrade-phase', $Phase,
        '--showvault-upgrade-result-file', ('"' + $ResultFile + '"')
    ) -RedirectStandardOutput $StandardOutput -RedirectStandardError $StandardError -Wait -PassThru
    $OutputLines = @()
    if (Test-Path -LiteralPath $StandardOutput -PathType Leaf) {
        $OutputLines += @(Get-Content -LiteralPath $StandardOutput -Encoding utf8)
    }
    if (Test-Path -LiteralPath $StandardError -PathType Leaf) {
        $OutputLines += @(Get-Content -LiteralPath $StandardError -Encoding utf8)
    }
    $ResultLines = @()
    if (Test-Path -LiteralPath $ResultFile -PathType Leaf) {
        $ResultLines = @(Get-Content -LiteralPath $ResultFile -Encoding utf8)
    }
    return [pscustomobject]@{
        ExitCode = $Process.ExitCode
        OutputLines = $OutputLines
        ResultLines = $ResultLines
    }
}

function Assert-PreparePhasePassed {
    param([Parameter(Mandatory = $true)]$Result)
    $Statuses = @($Result.ResultLines | Where-Object {
        $_ -match '^SHOWVAULT_UPGRADE_STATUS:[a-z-]+$'
    })
    if ($Statuses -contains 'SHOWVAULT_UPGRADE_STATUS:unavailable-configuration') {
        throw 'The installed before application prepare failed: unavailable-configuration.'
    }
    if ($Statuses -contains 'SHOWVAULT_UPGRADE_STATUS:prepare-harness-failed') {
        throw 'The installed before application prepare failed: harness-prepare-failure.'
    }
    if ($Result.ExitCode -ne 0) {
        throw 'The installed before application prepare failed: command-exit.'
    }
    if ($Statuses -notcontains 'SHOWVAULT_UPGRADE_STATUS:prepare-passed') {
        throw 'The installed before application prepare failed: missing-success-marker.'
    }
}

function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

try {
    $BeforeBuild = @{
        OutputDirectory = $BeforeOutput
        SyntheticUpgradeGeneration = 'before'
        SyntheticFixtureName = $FixtureName
        TestInstallDirectory = $InstallDirectory
    }
    if ($InnoSetupCompiler) { $BeforeBuild.InnoSetupCompiler = $InnoSetupCompiler }
    & $BuildScript @BeforeBuild
    $BeforeInstallers = @(Get-ChildItem -LiteralPath $BeforeOutput -Filter '*-setup.exe' -File -ErrorAction Stop)
    if ($BeforeInstallers.Count -ne 1) { throw 'The before installer set is invalid.' }
    $BeforeInstaller = $BeforeInstallers[0]
    Invoke-CheckedProcess -FilePath $BeforeInstaller.FullName -ArgumentList @(
        '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART'
    )
    if (-not (Test-Path -LiteralPath $InstalledExecutable -PathType Leaf)) {
        throw 'The before application was not installed.'
    }
    $CleanupExecutable = $InstalledExecutable
    $PrepareResult = Invoke-UpgradePhase -FilePath $InstalledExecutable -Phase 'prepare' -CaptureDirectory $WorkRoot
    Assert-PreparePhasePassed -Result $PrepareResult

    $AfterBuild = @{
        OutputDirectory = $AfterOutput
        SyntheticUpgradeGeneration = 'after'
        SyntheticFixtureName = $FixtureName
        TestInstallDirectory = $InstallDirectory
    }
    if ($InnoSetupCompiler) { $AfterBuild.InnoSetupCompiler = $InnoSetupCompiler }
    & $BuildScript @AfterBuild
    $AfterInstallers = @(Get-ChildItem -LiteralPath $AfterOutput -Filter '*-setup.exe' -File -ErrorAction Stop)
    if ($AfterInstallers.Count -ne 1) { throw 'The after installer set is invalid.' }
    $AfterInstaller = $AfterInstallers[0]
    Invoke-CheckedProcess -FilePath $AfterInstaller.FullName -ArgumentList @(
        '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART'
    )
    $VerifyResult = Invoke-UpgradePhase -FilePath $InstalledExecutable -Phase 'verify' -CaptureDirectory $WorkRoot
    if ($VerifyResult.ExitCode -ne 0) {
        throw 'The installed after application did not verify the preserved vault.'
    }
    $EncodedLines = @($VerifyResult.ResultLines | Where-Object { $_ -like 'SHOWVAULT_UPGRADE_REPORT:*' })
    if ($EncodedLines.Count -ne 1) { throw 'The installed proof report export is invalid.' }
    $EncodedLine = $EncodedLines[0]
    $EncodedReport = $EncodedLine.Substring('SHOWVAULT_UPGRADE_REPORT:'.Length)
    $ReportBytes = [Convert]::FromBase64String($EncodedReport)
    $ReportText = [Text.Encoding]::UTF8.GetString($ReportBytes)
    $CoreText = $ReportText -replace ',"evidenceSha256":"[0-9a-f]{64}"}$', '}'
    if ($CoreText -eq $ReportText) {
        throw 'The installed proof report has no bounded evidence digest.'
    }
    $Report = $ReportText | ConvertFrom-Json
    $CoreBytes = [Text.Encoding]::UTF8.GetBytes($CoreText)
    $Hasher = [Security.Cryptography.SHA256]::Create()
    try {
        $CoreHash = [Convert]::ToHexString($Hasher.ComputeHash($CoreBytes)).ToLowerInvariant()
    } finally {
        $Hasher.Dispose()
    }
    if ($CoreHash -ne $Report.evidenceSha256) {
        throw 'The installed proof report checksum is invalid.'
    }
    if (-not $Report.scope.windows -or $Report.scope.macOS -or
        -not $Report.preservation.installedArtifactReplaced -or
        -not $Report.preservation.rehydratedWithoutSourceScan -or
        $Report.preservation.sourcePresentDuringRehydration) {
        throw 'The installed Windows preservation claims are incomplete.'
    }
    if ($ReportText -match '(?i)([A-Z]:\\|\\\\|/Users/|/private/|/tmp/|file://|Bearer |accessToken|refreshToken|password|secret)') {
        throw 'The installed proof report contains a prohibited value.'
    }

    New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
    $BeforeEvidenceName = 'ShowVault-before-windows-x64-setup.exe'
    $AfterEvidenceName = 'ShowVault-after-windows-x64-setup.exe'
    Copy-Item -LiteralPath $BeforeInstaller.FullName -Destination (Join-Path $OutputDirectory $BeforeEvidenceName)
    Copy-Item -LiteralPath $AfterInstaller.FullName -Destination (Join-Path $OutputDirectory $AfterEvidenceName)
    $ReportPath = Join-Path $OutputDirectory 'windows-upgrade-diagnostic-report.json'
    [System.IO.File]::WriteAllBytes($ReportPath, $ReportBytes)

    $InstalledSignature = Get-AuthenticodeSignature -LiteralPath $InstalledExecutable
    $BeforeSignature = Get-AuthenticodeSignature -LiteralPath $BeforeInstaller.FullName
    $AfterSignature = Get-AuthenticodeSignature -LiteralPath $AfterInstaller.FullName
    $Metadata = [ordered]@{
        formatVersion = 'showvault.windows-installed-proof.v1'
        operatingSystem = 'Windows'
        operatingSystemVersion = [Environment]::OSVersion.Version.ToString()
        architecture = $env:PROCESSOR_ARCHITECTURE
        beforeInstallerAuthenticodeStatus = $BeforeSignature.Status.ToString()
        afterInstallerAuthenticodeStatus = $AfterSignature.Status.ToString()
        installedExecutableAuthenticodeStatus = $InstalledSignature.Status.ToString()
        callbackSchemeRegisteredForCurrentUser = $true
        externalVaultRetainedByInstaller = $true
        hostReboot = $false
        productionProvider = $false
        personalData = $false
    }
    $MetadataPath = Join-Path $OutputDirectory 'windows-execution-metadata.json'
    $Metadata | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $MetadataPath -Encoding utf8NoBOM

    $ChecksumNames = @(
        $BeforeEvidenceName,
        $AfterEvidenceName,
        'windows-upgrade-diagnostic-report.json',
        'windows-execution-metadata.json'
    )
    $ChecksumLines = foreach ($Name in $ChecksumNames) {
        $Path = Join-Path $OutputDirectory $Name
        "$(Get-Sha256 $Path)  $Name"
    }
    $ChecksumLines | Set-Content -LiteralPath (Join-Path $OutputDirectory 'SHA256SUMS') -Encoding ascii

    $CleanupResult = Invoke-UpgradePhase -FilePath $InstalledExecutable -Phase 'cleanup' -CaptureDirectory $WorkRoot
    if ($CleanupResult.ExitCode -ne 0) { throw 'The synthetic vault cleanup failed.' }
    $CleanupExecutable = $null

    Write-Host "Created controlled installed Windows evidence in $OutputDirectory"
    Write-Host 'Verified: installer replacement, source-free vault rehydration, Restore evidence, diagnostic privacy'
    Write-Host 'Host reboot, production provider, signing readiness, and personal data: not executed'
} finally {
    if ($CleanupExecutable -and (Test-Path -LiteralPath $CleanupExecutable)) {
        $FallbackCleanupRoot = Join-Path $WorkRoot ('cleanup-' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $FallbackCleanupRoot | Out-Null
        Invoke-UpgradePhase -FilePath $CleanupExecutable -Phase 'cleanup' -CaptureDirectory $FallbackCleanupRoot | Out-Null
    }
    $Uninstaller = Join-Path $InstallDirectory 'unins000.exe'
    if (Test-Path -LiteralPath $Uninstaller -PathType Leaf) {
        Start-Process -FilePath $Uninstaller -ArgumentList @(
            '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART'
        ) -Wait | Out-Null
    }
    if ((Test-Path -LiteralPath $OwnershipMarker -PathType Leaf) -and
        ((Get-Content -LiteralPath $OwnershipMarker -Raw).Trim() -eq 'showvault.windows-proof.v1') -and
        ([System.IO.Path]::GetFileName($WorkRoot) -match '^showvault-windows-proof-[0-9a-f]{32}$')) {
        Remove-Item -LiteralPath $WorkRoot -Recurse -Force
    }
}
