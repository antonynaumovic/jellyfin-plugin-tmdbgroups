# TMDb Episode Groups Plugin for Jellyfin

A Jellyfin plugin that allows you to use TMDB episode groups to provide alternate episode metadata for TV series. Perfect for series with alternate episode orders like DVD order, production order, or franchise-specific orderings.

## Features

- ?? **Episode Group Support**: Use any TMDB episode group for your TV series
- ?? **Metadata Updates**: Automatically update episode titles and descriptions from episode groups
- ?? **Metadata Provider Integration**: Shows as a metadata downloader in Jellyfin's library settings
- ?? **Web Configuration**: Easy-to-use configuration page in Jellyfin dashboard
- ?? **Manual & Automatic Refresh**: Refresh metadata manually or automatically during library scans
- ?? **Series-Specific**: Configure different episode groups for each series

## Use Case Example

**Problem:** You have "Dragon Ball Kai Recut" but TMDB lists it as "Dragon Ball Kai" with the wrong episode order.

**Solution:** Configure the series to use the correct episode group:
- Series: Dragon Ball Z Kai (TMDB ID: `61709`)
- Episode Group: DVD Order (`651d0a14c50ad2010bfffd7f`)
- Full URL: `https://www.themoviedb.org/tv/61709-dragon-ball-z-kai/episode_group/651d0a14c50ad2010bfffd7f`

The plugin will update all episode titles and descriptions to match the DVD order episode group.

## Installation

### Method 1: Manual Installation

1. **Build the plugin:**
   ```powershell
   dotnet build -c Release
   ```

2. **Copy the DLL:**
   ```
   Copy: Jellyfin.Plugin.TMDbEpisodeGroups\bin\Release\net9.0\Jellyfin.Plugin.TMDbEpisodeGroups.dll
   To: <Jellyfin-Data-Folder>/plugins/TMDb Episode Groups/
   ```
   
   Common Jellyfin data folder locations:
   - **Windows:** `%APPDATA%\Jellyfin\Server\plugins\TMDb Episode Groups\`
   - **Linux:** `/var/lib/jellyfin/plugins/TMDb Episode Groups/`
   - **Docker:** `/config/plugins/TMDb Episode Groups/`

3. **Restart Jellyfin**

### Method 2: Plugin Repository (Future)
Once published to the Jellyfin plugin repository, you can install directly from:
- Dashboard ? Plugins ? Catalog ? TMDb Episode Groups

## Configuration

### 1. Add TMDB API Key

1. Navigate to **Dashboard ? Plugins ? TMDb Episode Groups**
2. Enter your **TMDB API Key**
   - Get your API key from [TMDB Settings](https://www.themoviedb.org/settings/api)
   - You need a TMDB account to generate an API key
3. Click **Save**

### 2. Configure Series Episode Groups

1. Select a **TV Series** from the dropdown
   - Only series with TMDB metadata will appear
2. The plugin will fetch available **Episode Groups** for that series
3. Select your desired episode group (e.g., "DVD Order", "Production Order")
4. Click **Save Episode Group**

### 3. Enable Metadata Provider (Optional but Recommended)

To have the plugin automatically update episodes during library scans:

1. Go to **Dashboard ? Libraries**
2. Select your **TV Shows library**
3. Click **Manage library** ? **Metadata providers**
4. Scroll to **Episodes** section
5. Enable **"TMDb Episode Groups"**
6. Move it to your preferred priority order
7. Click **Save**

Now when you refresh metadata for a series, the plugin will automatically provide episode metadata from the configured episode group.

## Usage

### Finding Episode Group IDs

1. Go to the series page on TMDB (e.g., `https://www.themoviedb.org/tv/61709`)
2. Look for **"Episode Groups"** in the sidebar
3. Click on the episode group you want to use
4. Copy the **Episode Group ID** from the URL:
   ```
   https://www.themoviedb.org/tv/61709-dragon-ball-z-kai/episode_group/651d0a14c50ad2010bfffd7f
                                                                      ^^^^^^^^^^^^^^^^^^^^^^^^
                                                                      This is the Episode Group ID
   ```

### Refreshing Episode Metadata

#### Via Configuration Page
- After saving an episode group, click **"Refresh Episode Metadata"** button

