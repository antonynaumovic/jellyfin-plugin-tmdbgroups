[xml]$props = Get-Content 'Directory.Build.props'
$version = $props.Project.PropertyGroup.Version

$zipPath = "artifacts/tmdb-episode-groups_$version.zip"
$hash = (Get-FileHash -Path $zipPath -Algorithm MD5).Hash.ToLower()
Write-Output $hash
