"""Read the approved archer in a separate Blender process. Never save its file."""
import sys
import json
from pathlib import Path
import bpy

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from era_character_factory.audit import scene_report

ROOT = HERE.parents[1]
source = ROOT / 'ArtSource/Characters/Archer/Archer_Animated.blend'
bpy.ops.wm.open_mainfile(filepath=str(source), use_scripts=False)
rig = bpy.data.objects['Archer_Rig']
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH' and any(m.type == 'ARMATURE' and m.object == rig for m in o.modifiers)]
report = scene_report(rig, meshes)
out = HERE / 'Measurements'
out.mkdir(exist_ok=True)
(out / 'archer-reference.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf8')
print('FACTORY_AUDIT', len(meshes), 'meshes', len(rig.data.bones), 'bones', len(bpy.data.actions), 'actions')
for obj in meshes:
    print(obj.name, len(obj.data.vertices), [g.name[5:] for g in obj.vertex_groups if g.name.startswith('PART_')])
