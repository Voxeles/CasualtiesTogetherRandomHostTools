# Casualties: Unknown Random Host Tools

Adds various commands for Hosts to use

# Installation

1. Download CasualtiesTogetherRandomHostTools.dll from [Releases](https://github.com/Voxeles/CasualtiesTogetherRandomHostTools/releases)
2. Put it in "Casualties Unknown Demo/BepInEx/plugins"

# New Commands

1. `SavePlayerState`
- Allows you to save the player's state (inventory, health, crafted recipes)
- ('Health' includes all body stats - like brain health, broken limbs, splints on body, opiates, antidepressants, skill xp, etc.)
- Restore a player's state with `LoadPlayerState`
- Has an auto-save feature where the player states are created automatically (see `SavePlayerStateAuto` command)
- Caveats: Favourites items are saved but not restored upon load, and crafted recipes will appear as uncrafted to the client (limitations of the MP mod)

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
- The messages are translated for everyone, so you can turn the entire lobby spanish if you want to
- Supports Google translate and the DeepL API

7. `KaizoEnabled`
- Enable the Kaizo mode
- Adds a new 'fun' gimmick for every layer
