# OIDDANetwork

Optional network support for OIDDA. The base `OIDDA` module does not depend on it: a single-player game can leave this
module out (remove `Modules.Add("OIDDANetwork")` from the target and delete the folder).

## How it works

- **The server is the authority.** Analysis, rules and the Directors only run on the server (or host). A client never
  runs OIDDA: its `OIDDAManager` stays idle.
- **The ORS is the only API, on every peer.** Game code reads `ORS.CurrentState`, `CurrentIntensity`, `CurrentStress`,
  `CurrentFatigue`, `IsShouldSpawnEncounter`, the `Group*` members, `LocalPlayerId` and `TryGetPlayerState` the same way in
  single player, on the host and on a client. On a client they return the state replicated by the server.
- **Player id = client id.** The host plays as player 0 (the default player, `NetworkManager.ServerClientId`); remote
  clients get the id the engine assigns them (1, 2, 3...). On a dedicated server the default player is not a real player and
  is left out of the group.

## Setup

1. Add the `OIDDANetwork` module to your target (done in this repository's targets).
2. Put an `OIDDANetworkSync` script in the scene (next to the `OIDDAManager` works), on every peer, as a scene object so that
   it exists with the same id everywhere.
3. Start the network as usual (`NetworkManager.StartServer / StartHost / StartClient`).

`OIDDANetworkSync` settings:

| Setting | Default | Meaning |
|---|---|---|
| `ReplicateStateToAll` | off | Off: a client only receives its own player's state and the group's. On: every client receives every player's state (for example to show teammates). |
| `StateSendInterval` | 0.1 s | Seconds between state messages. |
| `AllowClientMetrics` | off | Lets clients report metrics that only exist on the client. |
| `MaxReportsPerSecond` | 10 | Reports per second accepted from each client. |
| `AllowedClientMetrics` | empty | The only metrics a client may report, with the range their values are clamped to. |

## On a client

| Call | Result |
|---|---|
| `ORS.CurrentState`, `CurrentIntensity`, `CurrentStress`, `CurrentFatigue`, `IsShouldSpawnEncounter` | The local player's replicated state (neutral until the first message arrives). |
| `ORS.GroupState`, `GroupIntensity`, `GroupStress`, `GroupFatigue`, `IsShouldSpawnGroupEncounter` | The group's replicated state. |
| `ORS.TryGetPlayerState(id)`, `TryGetGroupState` | The same, for any player the server shared with this client. |
| `QuickReceiver`, `ReceiverValue`, `GroupQuickReceiver` | Throw: game variables live on the server. |
| `AddDirectorIntensity`, `AddGroupIntensity`, `GroupQuickSender` | Ignored, with a single warning. |
| `QuickSender`, `SenderValue` | Sent to the server as an untrusted metric if `AllowClientMetrics` is on and the metric is allowed; otherwise ignored. |

## Debugging

With `DebugMode` on in the `OIDDAManager`, the module logs the mode, `LocalClientId` and the id of every client that
connects or disconnects.
