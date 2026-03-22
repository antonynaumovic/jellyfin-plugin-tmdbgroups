# TMDb Episode Groups Plugin - Usage Guide

## Overview
This plugin allows you to use TMDB episode groups to provide alternate episode metadata (titles and descriptions) for TV series in Jellyfin. This is useful for series with alternate episode orders like DVD order, production order, or franchise-specific orders.

## Example Use Case: Dragon Ball Z Kai
If you have "Dragon Ball Kai Recut" but TMDB thinks it's "Dragon Ball Kai" (TMDb ID: 61709), you can configure it to use the episode group `651d0a14c50ad2010bfffd7f`:

**Full TMDB URL:** `https://www.themoviedb.org/tv/61709-dragon-ball-z-kai/episode_group/651d0a14c50ad2010bfffd7f`

The plugin extracts:
- **TMDb Series ID:** `61709` (from the series metadata in Jellyfin)
- **Episode Group ID:** `651d0a14c50ad2010bfffd7f` (you configure this)

## Installation

1. **Build the plugin:**
   ```powershell
   dotnet build -c Release
   ```

2. **Copy the DLL to Jellyfin plugins folder:**
   ```
   <Jellyfin-Data-Folder>/plugins/TMDb Episode Groups/Jellyfin.Plugin.TMDbEpisodeGroups.dll
   ```
   
   Common locations:
   - Windows: `%APPDATA%\Jellyfin\Server\plugins\TMDb Episode Groups\`
   - Linux: `/var/lib/jellyfin/plugins/TMDb Episode Groups/`
   - Docker: `/config/plugins/TMDb Episode Groups/`

3. **Restart Jellyfin**

## Configuration

### Step 1: Add TMDB API Key
1. Go to **Dashboard → Plugins → TMDb Episode Groups**
2. Enter your **TMDB API Key** (get it from [TMDB Settings](https://www.themoviedb.org/settings/api))
3. Click **Save**

### Step 2: Configure Series Episode Groups
1. In the same plugin configuration page:
2. Select a **TV Series** from the dropdown
3. The plugin will load available **Episode Groups** for that series
4. Select the episode group you want to use (e.g., "DVD Order", "Production Order", etc.)
5. Click **Save Episode Group**

### Step 3: Refresh Episode Metadata

#### Option A: Via Configuration Page
- Click **Refresh Episode Metadata** button after saving the episode group

#### Option B: Via Jellyfin Library
1. Go to your **TV Series** library
2. Right-click on the series → **Identify**
3. In the metadata providers section, **enable "TMDb Episode Groups"**
4. Click **Refresh Metadata**

#### Option C: Via API
```powershell
# Refresh specific series
Invoke-RestMethod -Method Post "http://localhost:8096/TMDbEpisodeGroups/RefreshEpisodeMetadata/61709"

# Get episode groups for a series
Invoke-RestMethod "http://localhost:8096/TMDbEpisodeGroups/EpisodeGroups/61709"
```

## How It Works

### Metadata Provider Integration
The plugin registers as a **metadata provider** in Jellyfin, which means:
- It appears in the list of metadata downloaders for TV episodes
- It automatically runs when you refresh episode metadata
- It only updates episodes for series that have an episode group configured

### Metadata Updates
For each episode in a configured series:
1. The plugin fetches the episode group details from TMDB
2. It matches Jellyfin episodes to TMDB episodes by:
   - TMDB Episode ID (primary match)
   - Season/Episode number (fallback match)
3. It updates:
   - **Episode Title** (Name)
   - **Episode Description** (Overview)

### Episode Group ID Format
The Episode Group ID is the last part of the TMDB episode group URL:

```
https://www.themoviedb.org/tv/[SERIES_ID]-[SERIES_NAME]/episode_group/[EPISODE_GROUP_ID]
                                ^^^^^^^^^^                                ^^^^^^^^^^^^^^^^
                                Series ID                                 Episode Group ID
                             (from Jellyfin metadata)                    (you configure this)
```

Example:
- URL: `https://www.themoviedb.org/tv/61709-dragon-ball-z-kai/episode_group/651d0a14c50ad2010bfffd7f`
- Series ID: `61709` (already in Jellyfin if you used TMDB metadata)
- Episode Group ID: `651d0a14c50ad2010bfffd7f` (enter this in the plugin config)

## Finding Episode Groups

1. Go to your series page on TMDB (e.g., https://www.themoviedb.org/tv/61709)
2. Look for the "Episode Groups" section in the sidebar
3. Click on the episode group you want
4. Copy the Episode Group ID from the URL

## Configuration File Location

The plugin configuration is stored in:
```
<Jellyfin-Data-Folder>/plugins/configurations/Jellyfin.Plugin.TMDbEpisodeGroups.xml
```

Example configuration:
```xml
<PluginConfiguration>
  <TmdbApiKey>your_api_key_here</TmdbApiKey>
  <EpisodeGroupConfigs>
    <EpisodeGroupConfig>
      <TmdbSeriesId>61709</TmdbSeriesId>
      <EpisodeGroupId>651d0a14c50ad2010bfffd7f</EpisodeGroupId>
      <EpisodeGroupName>DVD Order (98 episodes)</EpisodeGroupName>
    </EpisodeGroupConfig>
  </EpisodeGroupConfigs>
</PluginConfiguration>
```

## Troubleshooting

### Plugin doesn't appear in Jellyfin
- Make sure you copied the DLL to the correct plugins folder
- Restart Jellyfin
- Check Jellyfin logs for any plugin loading errors

### Episode groups not loading
- Verify your TMDB API key is correct
- Make sure the series has a TMDB ID in Jellyfin
- Check that the series actually has episode groups on TMDB

### Episodes not updating
- Verify you've configured an episode group for the series
- Check that episodes have season/episode numbers
- Enable "TMDb Episode Groups" in the library metadata providers
- Check Jellyfin logs for error messages

### Metadata provider not showing in Jellyfin
- The provider only appears for **Episode** items
- Make sure the plugin is loaded (check Dashboard → Plugins)
- Restart Jellyfin if needed

## API Endpoints

### Get Episode Groups
```
GET /TMDbEpisodeGroups/EpisodeGroups/{tmdbSeriesId}
```
Returns list of available episode groups for a series.

### Refresh Episode Metadata
```
POST /TMDbEpisodeGroups/RefreshEpisodeMetadata/{tmdbSeriesId}
```
Manually triggers episode metadata refresh for a series.

### Refresh All Configured Series
```
POST /TMDbEpisodeGroups/RefreshAllEpisodeMetadata
```
Refreshes episode metadata for all configured series.

## Version
Current version: **0.0.0.1**

## Plugin Information
This is a standalone plugin with unique identifiers:
- Plugin GUID: `7e0a7d42-3f8c-4b9e-a1f2-5d8c9e6f4a3b`
- Namespace: `Jellyfin.Plugin.TMDbEpisodeGroups`
- API routes: `/TMDbEpisodeGroups`
- Assembly name: `Jellyfin.Plugin.TMDbEpisodeGroups.dll`
