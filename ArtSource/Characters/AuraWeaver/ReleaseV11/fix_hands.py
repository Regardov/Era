"""Expose the existing articulated gloves by fitting sleeve ends behind the wrists.

The V10 skeleton and all eleven Actions remain unchanged. No archer assets are edited.
"""
import bpy,bmesh,sys,json,hashlib
from pathlib import Path
from mathutils import Vector

HERE=Path(__file__).resolve().parent
SOURCE=HERE.parent
ROOT=SOURCE.parents[2]
sys.path[:0]=[str(SOURCE),str(ROOT/'ArtSource/CharacterFactory')]
from animate_aura_weaver import set_action,action_curves
from era_character_factory.qa import mesh_report

src=SOURCE/'ReleaseV10/AuraWeaver_Lumen_V10.blend'
source_hash=hashlib.sha256(src.read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(src))
r=bpy.data.objects['Archer_Rig'];s=bpy.context.scene
def rig_signature():
    return hashlib.sha256(repr([(b.name,b.parent.name if b.parent else None,tuple(tuple(row) for row in b.matrix_local)) for b in r.data.bones]).encode()).hexdigest()
def action_signature():
    return hashlib.sha256(repr([(a.name,[(f.data_path,f.array_index,[(tuple(k.co),k.interpolation) for k in f.keyframe_points]) for f in action_curves(a)]) for a in sorted(bpy.data.actions,key=lambda a:a.name)]).encode()).hexdigest()
rig_before=rig_signature();actions_before=action_signature()
r.animation_data_clear();r.data.pose_position='REST'
for p in r.pose.bones:p.matrix_basis.identity()
objects=[o for o in r.children if o.type=='MESH' and o.name not in {'AW_RunicFocus_Independent','AW_FocusHolster'}]

def smooth(t):
    t=max(0,min(1,t));return t*t*(3-2*t)

measurements=[]
for side in ['L','R']:
    forearm=r.data.bones['Forearm.'+side]
    hand=r.data.bones['Hand.'+side]
    wrist=hand.head_local.copy()
    axis=(forearm.tail_local-forearm.head_local).normalized()
    sleeve=bpy.data.objects['AW_Sleeve_'+side]
    ends=[(v.co-wrist).dot(axis) for v in sleeve.data.vertices]
    old_end=max(ends);new_end=-.012
    # Change only the distal forearm. Keep the shoulder and authored palette.
    for v,t in zip(sleeve.data.vertices,ends):
        blend=smooth((t+.18)/(old_end+.18))
        center=wrist+axis*t;radial=v.co-center
        v.co-=axis*(old_end-new_end)*blend
        v.co-=radial*(.35*blend)
    assert max((v.co-wrist).dot(axis) for v in sleeve.data.vertices)<.001
    glove=bpy.data.objects['AW_ConnectedGlove_'+side]
    # Extrude the verified V10 wrist boundary into a volumetric glove cuff.
    # The original palm/fingers keep their coordinates and articulated weights.
    assert len(glove.data.vertices)==116,'Unexpected source glove topology'
    wrist_loop=list(range(10))+list(reversed(range(30,40)))
    wrist_set=set(wrist_loop)
    assert all(abs(glove.vertex_groups['Hand.'+side].weight(i)-1)<1e-6 for i in wrist_loop)
    coords=[v.co.copy() for v in glove.data.vertices]
    weights=[[(glove.vertex_groups[w.group].name,w.weight) for w in v.groups] for v in glove.data.vertices]
    kept=[p for p in glove.data.polygons if not all(i in wrist_set for i in p.vertices)]
    faces=[tuple(p.vertices) for p in kept];material_ids=[p.material_index for p in kept]
    charcoal=next(i for i,m in enumerate(glove.data.materials) if m.name=='AW_charcoal')
    hand_inverse=hand.matrix_local.inverted()
    cross=[hand_inverse@coords[i] for i in wrist_loop]
    center=sum(cross,Vector())/len(cross)
    half_x=max(abs(v.x-center.x) for v in cross);half_z=max(abs(v.z-center.z) for v in cross)
    end_center=forearm.tail_local-axis*.07
    # Parallel-transport the hand cross-section: independent bone roll must not
    # twist the cuff between its two rings.
    hand_axis=(hand.tail_local-hand.head_local).normalized()
    rotation=hand_axis.rotation_difference(axis).to_matrix()@hand.matrix_local.to_3x3()
    end_points=[end_center+rotation@Vector(((v.x-center.x)/half_x*.033,0,(v.z-center.z)/half_z*.023)) for v in cross]
    previous=wrist_loop
    for blend in [.5,1.0]:
        ring=[]
        for index,endpoint in zip(wrist_loop,end_points):
            ring.append(len(coords));coords.append(coords[index].lerp(endpoint,blend))
            weights.append([('Hand.'+side,1-blend),('Forearm.'+side,blend)])
        for j in range(20):faces.append((previous[j],previous[(j+1)%20],ring[(j+1)%20],ring[j]));material_ids.append(charcoal)
        previous=ring
    for j in range(9):faces.append((previous[j],previous[j+1],previous[18-j],previous[19-j]));material_ids.append(charcoal)
    mesh=bpy.data.meshes.new('V11 connected glove and wrist cuff');mesh.from_pydata(coords,[],faces)
    for material in glove.data.materials:mesh.materials.append(material)
    glove.data=mesh
    for i,groups in enumerate(weights):
        for name,weight in groups:
            if weight<=0:continue
            group=glove.vertex_groups.get(name) or glove.vertex_groups.new(name=name)
            group.add([i],weight,'REPLACE')
    for polygon,material_id in zip(mesh.polygons,material_ids):polygon.material_index=material_id
    for o in [sleeve,glove]:
        bm=bmesh.new();bm.from_mesh(o.data)
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free();o.data.update()
    measurements.append({'side':side,'sleeve_end_before_m':old_end,'sleeve_end_after_m':max((v.co-wrist).dot(axis) for v in sleeve.data.vertices),'palm_and_fingers_unchanged':True,'new_cuff_vertices':40,'wrist_cuff_connected':True})

