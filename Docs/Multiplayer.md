# OIDDA multiplayer

OIDDA adapts difficulty **per player** and, optionally, paces the whole session with a **group Director**. It works for local
multiplayer (split screen, hot seat) with no networking at all, and for networked games through the optional `OIDDANetwork`
module. Single player is unchanged: with no extra player and no group, OIDDA behaves exactly as before.

## The three layers

| Layer | What it gives you | Needs the network? |
|---|---|---|
| **Player contexts** | Each player has their own values, personal Director (stress, fatigue, intensity, pacing state), smoothing and delay state. | No |
| **Group** | A Director that paces the session from every player's state, group-level variables and rules. | No |
| **Network** (`OIDDANetwork`) | The server runs OIDDA and sends each client the state it may see; reconnection; optional client metrics. | Yes |

## Quick start

### Local multiplayer

```csharp
var manager = /* your OIDDAManager */;
manager.RegisterPlayer(1);            // player 0 always exists: it is the default player
manager.RegisterPlayer(2);

var ors = new ORS();
ors.QuickSender(1, "deaths", 3f);                  // write player 1's variable (player id comes first)
ors.AddDirectorIntensity(1, 60f, "boss fight");    // personal intensity event for player 1
float intensity = ors.QuickReceiver<float>(2, "stamina");
```

* Player 0 (`OIDDAManager.DefaultPlayerId`) keeps using the scene's `GameplayGlobals`, so existing code keeps working.
* Every other player has a private dictionary of values, starting from the variables as they were when the manager initialized
  (or the values you pass to `RegisterPlayer`).
* `OIDDAManager.MaxPlayers` (default 8) is yours to set. It counts the players, the default one included while it is a person. On a
  dedicated server player 0 is the server, not a person, so it is left out: 8 means 8 clients. A player beyond the limit is refused with a warning.
* The configuration (`OIDDAConfig`) and the manager's thresholds are shared by every player.

### Networked game

1. The `OIDDANetwork` module is part of this repository's targets (`Modules.Add("OIDDANetwork")`; in your own game add the same
   line to your targets). Turn it on in the OIDDA settings: **Network Config > Enable Network**. It is **off by default**.
2. Put one `OIDDANetworkSync` script in the scene, on every peer. It must be a scene object, so that it exists with the same id
   everywhere.
3. Start the network as usual (`NetworkManager.StartServer / StartHost / StartClient`). `Samples/OIDDANetworkDemo` does it for you.

### Turning the network on or off

`Enable Network` (`OIDDASettings.EnableNetwork`) is the only switch. It is read once, when the game (or play mode) starts, so
changing it needs a restart.

| | Off (default) | On |
|---|---|---|
| `OIDDANetworkPlugin` | Installs nothing: no state provider in the ORS, no network events. | Installs the state provider. |
| `OIDDANetworkSync` | Does nothing: it does not register with the replicator and sends and receives nothing. It warns once that the network is off. | Works as described below. |
| ORS and game code | As in single player. | Same API on the server, the host and the clients. |

Because the module stays in the targets, you can turn networking on later (or off again) from the settings, with no code change
and no files to restore. A game that will never use networking can still leave the module out: remove `Modules.Add("OIDDANetwork")`
from both targets and delete the `OIDDANetwork` folder; the base `OIDDA` module does not depend on it. To bring it back, copy the folder
and the two lines back from this repository, then turn the setting on.

If the settings asset is missing the network stays off.

Game code does not change: the **ORS is the only API**, on the server, the host and the clients.

## Player ids

| Where | Id |
|---|---|
| Offline / single player | 0 |
| Host | 0 (`NetworkManager.ServerClientId`, the same as the default player) |
| Remote client | the `ClientId` the engine assigns (1, 2, 3...), valid for one connection |
| Dedicated server | 0 exists but is not a person: it is left out of the group |

The engine gives a client a **new id every time it connects**; see Reconnection below.

