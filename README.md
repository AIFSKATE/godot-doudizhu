# 🃏 Godot-DouZero

![Godot Engine](https://img.shields.io/badge/Godot-4.x-blue?logo=godotengine&logoColor=white) ![C#](https://img.shields.io/badge/C%23-11.0-purple?logo=csharp&logoColor=white) ![AI](https://img.shields.io/badge/AI-DouZero-red?logo=openai&logoColor=white) 

This is a Dou Dizhu (Fight the Landlord) mini-game demo developed based on **Godot Engine (C#)**. The project integrates the reinforcement learning model [DouZero](https://github.com/kwai/DouZero) at the core, while implementing a series of deep optimizations in UI rendering and interaction logic to achieve a silky-smooth card battle experience within a lightweight framework.

## 👀 Demo Screenshots

![Demo Showcase](images/Demo_0.jpg)

## ✨ Core Highlights

* **Native DouZero Integration**: Move beyond traditional rule-tree hardcoding. By directly integrating the DouZero reinforcement learning model, NPCs are endowed with authentic game-theoretic strategies and competitive pressure.

* **Zero-Allocation UI**: The card-playing phase completely bypasses high-overhead `QueueFree()` and instantiation processes. Card entities utilize direct node transferring (**Reparenting**) between the player's hand and the play area, significantly reducing GC (Garbage Collection) pressure.

* **O(1) Hidden Card Updates**: The AI hand display utilizes a precise "tail-end delta elimination" method. Local additions or deletions are only performed when the card count actually changes, avoiding meaningless redraw flickering of the entire list.

* **Math-Driven Layout**: Abandoning built-in horizontal containers, the underlying logic uses custom trigonometric functions to calculate card normal vectors, achieving a beautiful arched fan-shaped expansion.

* **Tween Physical Damping**: Card hovering, playing, and hand reorganization are all managed by Godot's native **Tween**. Featuring animation interruption protection (Kill) and Ease-Out curves, the interaction feels crisp and elastic.

## 🛠️ Tech Stack
* **Game Engine**: Godot (C# Version)
* **Core Language**: C# 
* **AI Algorithm**: DouZero (Reinforcement Learning)
