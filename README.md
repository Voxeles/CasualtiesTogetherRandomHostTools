# Casualties: Unknown Random Host Tools

Adds various commands for Hosts to use

# Installation

1. Download CasualtiesTogetherRandomHostTools.dll from [Releases](https://github.com/Voxeles/CasualtiesTogetherRandomHostTools/releases)
2. Put it in "Casualties Unknown Demo/BepInEx/plugins"

# New Run Settings

1. `Clear spawn location`
- Generates a platform for the player to spawn on
- The platform may be enclosed by walls if there's liquid nearby

2. `Starting depth`
- Sets the depth at which players begin each layer

3. `Generate block line`
- Generates a thick line of inifirock above the starting depth, to prevent the players from going up

# New Commands

1. `SavePlayerState`
- Allows you to save the player's state (inventory, health, crafted recipes)
- ('Health' includes all body stats - like brain health, broken limbs, splints on body, opiates, antidepressants, skill xp, etc.)
- Restore a player's state with `LoadPlayerState`
- Has an auto-save feature where the player states are created automatically (see `SavePlayerStateAuto` command)
- Caveats: 
- - The 'favourited' status is saved, but not restored upon load
- - Crafted recipes will appear as uncrafted to the client. The server will still correctly not award an INT xp bonus for crafting a previously crafted item
- - There's a small delay before all the new stats 'kick in'
- These are caused by the limitation/quirks of the MP mod, and I cannot fix them without forcing clients to install this mod as well.

2. `RunSettings`
- Allows you to change the run settings to any value, including out-of-bounds values
- You can also use this to change the run settings mid-run. Note that this requires reloading the save to work correctly (change the settings, then use the saveandquit command)
- `debugworld` setting does _not_ work! (limitation of the MP mod)

3. `PingSalads`
- Shows you the location and distance of elder thornbacks
- Works in-game and in spectator mode

4. `FinalDestination`
- PvP map, falling off results in instant death

5. `ExplosionOverride`
- Globally overrides all explosion parameters
- You don't have to fill every parameter, just press enter once you've set the ones you're interested in
- Leave the command parameters empty to disable it

6. `AutoTranslate`
- Automatically translates a player's messages
- The messages are translated for everyone, so you can turn the entire lobby Spanish if you want to
- Supports Google translate and the DeepL API

7. `SaveWorldTiles`
- Saves the world's tiles and fluids
- Load via `LoadWorldTiles`
- Caveats:
- - Items, buildings, and enemies are deleted on load and are not saved
- - It _only_ saves world tiles. Nothing else.

8. `SyncTimerOverride`
- Allows you to change the default timer sync period
- Lower values sync data more often, increasing network load but decreasing desync
- Higher values decrease network load, but increase desync
- Use with caution. Mainly useful for changing the default fluid sync timer period, as it is abysmally slow with multiple players and causes fluids to suddenly pop-in
- For example, Kaizo mode changes the fluid timer frequency on layer 2 to 0.15

9. `KaizoEnabled`
- Enable the Kaizo mode
- Adds a new 'fun' gimmick for every layer