## The group

Assign an `OIDDAGroupConfig` JSON asset to `OIDDAManager.GroupConfig` to turn the group on. Without it nothing changes.

| Setting | Default | Meaning |
|---|---|---|
| `Aggregation` | Average | How players' values are combined: `Average`, `Max` (the group follows its most affected player), `Weighted` (by `OIDDAContext.GroupWeight`), `Percentile`. |
| `Percentile` | 0.75 | Used by `Percentile`. |
| `JoinWarmupSeconds` | 10 | A new player counts as neutral for the group (score 0.5, no intensity, stress or fatigue) for this long. Their own rules start at once. |
| `IncludeDefaultPlayer` | on | Turn off where the default player is not a person. The network module does this on a dedicated server. |
| `UpdateInterval` | 0.25 s | How often the group is evaluated. |
| `UseSmoothing`, `SmoothingSpeed` | on, 0.1 | Smoothing of what group rules change. |
| `GroupRules` | empty | Rules of the group Director. They act on group-level variables. |

Thresholds and rates of the group Director are set on `OIDDAManager.GroupDirector`.

**Group variables.** Metrics and rule conditions of every player can read these read-only variables (constants in
`OIDDAGroupVariables`): `Group.Score`, `Group.Intensity`, `Group.Stress`, `Group.Fatigue`, `Group.State` (0 Build, 1 Peak,
2 Fade, 3 Relax), `Group.Multiplier`, `Group.PlayerCount`. For example, a personal rule can fire only while `Group.Intensity`
is above 50. They are never written into a player's own values.

**Leaving a player out of the group.** `ORS.SetGroupExcluded(playerId, true)` (or `OIDDAContext.ExcludedFromGroup`) takes a player
out of every aggregation and out of `Group.PlayerCount`, for example while they are dead or spectating; `false` brings them back.
An excluded player keeps their own difficulty and still reads the `Group.*` variables and the group's state. Nothing else changes:
the flag is not replicated to clients (the game that set it knows it), it survives a reconnection, and a player who comes back counts
at once with their real values (`JoinWarmupSeconds` only covers the first seconds after joining, whether or not they were excluded
meanwhile). If every player is excluded the group uses its neutral values (score 0.5, no intensity, stress or fatigue, 0 players).
`GroupWeight` is separate: it only matters to `Weighted`, and it does not exclude anyone from the other modes.

**Several groups.** Everyone starts in group 0. `ORS.SetPlayerGroup(playerId, groupId)` moves a player to another group (any `uint`
chosen by the game, for example a team number), creating the group the first time someone joins it. Each group has its own Director,
its own variables and its own `Group.*` values, and counts only its members; a player's metrics and rules read the `Group.*` values
of the group the player is in. All groups share the same `GroupConfig` (aggregation, warm-up, rules) and the thresholds in
`GroupDirector`. Moving a player does not start a new warm-up, and an excluded player stays excluded in the new group.

| Setting (`OIDDAManager`) | Default | Meaning |
|---|---|---|
| `MaxGroups` | 8 | Groups besides group 0, which always exists and does not count. A group that is empty and waiting to be removed does not count either. Beyond the limit `SetPlayerGroup` returns false, the player stays where it was, and the log warns once. 0 means only group 0. |
| `GroupGraceSeconds` | 120 | How long an empty group (never group 0) is kept, with its values and Director state, before it is removed. A player who comes back in time finds it as it was. 0 removes it as soon as it is empty. |

The members of the ORS without a group id (`Group`, `GroupState`, `GroupIntensity`, `AddGroupIntensity(amount)`, `GroupQuickSender(name, value)`...)
act on the group of the local player, that is the default player (player 0): group 0, unless you moved it. To act on another group use
the members that take the group id first: `AddGroupIntensity(groupId, amount)`, `GroupQuickSender(groupId, name, value)`,
`GroupQuickReceiver<T>(groupId, name)`, and `ORS.GetGroup(groupId)` for the group object (null if it does not exist). A group
that does not exist is ignored by the senders and makes `GroupQuickReceiver<T>` throw. `OIDDAContext.GroupId` tells which group a player is in.

