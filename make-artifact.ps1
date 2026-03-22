$dll = 'Jellyfin.Plugin.TMDbEpisodeGroups/bin/Release/net9.0/Jellyfin.Plugin.TMDbEpisodeGroups.dll'
$zipPath = 'artifacts/tmdb-episode-groups_0.0.0.2.zip'
New-Item -ItemType Directory -Force -Path 'artifacts' | Out-Null
Compress-Archive -Path $dll -DestinationPath $zipPath -Force
$hash = (Get-FileHash -Path $zipPath -Algorithm MD5).Hash.ToLower()
Write-Output $hash
