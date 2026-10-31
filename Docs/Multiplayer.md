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
* `OIDDAManager.MaxPlayers` (default 8, default player included) is yours to set. A player beyond it is refused with a warning.
* The configuration (`OIDDAConfig`) and the manager's thresholds are shared by every player.

### Networked game

1. The `OIDDANetwork` module is part of this repository's targets. In your own game, add `Modules.Add("OIDDANetwork")` to the
   target (or leave it out, with its folder, for a game without networking).
2. Put one `OIDDANetworkSync` script in the scene, on every peer. It must be a scene object, so that it exists with the same id
   everywhere.
3. Start the network as usual (`NetworkManager.StartServer / StartHost / StartClient`). `Samples/OIDDANetworkDemo` does it for you.

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

**Group API (ORS):** `AddGroupIntensity`, `GroupState`, `GroupIntensity`, `GroupStress`, `GroupFatigue`,
`IsShouldSpawnGroupEncounter`, `GroupQuickReceiver<T>`, `GroupQuickSender`.

## The network module

* **The server is the authority.** Analysis, rules and the Directors only run on the server or host. On a client the
  `OIDDAManager` stays idle.
* **What a client receives.** By default only its own player's state and the group's. With `ReplicateStateToAll` every client
  receives every player's state (for example to show teammates).
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
| `StateChannel` | Reliable | Channel of the state messages: `Reliable` or `Unreliable` (see below). |
| `StateSendInterval` | 0.1 s | Seconds between state messages. |
| `ReconnectGraceSeconds` | 120 | How long the server keeps a disconnected player's state. 0 turns reconnection off. |
| `SendPlayerKey` | on | Client side: send the player key so a reconnection is recognised. |
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

`Identify` and the client metric reports always use the reliable channel. The server and the clients must run the same
version of the plugin: the message format changed with the sequence number (version 2), and a client rejects any other version.

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

### Performance

With 8 players the server loop allocates about 616 bytes per state tick (owner-only) or 280 bytes (replicate to all), which
is essentially the messages themselves. A message with one player and the group is 58 bytes. The engine splits larger RPC
messages by itself (see `NetworkSettings.MaxSyncParts`).

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
| A client reads `Build` / 0 for everything | No state received yet, or no `OIDDANetworkSync` on the server, or the client is not connected. |
| Nothing happens on the server | There is no `OIDDAManager` in the scene, or `OIDDANetworkSync` is missing or is a duplicate (check the log). |
| "N client(s) have no player" warning | `OIDDAManager.MaxPlayers` is smaller than the number of connected clients. |
| `QuickReceiver` throws on a client | By design: game variables live on the server. Read the replicated state instead. |
| A returning player starts fresh | `ReconnectGraceSeconds` is 0 or expired, `SendPlayerKey` is off on the client, or the key is not valid. Turn on `DebugMode` on the manager to see "identified / resumed" lines. |
| State seems to lag or skip with `StateChannel = Unreliable` | Expected under packet loss: a lost message is not resent, the next one arrives within `StateSendInterval`. Use `Reliable` if every message matters. |
| A client ignores every state message | Server and client run different plugin versions (the message version must match), or the client is not connected. |
| Client metrics are ignored | `AllowClientMetrics` is off, the metric is not in `AllowedClientMetrics`, or the client is over `MaxReportsPerSecond`. |

With `DebugMode` on in the `OIDDAManager`, the network module logs the mode, `LocalClientId`, the ids of connecting
clients and the reconnection counters.

## Message format (version 2)

Little-endian. Header: version (1 byte), flags (1 byte: bit 0 = group present), player count (2 bytes), sequence number
(2 bytes). Then each player:
id (4 bytes) and a snapshot; then the group snapshot if flagged. A snapshot is 24 bytes: state (1), intensity, stress,
fatigue, score, difficulty multiplier (5 floats), flags (1: bit 0 = should spawn encounter), player count (2). A message
is rejected as a whole if its length, version, flags or state byte are wrong, or if its sequence number is not newer than the last applied; non-finite numbers become 0 and values are
clamped to their legitimate range.

## Verification

The logic is tested without the engine, against a simulated copy of the engine's networking API whose signatures were read
from the engine source, with golden-master traces of scripted scenarios for single player, local multiplayer and the group.
It has **not** been run against a live engine session: try it in the editor with a host and at least one client.