Reconnection keeps the group: a player who comes back (see Reconnection below) returns to its group, which is created again if it has
been removed meanwhile; if `MaxGroups` is reached by then, the player goes to group 0 and the log warns. Without a group config
`SetPlayerGroup` only stores the id, which takes effect when a group config is assigned. The group id is not part of a saved profile:
set it again when a player joins. On a remote client `SetPlayerGroup` does nothing and warns once.

**Clients.** With the default state format (V3, see "State format") a client receives the state of every group and knows which
group each player it receives is in; the group members of its ORS follow its own group, and `ORS.TryGetGroupState(groupId, out state)`
reads the others. With format V2 clients only receive group 0. On the server, `ORS.GetGroup` gives the group object itself.

**Group API (ORS):** `AddGroupIntensity`, `SetGroupExcluded`, `SetPlayerGroup`, `GetGroup`, `TryGetGroupState`, `GroupState`, `GroupIntensity`, `GroupStress`,
`GroupFatigue`, `IsShouldSpawnGroupEncounter`, `GroupQuickReceiver<T>`, `GroupQuickSender`.

## The network module

* **The server is the authority.** Analysis, rules and the Directors only run on the server or host. On a client the
  `OIDDAManager` stays idle.
* **What a client receives.** By default only its own player's state and the group's. With `ReplicateStateToAll` every client
  also receives the state of other players (for example to show teammates): the players of its own group, or every player
  with `OnlyOwnGroup` off (see "State format").
* **What a client can do.** Read state through the ORS (`CurrentState`, `CurrentIntensity`, `CurrentStress`, `CurrentFatigue`,
  `IsShouldSpawnEncounter`, `Group*`, `LocalPlayerId`, `TryGetPlayerState`, `TryGetGroupState`). Reading a game variable
  throws, and intensity or group changes are ignored with a single warning, because those live on the server.
* **Client metrics.** Off by default. If you turn on `AllowClientMetrics`, a client can report metrics that only exist on the
  client (a reaction time, say) with `ORS.QuickSender` (or `SenderValue` of a connected agent). The server accepts only the metrics listed in
  `AllowedClientMetrics`, clamps each value to the range given there, and limits each client to `MaxReportsPerSecond`. The
  sender is taken from the network layer, never from the message, so a client can only report for itself.

### `OIDDANetworkSync` settings

| Setting | Default | Meaning |
|---|---|---|
| `ReplicateStateToAll` | off | See above. |
| `OnlyOwnGroup` | on | With `ReplicateStateToAll`: each client only receives the players of its own group. Off: every player, whatever the group. |
| `StateChannel` | Reliable | Channel of the state messages: `Reliable` or `Unreliable` (see below). |
| `StateSendInterval` | 0.1 s | Seconds between state messages. |
| `ReconnectGraceSeconds` | 120 | How long the server keeps a disconnected player's state. 0 turns reconnection off. |
| `SendPlayerKey` | on | Client side: send the player key so a reconnection is recognised. |
| `SaveProfiles` | off | Server side: keep each player's profile between sessions (see Saving profiles). |
| `ProfileAutosaveSeconds` | 60 | Seconds between saves of the connected players' profiles. 0: only when they leave and when the server stops. |
| `AllowClientMetrics` | off | See above. |
| `MaxReportsPerSecond` | 10 | Reports per second accepted from each client. |
| `AllowedClientMetrics` | empty | The only metrics a client may report, with their range. |

Only one `OIDDANetworkSync` may be active: a second one warns and does nothing.

### State channel

