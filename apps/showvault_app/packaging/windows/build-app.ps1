[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [uri]$ApiBaseUrl = 'https://api.showvault.app',

    [switch]$PersonalBetaNoLogin,

    [string]$InnoSetupCompiler,

    [ValidateSet('', 'before', 'after')]
    [string]$SyntheticUpgradeGeneration = '',

    [string]$SyntheticFixtureName,

    [string]$TestInstallDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($PSVersionTable.PSVersion.Major -lt 7) {
    throw 'Windows packaging requires PowerShell 7 or newer.'
}
if (-not $IsWindows) {
    throw 'Windows packaging must run on Windows.'
}
if (-not [System.IO.Path]::IsPathFullyQualified($OutputDirectory) -or
    $OutputDirectory -notmatch '^[A-Za-z]:[\\/].+') {
    throw 'OutputDirectory must be a local absolute Windows drive path.'
}
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) {
    throw "OutputDirectory already exists: $OutputDirectory"
}

$IsLoopback = $ApiBaseUrl.Scheme -eq 'http' -and $ApiBaseUrl.IsLoopback
if ($ApiBaseUrl.Scheme -ne 'https' -and -not $IsLoopback) {
    throw 'ApiBaseUrl must use HTTPS, except loopback HTTP for controlled testing.'
}
if ($PersonalBetaNoLogin -and -not $IsLoopback) {
    throw 'PersonalBetaNoLogin is restricted to a loopback API.'
}
if ($SyntheticUpgradeGeneration) {
    if ($SyntheticFixtureName -notmatch '^showvault-upgrade-[a-z0-9-]{1,80}$') {
        throw 'SyntheticFixtureName is missing or unsafe.'
    }
    if (-not [System.IO.Path]::IsPathFullyQualified($TestInstallDirectory) -or
        $TestInstallDirectory -notmatch '^[A-Za-z]:[\\/].+') {
        throw 'A synthetic upgrade build requires a local absolute TestInstallDirectory.'
    }
    $TestInstallDirectory = [System.IO.Path]::GetFullPath($TestInstallDirectory)
} elseif ($SyntheticFixtureName -or $TestInstallDirectory) {
    throw 'Synthetic fixture/install options require SyntheticUpgradeGeneration.'
}

$Flutter = Get-Command flutter -ErrorAction Stop
$VcpkgRoot = $env:VCPKG_ROOT
if ([string]::IsNullOrWhiteSpace($VcpkgRoot) -or
    -not [System.IO.Path]::IsPathFullyQualified($VcpkgRoot) -or
    $VcpkgRoot -notmatch '^[A-Za-z]:[\\/][^\r\n]+$') {
    throw 'VCPKG_ROOT must be an absolute local Windows directory.'
}
$VcpkgRoot = [System.IO.Path]::GetFullPath($VcpkgRoot)
$VcpkgToolchain = Join-Path $VcpkgRoot 'scripts\buildsystems\vcpkg.cmake'
$VcpkgExecutable = Join-Path $VcpkgRoot 'vcpkg.exe'
if (-not (Test-Path -LiteralPath $VcpkgRoot -PathType Container) -or
    -not (Test-Path -LiteralPath $VcpkgToolchain -PathType Leaf) -or
    -not (Test-Path -LiteralPath $VcpkgExecutable -PathType Leaf)) {
    throw 'VCPKG_ROOT does not contain the required vcpkg executable and CMake toolchain.'
}
$env:VCPKG_ROOT = $VcpkgRoot
if (-not $InnoSetupCompiler) {
    $InnoCandidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
    )
    $InnoSetupCompiler = $InnoCandidates |
        Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Leaf) } |
        Select-Object -First 1
}
if (-not $InnoSetupCompiler -or
    -not (Test-Path -LiteralPath $InnoSetupCompiler -PathType Leaf)) {
    throw 'Inno Setup 6 compiler was not found.'
}

