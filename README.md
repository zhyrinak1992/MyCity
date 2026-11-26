# My City — Unity source project

Version: **v1.6.88 LIFE CYCLE**  
Unity Editor: **6000.3.29f1**

This repository contains the source package recovered from the project archive: C# gameplay systems, Editor tooling, StreamingAssets data, validation scripts, project notes, and the current master specification.

## Important project status
- This is the supplied source package, not a verified final game build.
- The archive's validation report records 90/90 static checks passed. Static checks do not replace Unity compilation, Play Mode, runtime save/load, profiling, or an iOS build.
- The package documentation notes that a ready-made Unity scene is not included; the bootstrap creates the world after a scene is loaded.
- Open it with Unity **6000.3.29f1**.

## Contents
- `Assets/` — C# source, Editor tooling, and StreamingAssets.
- `Packages/` — Unity package manifest.
- `ProjectSettings/` — Unity editor version declaration.
- `Tools/` — static validation scripts.
- `Documents/` and root `README_*` files — project requirements and technical notes.

## Rules
Keep the public repository free of passwords, signing certificates, private keys, and other credentials. Do not commit generated Unity folders such as `Library/` or `Temp/`.