State messages travel on a reliable channel by default: every message arrives, in order. With `StateChannel = Unreliable` they
go on an unreliable channel instead, so a lost message is never resent and nothing waits for it. This is safe because every
message is a complete picture of what that client may see and replaces the previous one: the next message (by default 0.1 s
later) makes up for a lost one.

Every message carries a sequence number. A client applies a message only if it is newer than the last one it applied, so a
message that arrives late (or twice) is dropped and the state never goes back in time. The numbers wrap around, and a new
connection starts over.

Some messages stay on the reliable channel even when `Unreliable` is set:

* the **first message** to a client, whether it is new or has just been recognised as a returning player, so it always has a state to read;
* any message **bigger than 1000 bytes** (about 35 players with `ReplicateStateToAll`). The engine splits a message bigger than a packet into parts, and losing
  one part loses the whole message, so the reliable channel is the better choice there.

`Identify` and the client metric reports always use the reliable channel.

### State format

`StateFormat` on `OIDDANetworkSync` is the format of the state messages the server sends. **A client reads both formats**, so
it is the server that decides.

* **V3** (default): each player's message part tells which group the player is in, and the message carries the state of **every**
  group, so a client can show the state of all teams. On a client the group members of the ORS (`GroupState`, `GroupIntensity`,
  ...) follow the local player's group, and `ORS.TryGetGroupState(groupId, out state)` reads any group that was sent. Its
  numbers are sent as 16-bit values (see "Message format"), which makes the messages about half the size.
* **V2**: the format of earlier versions of the plugin: players and group 0 only. Use it while some clients are still on an
  older version, which reject version 3. With V2 a client only knows group 0, whichever group it is in on the server.

**Only the players of your group.** With `ReplicateStateToAll`, `OnlyOwnGroup` (on by default) makes the server send one message per
group that has clients, each with that group's players and, with V3, the state of every group, instead of one message with every
player for everybody. A game with several teams then shows each client its teammates only, and each message stays small. It works
with both formats (with V2 the group part is still group 0 only). When everyone is in group 0, which is always the case without
`SetPlayerGroup`, the message is the same as with the option off. Turn it off to let every client see every player.

A message that carries more than 255 groups is cut at 255 (the manager allows 64 besides group 0). Messages bigger than 1000
bytes use the reliable channel (see above): with V3 that happens from 54 groups in an owner-only message.

### Reconnection

Because the id changes on every connection, a returning player is recognised by a **player key**: a random 128-bit secret that
the client creates on first use and keeps in a small file (`player.key` in OIDDA's folder in the user's local application data:
company, product and the folder name set in the OIDDA settings). It is not
related to the user. The client sends it once per connection. If the server was keeping that player's state (they identified
themselves before leaving and came back within `ReconnectGraceSeconds`), it is restored: values, Director state and age
(so no new warm-up). The state is frozen while the player is away. A game with accounts can supply its own key:

```csharp
OIDDAPlayerKey.Provider = () => myAccountId;   // 8 to 64 letters, digits, '-' or '_'
```

A key already used by a connected player cannot be claimed again, and at most 64 disconnected players are kept at once (the
oldest is dropped first).

### Saving profiles

Reconnection keeps a player's state in memory for `ReconnectGraceSeconds`. To keep it **between sessions** (a later day, or after
the server restarts), turn on `SaveProfiles` on the `OIDDANetworkSync` of the server. Everything happens on the server: a client never
sends state, so it cannot change its own profile. Only identified players (the ones that sent a key) have a profile; the default
player (id 0: the host in a listen server, or single player) has none, because its values live in the game's `GameplayGlobals`.

- **What is saved:** the player's values (by name and type), the pacing state of their personal Director (state, intensity, stress,
  fatigue, engagement, time in the state) and their age. Not saved: the group weight, the exclusion flag, the group's state,
  smoothing, ORS delays and timers. Texture and cube texture values are saved as the asset id and loaded again on restore; a texture
  made at run time, or one that no longer exists, is left out. Numbers are stored as floats (a position keeps float precision even
  with large worlds). At most 256 values, names up to 64 characters, strings up to 4096 characters, 64 KB in all.
