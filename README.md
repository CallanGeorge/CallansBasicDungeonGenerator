# Callan's Basic Dungeon Generator

A simple modular dungeon generator I made in Unity while working on my game development projects.

I wanted to understand how procedural dungeon generation actually works rather than just use an existing asset, so I ended up building my own. I've put the project here in case anyone wants to have a look at the code, mess around with it or use it as a starting point for their own project.

It's not intended to be a complete dungeon-generation framework. It's just a relatively straightforward generator with a visualiser so you can see how it works.

## What does it do?

- Generates a main path between a starting room and an ending room using recursive backtracking.
- Adds random branches, including branches that can have their own child branches.
- Checks room bounds to avoid overlapping rooms.
- Can replace existing rooms with compatible alternatives to create additional connections.
- Automatically connects unused doorways that already line up.
- Can place a closure prefab at any remaining unused doorway.
- Includes a visualiser that replays room placement and backtracking so you can watch the dungeon being built.

## Getting started

The project was made in **Unity 6 (6000.3.10f1)**.

1. Clone or download the repository and open the project in Unity.
2. Open `Assets/Scenes/SampleScene.unity`.
3. Press **Play** to watch the dungeon generate through the visualiser.

The sample project includes a collection of basic modular rooms to experiment with. You can change the generation settings in the Inspector to see how they affect the layout.

**Note:** If you're using the visualiser, leave **Generate On Start** disabled on the `DungeonGenerator` component. The visualiser calls `Generate()` itself.

## Using your own rooms

Each room prefab needs:

- A `DungeonRoom` component on its root GameObject.
- A `BoxCollider` assigned to the room's **Bounds** field. Make sure it covers the space the room occupies.
- At least one child GameObject with a `DungeonDoor` component for every possible entrance or exit.

Position each doorway marker at its actual opening, with its **blue (positive Z) axis pointing out of the room**. The generator uses these markers to rotate and position rooms so connected doorways line up and face one another.

Your modular rooms need compatible doorway sizes and positions. The generator can align door markers, but it won't automatically fix incompatible meshes or openings. It uses room bounding boxes to check for overlaps.

Once you've made your prefabs, add them to the generator's **Room Prefabs** list and assign your starting and ending room prefabs.

## Generation settings

| Setting | What it does |
| --- | --- |
| Room Count | Number of rooms on the main path, including the start and end rooms. |
| Max Attempts | Maximum number of placement attempts while generating the main path. |
| Branch Chance | Chance to try generating a branch from each available main-path doorway. |
| Max Branch Rooms | Total additional room budget shared by branches and child branches. |
| Min / Max Branch Length | Target length of each branch. Branches can end early if they get blocked. |
| Max Branch Depth | How many levels of branches can be generated. `1` means no child branches. |
| Child Branch Chance | Chance to attempt a child branch from an available branch doorway. |
| Max Child Branches Per Branch | Limit on the number of direct child branches each branch can generate. |
| Apply Room Upgrades | Allows compatible existing rooms to be replaced when doing so creates an additional connection. |
| Door Closure Prefab | Optional prefab spawned at unused doorways once generation is finished. |

**Room Count isn't the total dungeon size.** For example, a room count of 10 and a maximum of 20 branch rooms gives you up to 30 rooms if generation succeeds.

The door closure prefab is positioned and rotated at each unused doorway marker. Depending on how you've modelled it, you may need to offset its mesh inside the prefab to make it fit properly.

## How it works

The generator builds the main path first. If a room can't be placed or the remaining path gets blocked, it backtracks and tries another option.

Once the main path is complete, it attempts branches and child branches using any available doorways. It then looks for opportunities to swap existing rooms for compatible alternatives with extra connections, connects any remaining doorways that already line up, and closes unused openings if you've supplied a closure prefab.

The code is split into a few classes so it's easier to follow:

| Class | Purpose |
| --- | --- |
| `DungeonGenerator` | Coordinates the different generation stages and exposes the Inspector settings. |
| `MainPathGenerator` | Builds the main route using recursive backtracking. |
| `BranchGenerator` | Generates branches and child branches. |
| `RoomPlacer` | Aligns rooms and checks for overlaps. |
| `DungeonOptimiser` | Looks for compatible room upgrades and connects aligned doorways. |
| `DungeonRoom` / `DungeonDoor` | Describe the rooms, doorways and their connections. |
| `GenerationRecorder` / `DungeonVisualiser` | Record and replay room placement and backtracking. |

## Visualiser

The visualiser is mainly there because I wanted to actually see what the algorithm was doing. You can change **Step Delay** on the `DungeonVisualiser` component to speed it up or slow it down.

It replays **room placement and backtracking**, not every optimisation step. Room upgrades, automatically connected doorways and doorway closures are part of the finished dungeon, but aren't individually animated during playback.

## A few things to know

This is a learning project, and there are a few limitations:

- All rooms need to follow the same doorway conventions and have compatible geometry.
- Collision detection uses bounding boxes rather than checking the exact room meshes.
- Room upgrades are optional, and the generator won't always find opportunities to make extra connections.
- It doesn't generate extra corridor chains to force loops between branches.
- It isn't a full gameplay system - there are no objectives, enemies or room-specific gameplay features included.

Feel free to experiment with it and change things to suit your own project. If you spot an issue or have an idea, feedback is always welcome.

## License

This project is licensed under the MIT License.
