# Casualties: Unknown Random Host Tools

This is a host-side only mod, which adds new commands and settings for Hosts to use.
Clients do not need to install this mod for it to work!


## Installation

1. Download CasualtiesTogetherRandomHostTools.dll from [Releases](https://github.com/Voxeles/CasualtiesTogetherRandomHostTools/releases)
2. Put it in "Casualties Unknown Demo/BepInEx/plugins"


## New Run Settings

 - `Clear spawn location` \
Generates a platform for the players to safely spawn on.


 - `Starting depth` \
Sets the depth at which players begin each layer.


 - `Generate block line` \
Generates a thick line of inifirock above the starting depth, to prevent the players from going up.


## New Commands

 - `TimedCommand` \
Executes a command after a specified delay, in seconds.
Optionally annouces the time left until the command executes in the chat. \
Example: `TimedCommand 30 PVP_on rule PVP true`


 - Player state management \
Use `SavePlayerState` to save a player's inventory, health, crafted recipes. \
Restore a player's state with `LoadPlayerState` \
Enable `SavePlayerStateAuto` to create player states automatically every few minutes.


 - `PingSalads` \
Shows you the location and distance of elder thornbacks.


 - `FinalDestination` \
Custom PvP map, falling off results in instant death.


- `KaizoEnabled` \
  Enable the Kaizo mode! \
  Adds a new _fun_ gimmick for every layer: \
  Layer 1: Corrupted world \
  Layer 2: Melting \
  Layer 3: Gunpowder rocks! \
  Layer 4: Sound check \
  Layer 5: Tower of Babel


 - `ExplosionOverride` \
Globally overrides all explosion parameters. \
You don't have to fill every parameter, just press enter once you've set the ones you're interested in.
Leave the command parameters empty to disable it.


 - `AutoTranslate` \
Automatically translates a chat messages for everyone.
Finally, you can now turn the entire lobby Spanish if you want to. \
Supports Google translate (default, free, may timeout) and the DeepL API (requires an account).


 - World management \
Use `SaveWorldTiles` to save the world's tiles and fluids to a file. \
Load via `LoadWorldTiles` \
Caveat: It _only_ saves world tiles. Nothing else.


 - `SyncTimerOverride` \
Allows you to change the default timer sync period.
Lower values sync data more often, increasing network load but decreasing desync.
Higher values decrease network load, but increase desync. \
Use with caution. Mainly useful for changing the default fluid sync timer period, as it is abysmally slow with multiple players and causes fluids to suddenly pop-in.
For example, Kaizo mode changes the fluid timer frequency on layer 2 to `0.15`


 - Host cloning \
Use `SpawnClone` to spawn a clone that mimics your inputs.
The clone starts with a copy of your inventory and health. \
Remove all clones with `RemoveClones`. \
Update their state to match yours with `UpdateClones`.


 - `NamedAnnouncement` \
Sends a chat message with a given player name.
The player name does not have to be a name of a real player. \
The chat message allows for some TextMeshPro formatting tags (\<color>, \<b>, \<i>, \<s>).


 - `WorldReplaceLiquid` \
Replaces all instances of a given liquid tile with another.
Use `@a` to target all liquids on the map.
