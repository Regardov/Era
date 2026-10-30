"""Existing V7 -> V8, ten real .blend paint variants and a selected master."""
import bpy,bmesh,sys,math,json,hashlib
from pathlib import Path
from mathutils import Vector
HERE=Path(__file__).resolve().parent;SOURCE=HERE.parent;ERA=SOURCE.parents[2]
sys.path[:0]=[str(HERE),str(SOURCE),str(ERA/'ArtSource/CharacterFactory')]
from geometry_pass import run
from pattern_variants import Patterns,CONFIGS,symmetry_report,HEX
from era_character_factory.audit import action_payload,digest
from era_character_factory.qa import mesh_report,evaluated_bounds,surface_contacts
from animate_aura_weaver import set_action
src=SOURCE/'CorrectionV7/AuraWeaver_V7.blend';source_hash=hashlib.sha256(src.read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(src));scene=bpy.context.scene;rig=bpy.data.objects['Archer_Rig'];rig.data.pose_position='REST';scene.frame_set(1)
actions={a.name:digest(action_payload(a)) for a in bpy.data.actions};rest={b.name:[list(row) for row in b.matrix_local] for b in rig.data.bones}
optional={'AW_RunicFocus_Independent','AW_FocusHolster'}
def meshes():return [o for o in rig.children if o.type=='MESH' and o.name not in optional]
before=evaluated_bounds(meshes());M={m.name[3:]:m for m in bpy.data.materials if m.name.startswith('AW_')}
collection=bpy.data.collections.new('V8 / heels and garment fitting');scene.collection.children.link(collection)
geometry=run(rig,M,collection)
patterns=Patterns(HERE);ranking=sorted([dict(c,**patterns.score(c)) for c in CONFIGS],key=lambda c:c['mean_family_layout_error'])
chosen=ranking[0]['id']
if '--pick' in sys.argv:chosen=int(sys.argv[sys.argv.index('--pick')+1])
selected=next(c for c in CONFIGS if c['id']==chosen);patterns.apply(selected)
allmeshes=meshes();qa=mesh_report(rig,allmeshes)
for m in qa['meshes']:
    if m['zero_area_faces']:
        o=bpy.data.objects[m['object']]
        bm=bmesh.new();bm.from_mesh(o.data);print('DEGENERATE_DETAIL',o.name,[(f.index,[list(v.co) for v in f.verts]) for f in bm.faces if f.calc_area()<1e-12]);bm.free()
assert all(m['finite'] and not m['zero_area_faces'] and not m['subdivision'] for m in qa['meshes']),str([m for m in qa['meshes'] if m['zero_area_faces']])
assert all(m['nonmanifold_nonboundary_edges']==0 for m in qa['meshes'])
assert all(not w['unweighted'] and not w['non_normalized'] and w['max_influences']<=4 for w in qa['weights']),str([w for w in qa['weights'] if w['non_normalized'] or w['unweighted']])
symmetry=symmetry_report(geometry['mirror_objects'])
assert all(s['unmatched_painted_faces']==0 for s in symmetry),str(symmetry)
assert actions=={a.name:digest(action_payload(a)) for a in bpy.data.actions}
assert rest=={b.name:[list(row) for row in b.matrix_local] for b in rig.data.bones}
after=evaluated_bounds(allmeshes)
out=HERE/'Previews';out.mkdir(exist_ok=True);variants=HERE/'Variants';variants.mkdir(exist_ok=True)
scene.camera=bpy.data.objects['Camera_Perspective'];scene.cycles.samples=20
def camera(name,target,direction,scale):
    o=bpy.data.objects.get('Camera_'+name)
    if o is None:o=bpy.data.objects.new('Camera_'+name,bpy.data.cameras.new('Camera_'+name));scene.collection.objects.link(o)
    o.location=Vector(target)+Vector(direction).normalized()*3;o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler();o.data.type='ORTHO';o.data.ortho_scale=scale
