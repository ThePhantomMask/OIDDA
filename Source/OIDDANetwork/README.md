# OIDDANetwork

Optional network support for OIDDA. The base `OIDDA` module does not depend on it: a single-player game can leave this
module out (remove `Modules.Add("OIDDANetwork")` from the target and delete the folder).

The full guide is in [`Docs/Multiplayer.md`](../../Docs/Multiplayer.md).

## In short

- **The server is the authority.** OIDDA runs on the server (or host). A client never runs it: its `OIDDAManager` stays idle.
- **The ORS is the only API, on every peer.** `ORS.CurrentState`, `CurrentIntensity`, `CurrentStress`, `CurrentFatigue`,
  `IsShouldSpawnEncounter`, the `Group*` members, `LocalPlayerId` and `TryGetPlayerState` work the same in single player, on
  the host and on a client.
- **Player id = client id.** The host plays as player 0; remote clients get the id the engine assigns them (1, 2, 3...).

## Setup

1. Add the `OIDDANetwork` module to your target (done in this repository's targets).
2. Put one `OIDDANetworkSync` script in the scene, on every peer.
3. Start the network (`NetworkManager.StartServer / StartHost / StartClient`), or use `Samples/OIDDANetworkDemo`.

## Contents

| File | Role |
|---|---|
| `OIDDANetworkSync.cs` | The scene script: server tick, RPCs, client identification, settings. |
| `OIDDANetworkPlugin.cs` | Installs the state provider into the ORS. |
| `OIDDAStateCache.cs` | The provider: what a client received from the server. |
| `OIDDAServerSession.cs` | Server logic: players per client, what to send, reports, reconnection. |
| `OIDDANetworkCodec.cs` | The message format, strictly validated. |
| `OIDDAMetricGate.cs` | Allow-list, clamping and rate limit for client metrics. |
| `OIDDAPlayerKey.cs` | The secret key a client uses to be recognised when it reconnects. |
| `ManagerPlayerHost.cs`, `FlaxNetworkEnvironment.cs` | The thin layer over the manager and the engine. |
| `Samples/OIDDANetworkDemo.cs` | A script to try everything from the editor. Delete the folder if unwanted. |

## Debugging

With `DebugMode` on in the `OIDDAManager`, the module logs the mode, `LocalClientId`, the id of every client that connects or
disconnects, and the reconnection counters.