- **When it is saved:** when the player leaves, every `ProfileAutosaveSeconds` (a few players at a time, with each state update, so many players
  never stall a frame), and when the `OIDDANetworkSync` stops (the server stops or the scene ends).
- **When it is loaded:** when a player identifies and the server has nothing waiting for them in memory (see Reconnection). The
  result is logged as `Restored`. A value is applied only if the player has no value of that name yet, or one of the same type, so an
  old profile cannot break a game that changed. A profile that is not valid (another version, damaged, not a number) is ignored as a
  whole. The time they were away does not count: the profile is restored as it was saved.
- **When it is deleted:** never by itself. A profile stays until it is overwritten by the next save. A profile file is deleted only
  when the provider later saves that player successfully (so an old file cannot come back), or by `OIDDAProfileStore.Delete(key)`
  (for example when an account is deleted). There is no expiry in this version.

**Where it is kept.** A game can set a provider (its own database or cloud save). It is separate from `OIDDAPlayerKey.Provider`,
which only says *who* the player is:

```csharp
public sealed class MyProfiles : IOIDDAProfileProvider
{
    public bool TrySave(string key, byte[] data) { /* store it, return true on success */ }
    public bool TryLoad(string key, out byte[] data) { /* return false if there is none */ }
    public bool TryDelete(string key) { /* ... */ }
}

OIDDAProfileStore.Provider = new MyProfiles();
```

The calls are synchronous and run on the server's game thread, so they must answer quickly. If there is no provider, or it returns
`false` or throws, the profile is written to an **encrypted file** instead (one warning in the log). When loading, both places are
looked at and the more recent profile wins.

**The encrypted file.** AES-256-GCM, with a fresh random nonce at every save. The key is derived with HKDF-SHA256 from a secret and a
random salt stored in each file. The file is bound to its player: a file copied under another player's name is refused, and a changed
byte makes the whole file unreadable. It is named after the SHA-256 of the player key (`<OIDDA folder>/profiles/<hash>.oidp`) and
written through a temporary file, so an interruption never leaves a broken profile. Where AES-GCM is not supported no file is written
(one warning).

The secret is what makes the encryption worth anything. Set your own:

```csharp
OIDDAProfileStore.EncryptionKey = mySecret32Bytes;                       // 32 random bytes kept out of the game's files
// or, from a passphrase (slow on purpose: call it once at start-up)
OIDDAProfileStore.EncryptionKey = OIDDAProfileStore.KeyFromPassphrase(passphrase, salt);
```

A key that is not 32 bytes long means no file is written or read. **If you set none, a random key is made and kept in
`profiles/profile.secret`, next to the profiles.** That only stops casual reading and editing: anyone who can read the folder can
read the key too. A file written with one key cannot be read with another (the profile is then simply not found).

### Performance

With 8 players the server loop allocates about 616 bytes per state tick (owner-only) or 280 bytes (replicate to all), which
is essentially the messages themselves (with the default format V3 it is about 504 and 232 bytes). A message with one player and
the group is 58 bytes in V2 and 47 bytes in V3; each further group adds 18 bytes to every V3 message, so every client also gets
the state of all the groups. The engine splits larger RPC messages by itself (see `NetworkSettings.MaxSyncParts`).

### Client metrics and value types

A client reports a metric as a single number (a float). `ORS.QuickSender` on a client converts the value with
`OIDDAValue.TryToFloat`, the same conversion the server uses to score a metric, so a value gives the same number on both
paths. A type without a number is not sent.

| Type | Number |
|---|---|
| `float`, `int` | the value (a `double` is also accepted when sent from a client) |
| `bool` | 1 or 0 |
| `Vector2`, `Vector3`, `Vector4` | the length |
| `Quaternion` | the length (about 1 for any unit quaternion, so it carries little information) |
| `Color` | the sum of the channels |
| `Transform`, `Matrix` | the length of the translation |

