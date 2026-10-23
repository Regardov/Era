# Third-party game art

Textured, rigged actors and their skeletal animation clips are by **Kay Lousberg / KayKit**. They are used under **Creative Commons Zero 1.0 Universal (CC0)**. These models and animations are not original ERA artwork.

- Adventurers 1.0: Rogue Hooded and Mage, from https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Adventures-1.0
- Skeletons 1.0: Minion, Warrior, Rogue and Mage, from https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Skeletons-1.0
- Author: https://kaylousberg.com/

Original GLB sources and original license files are retained under `Assets/Era/Art/KayKit`. Native Unity meshes, skins, clips, materials and painted atlases are generated offline by `KayKitImporter.cs`; no external runtime importer is required. ERA adds animation state selection, blending, crouch offsets, equipment attachment and gameplay integration.

The v0.8 Lumen and all seven realm bosses are derived from those same CC0 skeletal actors. ERA adds the ivory/gold palette, satin material, halo, crystals, antlers, crowns, armor and appendages. Their base meshes and animation clips remain KayKit work. Jump phase overlays, landing compression, portal effects, surface/normal maps and interactive grass are authored in this project.

The terrain geometry, detail textures, icons, shrine meshes, crystal mesh, weapon meshes, music and synthesized sounds are authored in this project.
# v0.10 additions

Storm of the Seven is an original programmatically composed battle score (128 BPM, minor orchestral synth/pulse/percussion), authored in MusicComposer.cs. HumanArtBuilder derives smooth, adult-proportioned meshes from the existing CC0 KayKit models while retaining their UVs, skin weights and clips. Original templates and KayKit licensing remain in the project. The supplied item artwork in Resources/UI/item-atlas.png is preserved; UI and loot use sprite regions from that file. Stone walls, resource graphics cards and trial altar decorations are original code-authored meshes.

# v0.11 additions

FacetedArtBuilder replaces the smoothed hero surfaces with original triangle meshes and per-face colors. The ranger, monk, bearded aura keeper, wolf and jester were authored from the visual references supplied by the project owner; reference images are not redistributed. Existing CC0 KayKit skeletons and animation clips are reused. The original pre-smoothing templates remain in Art/KayKit/HumanTemplates. FacetedRealm.shader and the simplified terrain/rock/canopy shading are original project code. HumanArtBuilder is retained as historical source and is no longer part of project preparation.

# v0.15 additions

The four item sheets in Resources/UI/item-sheet-1.png through item-sheet-4.png were supplied by the project owner and are preserved byte-for-byte. IconLibrary maps 120 illustrations by item name; nine other catalogue items keep the existing project icons. Stream meshes, shared river beds, geometric grass blades, shrubs, hills and water shading are authored in project code.

# v0.16 additions

The seven meshes in `ArtSource/Characters/ERA_Character_Workshop.blend` are newly authored Blender geometry, created by the project's `build_characters.py` from the character references supplied by the owner. Reference images are not redistributed. Costume parts, per-face palettes, skin weights and proportion targets are authored in this project. The models reuse the existing CC0 KayKit bone hierarchy; the Unity prefabs retain the KayKit animation clips listed above. The exported FBX files contain armatures and weights, without animation clips. Earlier KayKit source meshes and licenses remain available for provenance and for creatures not replaced in this release.