HERE.mkdir(exist_ok=True)
previews=HERE/'Previews';previews.mkdir(exist_ok=True)
r.data.pose_position='POSE';set_action(r,bpy.data.actions['AuraWeaver_Idle']);s.frame_set(1)
qa=mesh_report(r,objects)
assert all(m['finite'] and not m['zero_area_faces'] and not m['nonmanifold_nonboundary_edges'] for m in qa['meshes'])
assert all(not w['unweighted'] and not w['non_normalized'] and w['max_influences']<=4 for w in qa['weights'])
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'AuraWeaver_Lumen_V11.blend'),compress=True)
manifest=json.loads((SOURCE/'ReleaseV10/AuraWeaver_Manifest.json').read_text())
manifest['sourceBlendSha256']=hashlib.sha256((HERE/'AuraWeaver_Lumen_V11.blend').read_bytes()).hexdigest()
manifest['triangles']=sum(len(p.vertices)-2 for o in objects for p in o.data.polygons)
(HERE/'AuraWeaver_Manifest.json').write_text(json.dumps(manifest,indent=2))
bpy.ops.object.select_all(action='DESELECT');r.select_set(True)
for o in objects:o.hide_set(False);o.select_set(True)
bpy.context.view_layer.objects.active=r
bpy.ops.export_scene.fbx(filepath=str(HERE/'AuraWeaver_Rigged.fbx'),use_selection=True,object_types={'MESH','ARMATURE'},global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',use_space_transform=True,bake_space_transform=False,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,mesh_smooth_type='FACE',use_tspace=False,use_custom_props=False,add_leaf_bones=False,primary_bone_axis='Y',secondary_bone_axis='X',use_armature_deform_only=False,armature_nodetype='NULL',bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=True,bake_anim_force_startend_keying=True,bake_anim_step=1,bake_anim_simplify_factor=0,path_mode='AUTO',embed_textures=False)
s.render.resolution_x=1000;s.render.resolution_y=800;s.render.resolution_percentage=100;s.cycles.samples=16
cam=bpy.data.objects['Camera_Perspective'];s.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=.72
for pose in ['Idle','Walk','Run','Cast','AuraChannel']:
    a=bpy.data.actions['AuraWeaver_'+pose];set_action(r,a);s.frame_set(round(a.frame_range[0]+(a.frame_range[1]-a.frame_range[0])*.22))
    center=sum((r.matrix_world@r.pose.bones['Hand.'+side].head for side in ['L','R']),Vector())/2
    cam.location=center+Vector((1.3,-3,.7));cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
    s.render.filepath=str(previews/(pose+'-hands.png'));bpy.ops.render.render(write_still=True)
assert hashlib.sha256(src.read_bytes()).hexdigest()==source_hash
assert rig_signature()==rig_before and action_signature()==actions_before
(HERE/'validation.json').write_text(json.dumps({'source_sha256':source_hash,'source_unchanged':True,'skeleton_unchanged':rig_signature()==rig_before,'actions_unchanged':action_signature()==actions_before,'rig_sha256':rig_before,'actions_sha256':actions_before,'measurements':measurements,'qa':qa},indent=2))
print('ERA_LUMEN_V11_EXPORTED',manifest['triangles'])
