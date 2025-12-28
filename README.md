# OIDDA for the Flax Engine
**OIDDA (Organic Intelligent Dynamic Difficulty Adjustment)** is an intelligent difficulty adaptation system designed to create personalised and seamless gaming experiences. Unlike traditional static difficulty systems, OIDDA constantly monitors player behaviour and adapts the game in real time, without requiring manual intervention or difficulty selection menus.

## **The system uses statistical analysis and behavioural assessment algorithms to understand**:
* **The player's playing style**
* **Strengths and weaknesses**
* **Performance in different situations**
* **Progression patterns**

# Installation
To add this plugin project to your game, follow the instructions in the [Flax Engine documentation](https://docs.flaxengine.com/manual/scripting/plugins/plugin-project.html#automated-git-cloning) for adding a plugin project automatically using git or manually.

# Setup
1. Install the plugin into the Flax Engine game project.
2. Assign the variables that are indispensable in GameplayGlobals.
3. Assign the relevant GameplayGlobals within OIDDA Settings.
4. Assign any static agents you feel you are using with a name and then script, agent type.
5. Add in the scene on an actor the OIDDA Manager script.

# ORS
ORS (OIDDA Receiver Senders) are agents that can easily assign/receive variables.

They can be divided into two groups:
* **Static:** Those that are used permanently during the game. 
* **Dynamic:** These are the ones that can be used in anything the developer needs.

There are three types of agents:
* **ReceiverSender** -> Can send and receive data
* **Receiver** -> Can only receive data
* **Sender** -> Can only send data 

`ORS` - a static class used to interact with the ORS class.

## Example of the use of ORS

```csharp
// Static Connection:
ORS.Instance.ConnectORSAgent(this, ORSUtils.ORSType.Sender);
// Dynamic Connection:
ORS.Instance.ConnectORSAgent(ORSUtils.ORSType.ReceiverSender);

// Receive difficulty data as a float
var difficulty = ORS.Instance.ReceiverValue<float>("Difficulty");

// Send the aggression level data to the OIDDA Manager
int aggressiveLevel= 10;
ORS.Instance.SenderValue("Aggressive Level", aggressiveLevel);

// Static Disconnection:
ORS.Instance.DisconnectORSAgent(this);
// Dynamic Disconnection:
ORS.Instance.DisconnectORSAgent();
```

# Future  plans
Add support for a neural network that you can customise.

# Support
You are free to contribute to the plugin to improve it.
You can also create forks with modifications dedicated exclusively to your project.
Also creating ports for other ports such as **Unity**, **Unreal Engine**, **Godot**, etc.