The server clamps the number to the range given in `AllowedClientMetrics`. Send a float yourself if you need a different
conversion.

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| A client reads `Build` / 0 for everything | No state received yet, or no `OIDDANetworkSync` on the server, or the client is not connected, or **Enable Network** is off in the OIDDA settings (the log warns once). |
| Nothing happens on the server | **Enable Network** is off in the OIDDA settings, or there is no `OIDDAManager` in the scene, or `OIDDANetworkSync` is missing or is a duplicate (check the log). |
| `SetPlayerGroup` returns false | The player is not registered, or `MaxGroups` is reached (the log warns once). |
| "N client(s) have no player" warning | `OIDDAManager.MaxPlayers` is smaller than the number of connected clients. |
| `QuickReceiver` throws on a client | By design: game variables live on the server. Read the replicated state instead. |
| Profiles are not saved or restored | `SaveProfiles` is off, the player never sent a key (`SendPlayerKey` off on the client, or the key is not valid), or the file could not be written (see the warnings: no AES-GCM, an `EncryptionKey` that is not 32 bytes, a folder that cannot be written). |
| A returning player starts fresh | `ReconnectGraceSeconds` is 0 or expired, `SendPlayerKey` is off on the client, or the key is not valid. Turn on `DebugMode` on the manager to see "identified / resumed" lines. |
| State seems to lag or skip with `StateChannel = Unreliable` | Expected under packet loss: a lost message is not resent, the next one arrives within `StateSendInterval`. Use `Reliable` if every message matters. |
| A client ignores every state message | Server and client run different plugin versions (the message version must match), or the client is not connected. |
| Client metrics are ignored | `AllowClientMetrics` is off, the metric is not in `AllowedClientMetrics`, or the client is over `MaxReportsPerSecond`. |

With `DebugMode` on in the `OIDDAManager`, the network module logs the mode, `LocalClientId`, the ids of connecting
clients and the reconnection counters.

## Message format

Little-endian. A snapshot is state (1), intensity, stress, fatigue, score, difficulty multiplier (5 numbers), flags
(1: bit 0 = should spawn encounter), player count (2). In version 2 the five numbers are 32-bit floats (24 bytes in all); in
version 3 they are 16-bit values scaled to the range each one can have (0-100 for intensity, stress and fatigue, 0-1 for the
score, 0-10 for the multiplier), 14 bytes in all. The step is 0.0015 for a percentage, so a value comes back within 0.001 of
what the server had; the ends of each range, the state, the spawn flag and the player count are exact. A message is rejected as a whole if its length, version, flags or state
byte are wrong, or if its sequence number is not newer than the last applied; non-finite numbers become 0 and values are
clamped to their legitimate range.

**Version 2.** Header: version (1 byte), flags (1 byte: bit 0 = group present), player count (2 bytes), sequence number
(2 bytes). Then each player: id (4 bytes) and a snapshot; then the group 0 snapshot if flagged.

**Version 3.** Header: version (1 byte), flags (1 byte, must be 0), player count (2 bytes), sequence number (2 bytes), group
count (1 byte). Then each player: id (4 bytes), group id (4 bytes) and a snapshot; then each group: id (4 bytes) and a snapshot.
At most 256 players and 255 groups. A player is 22 bytes and a group 18, so a message with one player and one group is 47 bytes.
A version 3 message of an earlier build of this branch (32 and 28 bytes) is rejected, not misread: use the same build on both sides, or version 2.

## Verification

The logic is tested without the engine, against a simulated copy of the engine's networking API whose signatures were read
from the engine source, with golden-master traces of scripted scenarios for single player, local multiplayer and the group.
It has **not** been run against a live engine session: try it in the editor with a host and at least one client.