camera('DetailHeels',(.23,-.07,.085),(1,.14,.16),.40)
camera('DetailFrontBags',(.03,-.12,1.10),(1,-1.3,.14),.72)
def render(name,path,percentage=100):
    scene.camera=bpy.data.objects['Camera_'+name];scene.render.resolution_percentage=percentage;scene.render.filepath=str(path)
    hidden=[]
    if name=='DetailHeels':
        for o in allmeshes:
            if o.name!='AW_Boot_L':hidden.append((o,o.hide_render));o.hide_render=True
    bpy.ops.render.render(write_still=True)
    for o,state in hidden:o.hide_render=state
if '--draft' in sys.argv:
    for name in ['Front','Side','Back','DetailHeels','DetailFrontBags']:render(name,out/('Draft-'+name+'.png'),65)
else:
    for cfg in CONFIGS:
        patterns.apply(cfg);scene['ERA_pattern_variant_label']=cfg['label'];scene.camera=bpy.data.objects['Camera_Perspective'];scene.render.resolution_percentage=100
        bpy.ops.wm.save_as_mainfile(filepath=str(variants/('AuraWeaver_Pattern_'+str(cfg['id']).zfill(2)+'.blend')),compress=True,copy=True)
        for name in ['Front','Back']:render(name,variants/(str(cfg['id']).zfill(2)+'-'+name+'.png'),65)
patterns.apply(selected);scene['ERA_pattern_variant_label']=selected['label'];scene.camera=bpy.data.objects['Camera_Perspective'];scene.render.resolution_percentage=100;scene.cycles.samples=40
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'AuraWeaver_V8.blend'),compress=True)
if '--draft' not in sys.argv:
    for name in ['Front','Side','Back','Perspective','DetailHeels','DetailFrontBags']:render(name,out/(name+'.png'))
pairs=[('AW_PouchAssembly_L','AW_OuterCoat'),('AW_PouchAssembly_R','AW_OuterCoat'),('AW_RearPouch_Long','AW_OuterCoat'),('AW_RearPouch_Short','AW_OuterCoat')]
rest_contacts=surface_contacts(allmeshes,pairs,max_pairs=2);samples=[]
if '--draft' not in sys.argv:
    rig.data.pose_position='POSE'
    for action in bpy.data.actions:
        set_action(rig,action);low,high=action.frame_range
        for t in [0,.5,1]:
            scene.frame_set(round(low+(high-low)*t));bpy.context.view_layer.update();bounds=evaluated_bounds(allmeshes)
            assert all(math.isfinite(x) and abs(x)<8 for x in bounds['min']+bounds['max'])
            samples.append({'action':action.name,'frame':scene.frame_current,'contacts':surface_contacts(allmeshes,pairs,max_pairs=1)})
report={'source':str(src),'source_sha256':source_hash,'source_unchanged':source_hash==hashlib.sha256(src.read_bytes()).hexdigest(),'bone_count':len(rest),
 'rest_bones_unchanged':True,'original_actions_unchanged':True,'actions':list(actions),'geometry':geometry,'selected_variant':chosen,'selected_label':selected['label'],
 'ranking':ranking,'score_scope':'Symmetrized unlit color-family atlas only, not a full-character similarity percentage. Render review is also required.',
 'local_palette_srgb':HEX,'symmetry':symmetry,'qa':qa,'mesh_count':len(allmeshes),'triangles':sum(len(f.vertices)-2 for o in allmeshes for f in o.data.polygons),
 'before_bounds':before,'after_bounds':after,'rest_contacts':rest_contacts,'pose_samples':samples,
 'limits':['Illustration has perspective, lighting and asymmetry; paint positions are normalized and symmetrized, not exact hidden topology.',
           'Animation samples are not full deformation/Unity certification. Unity and game releases were not modified.']}
(HERE/'validation.json').write_text(json.dumps(report,indent=2),encoding='utf8')
(HERE/'variants.json').write_text(json.dumps({'selected':chosen,'ranking':ranking},indent=2),encoding='utf8')
print('V8_PASS_OK',json.dumps({k:report[k] for k in ['selected_variant','mesh_count','triangles','rest_contacts']}))
