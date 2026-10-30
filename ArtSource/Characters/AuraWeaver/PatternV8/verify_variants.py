"""Check all saved variants and the selected V8 file against V7."""
import bpy,sys,json,hashlib
from pathlib import Path
HERE=Path(__file__).resolve().parent;ERA=HERE.parents[3]
sys.path[:0]=[str(HERE),str(HERE.parent),str(ERA/'ArtSource/CharacterFactory')]
from era_character_factory.audit import action_payload,digest
from era_character_factory.qa import mesh_report,surface_contacts
from pattern_variants import symmetry_report
SOURCE=HERE.parent/'CorrectionV7/AuraWeaver_V7.blend'
def contract():
    r=bpy.data.objects['Archer_Rig']
    return {'bones':{b.name:[list(row) for row in b.matrix_local] for b in r.data.bones},'actions':{a.name:digest(action_payload(a)) for a in bpy.data.actions}}
def paint_hash():
    return digest({n:[bpy.data.objects[n].data.materials[f.material_index].name for f in bpy.data.objects[n].data.polygons] for n in ['AW_OuterCoat','AW_Sleeve_L','AW_ShoulderMantle','AW_HoodAssembly']})
bpy.ops.wm.open_mainfile(filepath=str(SOURCE));original=contract();rows=[]
names=['AW_OuterCoat','AW_InnerCoat','AW_HoodAssembly','AW_ShoulderMantle','AW_ShoulderInlay','AW_RearMantleAccent','AW_RearRobeInlay']
for file in sorted((HERE/'Variants').glob('*.blend'))+[HERE/'AuraWeaver_V8.blend']:
    bpy.ops.wm.open_mainfile(filepath=str(file));assert original==contract();r=bpy.data.objects['Archer_Rig'];r.data.pose_position='REST';bpy.context.view_layer.update()
    objects=[o for o in r.children if o.type=='MESH' and o.name not in ['AW_RunicFocus_Independent','AW_FocusHolster']]
    symmetry=symmetry_report(names);assert all(s['unmatched_painted_faces']==0 for s in symmetry)
    qa=mesh_report(r,objects);assert all(not m['zero_area_faces'] and m['finite'] and not m['subdivision'] for m in qa['meshes'])
    assert all(not m['nonmanifold_nonboundary_edges'] for m in qa['meshes'])
    assert all(not w['unweighted'] and not w['non_normalized'] and w['max_influences']<=4 for w in qa['weights'])
    rows.append({'file':file.name,'variant':r['ERA_pattern_variant'],'paint_hash':paint_hash(),'bone_and_action_contract_equal':True,'symmetry':symmetry,'mesh_count':len(objects),'triangles':sum(len(f.vertices)-2 for o in objects for f in o.data.polygons)})
assert len(rows)==11;assert len({r['paint_hash'] for r in rows[:-1]})==10
selected=next(r for r in rows[:-1] if r['variant']==rows[-1]['variant']);assert selected['paint_hash']==rows[-1]['paint_hash']
report={'source_sha256':hashlib.sha256(SOURCE.read_bytes()).hexdigest(),'ten_distinct_material_layouts':True,'selected_master_equals_variant':selected['variant'],'files':rows}
(HERE/'saved-variants-check.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('ALL_TEN_SAVED_VARIANTS_VERIFIED',selected['variant'])
