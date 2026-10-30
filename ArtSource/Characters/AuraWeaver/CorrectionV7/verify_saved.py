"""Read-only checks of the saved V7 against its unchanged V6 source."""
import bpy,bmesh,sys,json,hashlib
from pathlib import Path
HERE=Path(__file__).resolve().parent;ERA=HERE.parents[3]
sys.path.insert(0,str(ERA/'ArtSource/CharacterFactory'))
from era_character_factory.audit import action_payload,digest
from era_character_factory.qa import mesh_report,surface_contacts
source=HERE.parent/'ReferenceV6/AuraWeaver_V6.blend'
core=['AW_HoodAssembly','AW_Hat','AW_Head','AW_Hair','AW_Bodice','AW_Scarf']
def geometry(o):
    return digest({'vertices':[list(v.co) for v in o.data.vertices],
                   'faces':[[list(p.vertices),p.material_index] for p in o.data.polygons],
                   'materials':[m.name for m in o.data.materials]})
def hand(o):
    return {'vertices':len(o.data.vertices),'edges':sorted(tuple(sorted(e.vertices)) for e in o.data.edges),
            'weights':[[[o.vertex_groups[g.group].name,g.weight] for g in v.groups] for v in o.data.vertices]}
def contract():
    r=bpy.data.objects['Archer_Rig']
    return {'bones':{b.name:[list(row) for row in b.matrix_local] for b in r.data.bones},
            'actions':{a.name:digest(action_payload(a)) for a in bpy.data.actions}}
bpy.ops.wm.open_mainfile(filepath=str(source));original=contract();geometry_before={n:geometry(bpy.data.objects[n]) for n in core}
hands_before={s:hand(bpy.data.objects['AW_ConnectedGlove_'+s]) for s in ['L','R']}
bpy.ops.wm.open_mainfile(filepath=str(HERE/'AuraWeaver_V7.blend'));assert original==contract()
assert geometry_before=={n:geometry(bpy.data.objects[n]) for n in core}
assert hands_before=={s:hand(bpy.data.objects['AW_ConnectedGlove_'+s]) for s in ['L','R']}
r=bpy.data.objects['Archer_Rig'];r.data.pose_position='REST';bpy.context.view_layer.update()
cap=bpy.data.objects['AW_HoodAssembly'];bm=bmesh.new();bm.from_mesh(cap.data)
hood={'boundary_edges':sum(e.is_boundary for e in bm.edges),'nonmanifold_edges':sum(not e.is_manifold for e in bm.edges)};bm.free()
assert not hood['boundary_edges'] and not hood['nonmanifold_edges']
objects=[o for o in r.children if o.type=='MESH' and o.name not in ['AW_RunicFocus_Independent','AW_FocusHolster']]
contacts=surface_contacts(objects,[('AW_RearPouch_Long','AW_OuterCoat'),('AW_RearPouch_Short','AW_OuterCoat')])
assert all(c['face_pairs']==0 for c in contacts)
report={'source_unchanged_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'original_skeleton_actions_equal':True,
        'unchanged_geometry_material_assignments':core,'glove_topology_and_all_original_weights_equal':True,
        'closed_hood':hood,'rest_pouch_contacts':contacts,'qa':mesh_report(r,objects)}
(HERE/'saved-file-check.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('SAVED_V7_VERIFIED')