#### Via Jellyfin Library
1. Go to your TV series library
2. Right-click on the series ? **Identify** ? **Search**
3. After identification, right-click again ? **Refresh Metadata**
4. The plugin will automatically update episodes if configured

#### Via API
```powershell
# Get available episode groups
Invoke-RestMethod "http://localhost:8096/TMDbEpisodeGroups/EpisodeGroups/61709"

# Refresh specific series
Invoke-RestMethod -Method Post "http://localhost:8096/TMDbEpisodeGroups/RefreshEpisodeMetadata/61709"

# Refresh all configured series
Invoke-RestMethod -Method Post "http://localhost:8096/TMDbEpisodeGroups/RefreshAllEpisodeMetadata"
```

## How It Works

1. **Configuration**: You configure which episode group to use for each series
2. **Detection**: When Jellyfin refreshes episode metadata, the plugin checks if the series has a configured episode group
3. **Fetching**: The plugin fetches episode details from the TMDB episode group
4. **Matching**: Episodes are matched by TMDB ID or season/episode number
5. **Updating**: Episode titles and descriptions are updated

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/TMDbEpisodeGroups/EpisodeGroups/{tmdbSeriesId}` | Get available episode groups for a series |
| POST | `/TMDbEpisodeGroups/RefreshEpisodeMetadata/{tmdbSeriesId}` | Refresh episode metadata for a series |
| POST | `/TMDbEpisodeGroups/RefreshAllEpisodeMetadata` | Refresh metadata for all configured series |

## Technical Details

- **Plugin Name:** TMDb Episode Groups
- **Plugin GUID:** `7e0a7d42-3f8c-4b9e-a1f2-5d8c9e6f4a3b`
- **Namespace:** `Jellyfin.Plugin.TMDbEpisodeGroups`
- **API Route:** `/TMDbEpisodeGroups`
- **Target Framework:** .NET 9.0
- **Jellyfin Version:** 10.8.0+
- **Metadata Provider:** Implements `IRemoteMetadataProvider<Episode, EpisodeInfo>`

## Building from Source

```powershell
# Clone the repository
git clone https://github.com/jellyfin/jellyfin-plugin-tmdbgroups.git
cd jellyfin-plugin-tmdbgroups

# Restore dependencies
dotnet restore

# Build
dotnet build -c Release

# Run tests
dotnet test

# Output DLL location
# Jellyfin.Plugin.TMDbEpisodeGroups\bin\Release\net9.0\Jellyfin.Plugin.TMDbEpisodeGroups.dll
```

## Testing

### Unit Tests
```powershell
dotnet test --verbosity normal
```

The project includes comprehensive unit tests covering:
- TMDB API client functionality
- Configuration management
- Episode metadata matching and updates
- Error handling

### Manual Testing in Jellyfin
1. Install the plugin
2. Configure a series with an episode group
3. Refresh the series metadata
4. Verify episode titles and descriptions updated

## Standalone Plugin

This is a standalone plugin for managing episode metadata using TMDb episode groups:
- ✅ Unique plugin GUID: `7e0a7d42-3f8c-4b9e-a1f2-5d8c9e6f4a3b`
- ✅ Namespace: `Jellyfin.Plugin.TMDbEpisodeGroups`
- ✅ API routes: `/TMDbEpisodeGroups`
- ✅ Assembly name: `Jellyfin.Plugin.TMDbEpisodeGroups.dll`
- ✅ Functionality: Episode metadata from TMDb episode groups

## Troubleshooting

### Plugin Not Appearing
- Verify DLL is in correct plugins folder
- Restart Jellyfin
- Check Jellyfin logs for loading errors

### Episode Groups Not Loading
- Verify TMDB API key is correct
- Ensure series has TMDB ID
- Check that series has episode groups on TMDB

### Metadata Not Updating
- Verify episode group is configured for the series
- Enable plugin in library metadata providers
- Check Jellyfin logs for errors
- Ensure episodes have season/episode numbers

## Contributing

Contributions are welcome! Please feel free to submit pull requests or open issues.

## License

This project is licensed under the same license as Jellyfin.

## Version

**Current Version:** 0.0.0.1

## Credits

Built for the Jellyfin community to support alternate episode orderings.
