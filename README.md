Avoider
A small Unity plug-in (a C# DLL) that makes a NavMesh Agent run away and hide from another object. Put it on an enemy, point it at the player, and the enemy keeps staring at you until you get close, then it ducks behind the nearest wall you can't see past.
Group members
Connor Hewitt
How it works
When you add the Avoider to an object, it first makes sure everything is set up. If the object isn't a NavMesh Agent, isn't sitting on a baked NavMesh, or has no avoidee assigned, it prints a warning in the Console that says what to fix.
Once the game is running, the avoider always turns to face the avoidee. A few times a second it checks two things: is the avoidee within range, and can it actually see me?
If it's being watched, it looks for a place to hide:
It scatters points around itself using Poisson-disc sampling. That just means the points are spread out evenly and never bunch up.
Each point gets snapped onto the NavMesh. Points that land off the NavMesh are thrown out.
For each point, it shoots a ray from the avoidee's eyes to that spot. If a wall is in the way, the spot is hidden. If the ray gets through, the avoidee could see it, so the point is skipped.
From the hidden spots it can actually walk to, it picks the closest one and runs there at the speed you set.
This keeps repeating while the avoidee moves around, so the avoider keeps finding new hiding spots.
Inspector settings
Avoidee: the object to run from (like the player). Required.
Range: how close the avoidee has to get before the avoider reacts.
Speed: how fast it runs.
Think Interval: how often, in seconds, it re-checks what's going on.
Turn Speed: how quickly it turns to keep looking at the avoidee.
Search Radius: how far around itself it looks for hiding spots.
Point Spacing: the minimum distance between the sample points.
Eye Height: how high the "eyes" are for the line of sight checks.
Obstacle Mask: which layers count as walls that block sight.
Show Gizmos: checkbox to turn the debug drawing on or off for that object.
Gizmos
If Show Gizmos is checked (and Gizmos is turned on in the Scene view), you'll see:
a cyan circle showing the range
a line to the avoidee, green when it can't see the avoider and red when it can
red points for spots the avoidee can see (ignored)
blue points for hidden spots (the candidates)
a yellow circle on the spot it picked
How to use it
Drop AvoiderPlugin.dll into your project's Assets/Plugins folder.
Add a Nav Mesh Agent to whatever should run away.
Bake a NavMesh. In Unity 6 that means adding a Nav Mesh Surface (from the AI Navigation package) to your ground and hitting Bake.
Add the Avoider component to the same object.
Drag the object you want to avoid into the Avoidee slot.
Make sure your walls have colliders and are on a layer that's included in the Obstacle Mask.
Source code
The code is in the AvoiderPlugin folder:
Avoider.cs is the main component.
PoissonDiscSampler.cs generates the Poisson-disc points (based on Bridson's algorithm).
To rebuild the DLL, make a Class Library project in Visual Studio and add both files. Then add references to UnityEngine.CoreModule.dll, UnityEngine.AIModule.dll and UnityEngine.PhysicsModule.dll. You can find them in your Unity install under Editor/Data/Managed/UnityEngine. Build it and grab the DLL from bin/Debug.
Showcase project
The Unity project in this repo has a test scene with a ground plane, some walls to hide behind, a player you can move around, and a capsule with the Avoider on it. Made with Unity 6 (6000.0.71f1).
Sources
Bridson, "Fast Poisson Disk Sampling in Arbitrary Dimensions" (SIGGRAPH 2007)
Unity manual page on managed plug-ins