$ScriptDirectory = Split-Path -Parent $PSCommandPath
$AppDirectory = [System.IO.Path]::GetFullPath(
    (Join-Path $ScriptDirectory '..\..')
)
$InstallerDefinition = Join-Path $ScriptDirectory 'installer.iss'
$VersionMatch = Select-String -LiteralPath (Join-Path $AppDirectory 'pubspec.yaml') `
    -Pattern '^version: ([0-9]+\.[0-9]+\.[0-9]+)\+([0-9]+)$'
if (@($VersionMatch).Count -ne 1) {
    throw 'pubspec.yaml must contain one numeric application version and build.'
}
$Version = $VersionMatch.Matches[0].Groups[1].Value
$BuildNumber = $VersionMatch.Matches[0].Groups[2].Value
$PackageVersion = "$Version-$BuildNumber"

Push-Location $AppDirectory
try {
    & $Flutter.Source clean
    if ($LASTEXITCODE -ne 0) { throw 'flutter clean failed.' }
    & $Flutter.Source pub get
    if ($LASTEXITCODE -ne 0) { throw 'flutter pub get failed.' }
    $FlutterArguments = @(
        'build',
        'windows',
        '--release',
        "--dart-define=SHOWVAULT_API_BASE_URL=$($ApiBaseUrl.AbsoluteUri.TrimEnd('/'))"
    )
    if ($PersonalBetaNoLogin) {
        $FlutterArguments += '--dart-define=SHOWVAULT_PERSONAL_BETA_BYPASS_AUTH=true'
    }
    if ($SyntheticUpgradeGeneration) {
        $FlutterArguments += "--dart-define=SHOWVAULT_SYNTHETIC_FIXTURE_HOME=$SyntheticFixtureName"
        $FlutterArguments += '--dart-define=SHOWVAULT_UPGRADE_HARNESS=true'
        $FlutterArguments += "--dart-define=SHOWVAULT_UPGRADE_GENERATION=$SyntheticUpgradeGeneration"
    }
    & $Flutter.Source @FlutterArguments
    if ($LASTEXITCODE -ne 0) { throw 'flutter build windows failed.' }
} finally {
    Pop-Location
}

$BuildDirectory = Join-Path $AppDirectory 'build\windows\x64\runner\Release'
$Executable = Join-Path $BuildDirectory 'ShowVault.exe'
foreach ($RequiredPath in @(
    $Executable,
    (Join-Path $BuildDirectory 'flutter_windows.dll'),
    (Join-Path $BuildDirectory 'data\flutter_assets'),
    (Join-Path $BuildDirectory 'data\app.so')
)) {
    if (-not (Test-Path -LiteralPath $RequiredPath)) {
        throw "The complete Windows release was not produced: $RequiredPath"
    }
}

New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$DeployDirectory = Join-Path $OutputDirectory 'ShowVault'
Copy-Item -LiteralPath $BuildDirectory -Destination $DeployDirectory -Recurse

$InnoArguments = @(
    "/DSourceDirectory=$DeployDirectory",
    "/DOutputDirectory=$OutputDirectory",
    "/DAppVersion=$Version",
    "/DPackageVersion=$PackageVersion"
)
if ($TestInstallDirectory) {
    $InnoArguments += "/DInstallDirectory=$TestInstallDirectory"
}
$InnoArguments += $InstallerDefinition
& $InnoSetupCompiler @InnoArguments
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed.' }

$InstallerName = "ShowVault-$PackageVersion-windows-x64-setup.exe"
$InstallerPath = Join-Path $OutputDirectory $InstallerName
if (-not (Test-Path -LiteralPath $InstallerPath -PathType Leaf)) {
    throw 'The Windows installer was not produced.'
}
$ZipName = "ShowVault-$PackageVersion-windows-x64.zip"
$ZipPath = Join-Path $OutputDirectory $ZipName
Compress-Archive -LiteralPath $DeployDirectory -DestinationPath $ZipPath

$Signature = Get-AuthenticodeSignature -LiteralPath (Join-Path $DeployDirectory 'ShowVault.exe')
$InstallerSignature = Get-AuthenticodeSignature -LiteralPath $InstallerPath
$DeploymentFiles = Get-ChildItem -LiteralPath $DeployDirectory -File -Recurse
$Manifest = [ordered]@{
    formatVersion = 'showvault.windows-package.v1'
    appVersion = "$Version+$BuildNumber"
    architecture = 'x64'
    executable = 'ShowVault.exe'
    deploymentFileCount = @($DeploymentFiles).Count
    installer = $InstallerName
    portableArchive = $ZipName
    authenticationCallbackScheme = 'showvault'
    controlPlaneProfile = if ($IsLoopback) { 'controlled-loopback' } else { 'public-https' }
    authenticodeStatus = $Signature.Status.ToString()
    installerAuthenticodeStatus = $InstallerSignature.Status.ToString()
    syntheticUpgradeGeneration = if ($SyntheticUpgradeGeneration) { $SyntheticUpgradeGeneration } else { 'none' }
    externalVaultRemovalPolicy = 'retain-by-default'
}
$ManifestPath = Join-Path $OutputDirectory 'windows-package-manifest.json'
$Manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $ManifestPath -Encoding utf8NoBOM

$ChecksumEntries = foreach ($Artifact in @($InstallerPath, $ZipPath, $ManifestPath)) {
    $Hash = (Get-FileHash -LiteralPath $Artifact -Algorithm SHA256).Hash.ToLowerInvariant()
    "$Hash  $([System.IO.Path]::GetFileName($Artifact))"
}
$ChecksumEntries | Set-Content -LiteralPath (Join-Path $OutputDirectory 'SHA256SUMS') -Encoding ascii

Write-Host "Created Windows package artifacts in $OutputDirectory"
Write-Host "Authentication callback: showvault://callback (registered for the current user)"
Write-Host "External local vault: retained by upgrade and uninstall"
Write-Host "Authenticode status: $($Signature.Status)"
