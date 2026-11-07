# OIDDANetwork

Optional network support for OIDDA. The base `OIDDA` module does not depend on it. The module is **off by default**: it does
nothing until **Network Config > Enable Network** is turned on in the OIDDA settings (`OIDDASettings.EnableNetwork`). A game that
will never use networking can also leave it out (remove `Modules.Add("OIDDANetwork")` from the targets and delete the folder).

The full guide is in [`Docs/Multiplayer.md`](../../Docs/Multiplayer.md).

## In short

- **The server is the authority.** OIDDA runs on the server (or host). A client never runs it: its `OIDDAManager` stays idle.
- **The ORS is the only API, on every peer.** `ORS.CurrentState`, `CurrentIntensity`, `CurrentStress`, `CurrentFatigue`,
  `IsShouldSpawnEncounter`, the `Group*` members, `LocalPlayerId` and `TryGetPlayerState` work the same in single player, on
  the host and on a client.
- **State format.** `OIDDANetworkSync.StateFormat` is `V3` by default: each player's group and the state of every group. `V2` is the format of earlier versions (group 0 only) for clients that have not been updated; clients read both (see `Docs/Multiplayer.md`).
- **State channel.** `OIDDANetworkSync.StateChannel` is `Reliable` by default. `Unreliable` sends the periodic state without resends: each message is a full picture with a sequence number, and a late one is dropped (see `Docs/Multiplayer.md`).
- **Player id = client id.** The host plays as player 0; remote clients get the id the engine assigns them (1, 2, 3...).

## Setup

1. Add the `OIDDANetwork` module to your target (done in this repository's targets) and turn on **Enable Network** in the OIDDA settings.
2. Put one `OIDDANetworkSync` script in the scene, on every peer.
3. Start the network (`NetworkManager.StartServer / StartHost / StartClient`), or use `Samples/OIDDANetworkDemo`.

## Contents

| File | Role |
|---|---|
| `OIDDANetworkSync.cs` | The scene script: server tick, RPCs, client identification, settings. |
| `OIDDANetworkPlugin.cs` | Installs the state provider into the ORS (only when `EnableNetwork` is on). |
| `OIDDAStateCache.cs` | The provider: what a client received from the server. |
| `OIDDAServerSession.cs` | Server logic: players per client, what to send, reports, reconnection. |
| `OIDDANetworkCodec.cs` | The message formats (versions 2 and 3), strictly validated. |
| `OIDDAStateFrame.cs` | A decoded message, whatever its version. |
| `OIDDAMetricGate.cs` | Allow-list, clamping and rate limit for client metrics. |
| `OIDDAPlayerKey.cs` | The secret key a client uses to be recognised when it reconnects. |
| `OIDDAProfileStore.cs` | Saves a player's profile between sessions: a provider set by the game, or an AES-256-GCM encrypted file (off unless `SaveProfiles` is on). The profile itself is `OIDDAProfile` in the base module. |
| `ManagerPlayerHost.cs`, `FlaxNetworkEnvironment.cs` | The thin layer over the manager and the engine. |
| `Samples/OIDDANetworkDemo.cs` | A script to try everything from the editor. Delete the folder if unwanted. |

## Debugging

With `DebugMode` on in the `OIDDAManager`, the module logs the mode, `LocalClientId`, the id of every client that connects or
disconnects, and the reconnection counters.
