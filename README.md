# OIDDA for the Flax Engine
**OIDDA (Organic Intelligent Dynamic Difficulty Adjustment)** is an intelligent difficulty adaptation system designed to create personalised and seamless gaming experiences. Unlike traditional static difficulty systems, OIDDA constantly monitors player behaviour and adapts the game in real time, without requiring manual intervention or difficulty selection menus.

## **The system uses statistical analysis and behavioural assessment algorithms to understand**:
* **The player's playing style**
* **Strengths and weaknesses**
* **Performance in different situations**
* **Progression patterns**

> [!NOTE]
> # Status: Beta (v0.8.7)
> OIDDA is currently in its Beta phase. While the core architecture is stable and features advanced Dynamic Cooldown and Value Smoothing logic—fully optimized for native integration with Flax Engine's Gameplay Globals—the project is still evolving.
> Although the system is production-ready for real-world gameplay implementation, this phase is dedicated to fine-tuning balancing algorithms and ensuring high-scale performance optimization. Developers should be aware of the following:
> * API Stability: Minor breaking changes to the API may occur as we refine the framework toward the 1.0 "Stable" release.
> * Pacing & Calibration: This phase focuses on gathering data to perfect the "feel" of the Pacing Director.
> * Feedback regarding player flow and balancing thresholds is highly encouraged.

# Installation
To add this plugin project to your game, follow the instructions in the [Flax Engine documentation](https://docs.flaxengine.com/manual/scripting/plugins/plugin-project.html#automated-git-cloning) for adding a plugin project automatically using git or manually.

# Setup
1. Install the plugin into the Flax Engine game project.
2. Create GameplayGlobals and assign the variables that are indispensable.
3. Create a OODDAMetrics JsonAsset and assign the metrics and rules/exceptions.
4. Assign the relevant GameplayGlobals and OODDAMetrics JsonAsset within OIDDA Settings.
5. Assign any static agents you feel you are using with a name and then script, agent type.
6. Add in the scene on an actor the OIDDA Manager script.

# ORS
ORS (OIDDA Receiver Senders) are agents that can easily assign/receive variables.

They can be divided into three groups:
* **Static:** Those that are used permanently during the game. 
* **Dynamic:** These are the ones that can be used in anything the developer needs.
* **Quick:** These used without wanting to send data quickly without needing to connect it and are of the ReceiverSender type.

There are three types of agents:
* **ReceiverSender** -> Can send and receive data
* **Receiver** -> Can only receive data
* **Sender** -> Can only send data 

`ORS` - a static class used to interact with the ORS class.

## Example of the use of ORS

```csharp
// Static Connection:
ORS.Instance.ConnectORSAgent("Difficulty Level");
// Dynamic Connection:
ORS.Instance.ConnectORSAgent(ORSUtils.ORSType.ReceiverSender);

// Receive difficulty data as a float (Dynamic)
var difficulty = ORS.Instance.ReceiverValue<float>("Difficulty");
// Receive difficulty data as a float (Static)
var difficulty = ORS.Instance.ReceiverValue<float>();
// Receive Enemy position data as a Vector3 (Quick)
var EnemyPos = ORS.Instance.QuickReceiver<Vector3>("Enemy Pos");

// Send the aggression level data to the OIDDA Manager (Dynamic)
int aggressiveLevel = 10;
ORS.Instance.SenderValue("Aggressive Level", aggressiveLevel);
// Send the difficulty data changed to the OIDDA Manager (Static)
float difficultyChanged = difficulty * 0.2f;
ORS.Instance.SenderValue("Difficulty Level", aggressiveLevel);
// Send the Enemy position data  data to the OIDDA Manager (Quick)
EnemyPos *= Vector3.One;
ORS.Instance.QuickSender("Enemy Pos", EnemyPos);

// Static Disconnection:
ORS.Instance.DisconnectORSAgent("Difficulty Level");
// Dynamic Disconnection:
ORS.Instance.DisconnectORSAgent();
```

# Future Roadmap: 
Current development is laying the groundwork for the 2.0 release, which will introduce neural network-based learning capabilities.

# Support
You are free to contribute to the plugin to improve it.
You can also create forks with modifications dedicated exclusively to your project.
Also creating ports for other ports such as **Unity**, **Unreal Engine**, **Godot**, etc.



