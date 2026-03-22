param(
    [string]$Changelog = "- Bug fixes and improvements",
    [string]$TargetAbi = "10.11.0.0"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

[xml]$props = Get-Content 'Directory.Build.props'
$version = $props.Project.PropertyGroup.Version

Write-Host "Building v$version..."
dotnet build -c Release
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "Creating artifact..."
$checksum = & ./make-artifact.ps1

$manifestPath = 'manifest.json'
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json

$entry = [ordered]@{
    changelog = $Changelog
    checksum  = $checksum
    sourceUrl = "https://github.com/antonynaumovic/jellyfin-plugin-tmdbgroups/releases/download/v$version/tmdb-episode-groups_$version.zip"
    targetAbi = $TargetAbi
    timestamp = (Get-Date -Format 'yyyy-MM-ddT00:00:00Z')
    version   = $version
}

$existing = $manifest[0].versions | Where-Object { $_.version -eq $version }
if ($existing) {
    if ($existing.checksum -eq $entry.checksum -and $existing.timestamp -eq $entry.timestamp) {
        Write-Host "Version $version already up to date. Skipping manifest update."
        exit 0
    }
    Write-Host "Recreating manifest entry for v$version (checksum or timestamp changed)."
    $manifest[0].versions = @($manifest[0].versions | Where-Object { $_.version -ne $version })
}

$manifest[0].versions = @([PSCustomObject]$entry) + $manifest[0].versions

ConvertTo-Json -InputObject @($manifest) -Depth 10 | Set-Content $manifestPath -Encoding UTF8

Write-Host "Released v$version (checksum: $checksum)"
