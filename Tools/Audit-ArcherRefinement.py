"""Blender CLI: --python this.py -- --baseline saved-v022.blend.
Read-only comparison; never saves either Blender source.
"""
import bpy
import json
import sys
import argparse
from pathlib import Path

root=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser()
parser.add_argument('--baseline',type=Path,required=True)
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
def snapshot(path):
    bpy.ops.wm.open_mainfile(filepath=str(path),use_scripts=False)
    rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
    actions={a.name:[(fc.data_path,fc.array_index,[(tuple(k.co),k.interpolation,tuple(k.handle_left),tuple(k.handle_right)) for k in fc.keyframe_points]) for layer in a.layers for strip in layer.strips for bag in strip.channelbags for fc in bag.fcurves] for a in bpy.data.actions}
    bones={b.name:(b.parent.name if b.parent else None,tuple(tuple(row) for row in b.matrix_local)) for b in rig.data.bones}
    meshes={o.name:([tuple(v.co) for v in o.data.vertices],[tuple(p.vertices) for p in o.data.polygons],[(g.name,g.index) for g in o.vertex_groups],[[(g.group,g.weight) for g in v.groups] for v in o.data.vertices],[m.name for m in o.data.materials]) for o in bpy.context.scene.objects if o.type=='MESH'}
    return actions,bones,meshes
old,oldbones,oldmeshes=snapshot(args.baseline)
new,bones,meshes=snapshot(root/'ArtSource/Characters/Archer/Archer_Animated.blend')
changed=[n for n in old if n not in new or old[n]!=new[n]]
allowed={'Archer_'+prefix+suffix for prefix in ['Walk','Run'] for suffix in ['','Left','Right','Back']}
editable={'Quiver_Case','Bow_Wood',*[f'Arrow_{i:02}' for i in range(1,6)]}
body=[n for n in oldmeshes if n not in editable]
report={'baselineActions':len(old),'currentActions':len(new),'preservedActions':[n for n in old if n not in changed],'changedExistingActions':changed,'addedActions':sorted(set(new)-set(old)),'boneCount':len(bones),'restSkeletonUnchanged':bones==oldbones,'bodyMeshesUnchanged':all(oldmeshes[n]==meshes[n] for n in body),'bodyMeshesChecked':body,'originalMeshObjectNamesPreserved':set(oldmeshes)==set(meshes)}
report['ok']=set(changed)==allowed and len(report['preservedActions'])==14 and len(new)==26 and report['restSkeletonUnchanged'] and report['bodyMeshesUnchanged'] and report['originalMeshObjectNamesPreserved']
(root/'Docs/archer-refinement-preservation.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('ARCHER_REFINEMENT_PRESERVATION',json.dumps(report))
assert report['ok'],report
