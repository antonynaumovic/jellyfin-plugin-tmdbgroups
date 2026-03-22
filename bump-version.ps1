[xml]$props = Get-Content 'Directory.Build.props'
$current = $props.Project.PropertyGroup.Version

$parts = $current.Split('.')
$parts[-1] = [string]([int]$parts[-1] + 1)
$next = $parts -join '.'

$props.Project.PropertyGroup.Version = $next
$props.Project.PropertyGroup.AssemblyVersion = $next
$props.Project.PropertyGroup.FileVersion = $next
$props.Save((Resolve-Path 'Directory.Build.props'))

Write-Output "Bumped $current -> $next"
