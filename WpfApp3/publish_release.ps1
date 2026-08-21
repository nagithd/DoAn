param(
    [string]$Runtime = "win-x64",
    [switch]$SelfContained,
    [switch]$KeepBuildOutput
)

$ErrorActionPreference = "Stop"

$ProjectRoot = $PSScriptRoot
$CapstoneRoot = Split-Path (
    Split-Path $ProjectRoot -Parent
) -Parent
$ReleaseRoot = Join-Path `
    $CapstoneRoot `
    "WpfApp3_NonRuntime\Releases\WpfApp3"
$CurrentFolder = Join-Path $ReleaseRoot "current"
$ArchiveFolder = Join-Path $ReleaseRoot "archive"
$StagingFolder = Join-Path $ReleaseRoot ".staging"
$ProjectFile = Join-Path $ProjectRoot "WpfApp3.csproj"

$runningCurrentRelease = @(
    Get-Process -Name "WpfApp3" -ErrorAction SilentlyContinue |
        Where-Object {
            try {
                $_.Path.StartsWith(
                    $CurrentFolder,
                    [StringComparison]::OrdinalIgnoreCase)
            }
            catch {
                $false
            }
        }
)

if ($runningCurrentRelease.Count -gt 0) {
    $processIds = (
        $runningCurrentRelease |
            ForEach-Object { $_.Id }
    ) -join ", "
    throw (
        "WpfApp3 is currently running from the current release " +
        "(PID: $processIds). Close the application before publishing."
    )
}

New-Item -ItemType Directory -Path $ReleaseRoot -Force |
    Out-Null
New-Item -ItemType Directory -Path $ArchiveFolder -Force |
    Out-Null

if (Test-Path -LiteralPath $StagingFolder) {
    Remove-Item -LiteralPath $StagingFolder -Recurse -Force
}

$publishArguments = @(
    "publish",
    $ProjectFile,
    "-c", "Release",
    "-r", $Runtime,
    "-o", $StagingFolder
)

if ($SelfContained) {
    $publishArguments += "--self-contained"
    $publishArguments += "true"
}
else {
    $publishArguments += "--self-contained"
    $publishArguments += "false"
}

dotnet @publishArguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$PublishedExe = Join-Path $StagingFolder "WpfApp3.exe"
if (-not (Test-Path -LiteralPath $PublishedExe)) {
    throw "Published executable was not found: $PublishedExe"
}

if (Test-Path -LiteralPath $CurrentFolder) {
    $timestamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
    $archivePath = Join-Path (
        $ArchiveFolder
    ) "WpfApp3_$timestamp.zip"
    Compress-Archive `
        -Path (Join-Path $CurrentFolder "*") `
        -DestinationPath $archivePath `
        -CompressionLevel Optimal
    Remove-Item -LiteralPath $CurrentFolder -Recurse -Force
}

Move-Item -LiteralPath $StagingFolder -Destination $CurrentFolder

if (-not $KeepBuildOutput) {
    $generatedFolders = @(
        (Join-Path $ProjectRoot "bin"),
        (Join-Path $ProjectRoot "obj")
    )

    foreach ($generatedFolder in $generatedFolders) {
        if (Test-Path -LiteralPath $generatedFolder) {
            Remove-Item `
                -LiteralPath $generatedFolder `
                -Recurse `
                -Force
        }
    }
}

Write-Host ""
Write-Host "Release published successfully:"
Write-Host $CurrentFolder
Write-Host ""
Write-Host "Executable:"
Write-Host (Join-Path $CurrentFolder "WpfApp3.exe")
if (-not $KeepBuildOutput) {
    Write-Host ""
    Write-Host "Generated bin/obj folders were cleaned."
}
