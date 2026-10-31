"""Refine the actual v19 Earthbound Monk. Does not rebuild the workshop or edit its rig."""
import bpy, math, json, hashlib, types, ast
from pathlib import Path
from mathutils import Vector, Quaternion

ROOT=Path(__file__).resolve().parents[4]
DEST=Path(__file__).resolve().parent
SOURCE=ROOT/'ArtSource/Characters/ERA_Character_Workshop-v19.blend'
ORIGINAL_JSON=ROOT/'Assets/Era/Art/BlenderCharacters/Echo.era-mesh.json'
original_hash=hashlib.sha256(SOURCE.read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
collection=next(c for c in bpy.data.collections if 'Earthbound Monk' in c.name)
keep=set(collection.objects)
for obj in list(bpy.data.objects):
    if obj not in keep:bpy.data.objects.remove(obj,do_unlink=True)
collection.hide_viewport=False;collection.hide_render=False
origin=next(o for o in collection.objects if o.type=='EMPTY')
arm=next(o for o in collection.objects if o.type=='ARMATURE')
origin.location=(0,0,0);origin.rotation_euler=(0,0,0)
for pb in arm.pose.bones:
    pb.rotation_mode='QUATERNION';pb.rotation_quaternion=(1,0,0,0);pb.location=(0,0,0);pb.scale=(1,1,1)
arm.data.pose_position='REST'
rest={b.name:[list(row) for row in b.matrix_local] for b in arm.data.bones}
old=json.loads(ORIGINAL_JSON.read_text())
# Reuse only construction/export helpers, never the workshop's executable build loop.
script=ROOT/'ArtSource/Characters/build_characters.py'
ns={'__file__':str(script),'__name__':'echo_construction_helpers'}
exec(compile(script.read_text().split('BUILDERS=')[0],str(script),'exec'),ns)
tree=ast.parse(script.read_text());helpers=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in ('export_game','relaxed_pose')]
exec(compile(ast.Module(body=helpers,type_ignores=[]),str(script),'exec'),ns)
ns['OUT']=DEST
c=object.__new__(ns['Character']);c.key='Echo';c.label='Echo / refined Earthbound Monk';c.collection=collection;c.root=origin;c.arm=arm
c.parts=[o for o in collection.objects if o.type=='MESH']
c.rig={name:{'position':Vector(old['bindPositions'][i*3:i*3+3])} for i,name in enumerate(old['bones'])}
palette={'brown':(.31,.25,.21),'brown_light':(.48,.37,.27),'brown_dark':(.16,.13,.12),
         'leather':(.22,.14,.095),'leather_light':(.37,.25,.16),'leather_edge':(.55,.40,.25),
         'linen':(.80,.77,.67),'linen_shadow':(.58,.57,.48),'cream':(.91,.88,.75),
         'skin':(.74,.55,.40),'skin_light':(.85,.66,.49),'skin_shadow':(.48,.31,.23),
         'hair':(.16,.115,.085),'hair_light':(.25,.18,.12),'eye':(.22,.52,.51),
         'gold':(.64,.44,.20),'gold_light':(.84,.67,.36),'gold_shadow':(.39,.26,.12)}
def color_material(name,color):
    mat=ns['MATS'][name];linear=tuple(ns['srgb_channel'](v) for v in color)
    mat.diffuse_color=(*linear,1)
    if mat.use_nodes:
        shader=mat.node_tree.nodes.get('Principled BSDF')
        if shader:shader.inputs['Base Color'].default_value=(*linear,1);shader.inputs['Roughness'].default_value=.83
    return mat
for name,color in palette.items():color_material(name,color)
for obj in c.parts:
    for i,mat in enumerate(obj.data.materials):
        base=mat.name.split('.')[0]
        if base in ns['MATS']:obj.data.materials[i]=ns['MATS'][base]
    if any(g.name=='head' for g in obj.vertex_groups) and not any(g.name in ('spine','chest') for g in obj.vertex_groups):
        for v in obj.data.vertices:
            v.co.x*=.88;v.co.y*=.91;v.co.z=1.94+(v.co.z-1.94)*.89
    if obj.name.startswith('Torso /'):
        for v in obj.data.vertices:
            if v.co.z>1.60 and abs(v.co.x)>.24:v.co.z-=.045
    if obj.name.startswith('Coat /'):
        for v in obj.data.vertices:
            if v.co.z<.45:v.co.z=.355+.025*math.cos(math.atan2(v.co.y,v.co.x)*2)
    if obj.name.startswith(('Buckle /','Pouch /','Boot / sole','Cuff / leather')):
        bevel=obj.modifiers.new('Small crafted edge bevel','BEVEL');bevel.width=.005;bevel.segments=1;bevel.affect='EDGES'
        obj.modifiers.move(len(obj.modifiers)-1,0)
    # Broad readable planes; no random per-triangle paint noise.
    if obj.name.startswith(('Hair /','Face /','Ear /')):
        for face in obj.data.polygons:face.use_smooth=False

V=ns['V'];mix=ns['mix']
def torso_weight(p):
    t=max(0,min(1,(p.y-1.18)/.34));return {'spine':1-t,'chest':t}
for side in (-1,1):
    s='l' if side<0 else 'r'
    # Rounded folded cloth shoulder caps, bound to the existing upper-arm chain.
    c.tube('Shoulder / layered cloth '+s,(side*.29,1.63,0),(side*.43,1.61,0),.15,.14,'brown_light','upperarm.'+s,n=12,depth=1.05)
    c.tube('Shoulder / linen piping '+s,(side*.27,1.63,.14),(side*.39,1.49,.14),.012,.012,'linen','upperarm.'+s,8)
    c.tube('Robe / ivory seam '+s,(side*.05,1.04,.238),(side*.10,.39,.30),.010,.009,'linen_shadow','hips',8)
    c.tube('Boot / carved welt '+s,(side*.25,.052,.14),(side*.25,.052,-.17),.009,.009,'leather_edge','foot.'+s,8)
    c.jewel('Pouch / brass stud '+s,(side*.28,1.0,.13),(.013,.013,.008),'gold_light','hips')
c.skin_path('Robe / raised ivory lapel seam',[(-.19,1.635,.195),(-.09,1.49,.225),(.06,1.30,.24),(.20,1.12,.237)],[.009,.010,.010,.008],'cream',torso_weight,8)
c.skin_path('Robe / brown lapel edge',[(.24,1.60,.19),(.10,1.43,.226),(-.07,1.20,.225),(-.22,1.07,.215)],[.011,.012,.012,.009],'leather_edge',torso_weight,8)
c.panel('Sash / shaped front tab',[(-.075,1.06,.237),(.075,1.06,.237),(.065,.73,.29),(0,.68,.30),(-.065,.73,.29)],'brown_dark','hips')
c.loft('Waist / continuous under-sash',[((0,.94,0),.28,.205),((0,1.07,0),.29,.212),((0,1.15,0),.275,.20)],'brown_dark','hips',n=16)
c.ring('Neck / standing linen collar',(0,1.72,0),.105,.055,'linen','chest',.88)
c.jewel('Storm / restrained jade clasp',(.14,1.43,.25),(.038,.048,.016),'eye','chest')
c.tube('Storm / clasp mount',(.14,1.38,.24),(.14,1.48,.24),.023,.023,'gold','chest',8)
# A separate short carved wand, not a full staff. It is exported independently for the game.
start=len(c.parts)
c.tube('Wand / tapered dark wood',(0,0,0),(0,.59,0),.022,.009,'brown_dark','hand.r',10)
for y in (.035,.075,.115):c.tube('Wand / grip binding',(0,y,0),(0,y+.015,0),.028,.028,'leather_edge','hand.r',10)
c.tube('Wand / brass ferrule',(0,.48,0),(0,.52,0),.022,.018,'gold','hand.r',10)
c.jewel('Wand / storm crystal',(0,.58,0),(.034,.07,.034),'eye','hand.r')
wand_parts=c.parts[start:]
for obj in wand_parts:obj['display_only']=True;obj.hide_render=True;obj.hide_set(True)
assert rest=={b.name:[list(row) for row in b.matrix_local] for b in arm.data.bones}
arm.data.pose_position='POSE';bpy.context.view_layer.update()
result=ns['export_game'](c)
assert result['triangles']<14000
data=json.loads((DEST/'Echo.era-mesh.json').read_text())
assert data['bones']==old['bones'] and data['bindPositions']==old['bindPositions']
assert all(math.isfinite(v) for v in data['positions'])
bpy.ops.object.select_all(action='DESELECT')
for obj in [arm,*[o for o in c.parts if not o.get('display_only')]]:obj.select_set(True)
bpy.context.view_layer.objects.active=arm
bpy.ops.export_scene.fbx(filepath=str(DEST/'Echo_Refined.fbx'),use_selection=True,add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',mesh_smooth_type='FACE')
bpy.ops.wm.save_as_mainfile(filepath=str(DEST/'Echo_Refined.blend'))
# Studio turnaround, independent of other characters and source workshop.
scene=bpy.context.scene;scene.render.engine='BLENDER_EEVEE';scene.render.image_settings.file_format='PNG';scene.render.film_transparent=False
scene.render.resolution_x=1500;scene.render.resolution_y=900;scene.render.resolution_percentage=100;scene.view_settings.view_transform='Standard'
scene.world.color=(.50,.52,.54);scene.world.use_nodes=True;scene.world.node_tree.nodes.get('Background').inputs['Color'].default_value=(.80,.82,.84,1);scene.world.node_tree.nodes.get('Background').inputs['Strength'].default_value=.6
def aim(obj,target):obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()
for name,power,position,size in [('Key',450,(-3,-4,6),5),('Fill',180,(4,-2,4),4),('Rim',250,(1,4,5),3)]:
    light=bpy.data.lights.new(name,'AREA');light.energy=power;light.shape='DISK';light.size=size;obj=bpy.data.objects.new(name,light);scene.collection.objects.link(obj);obj.location=position;aim(obj,(0,0,1.2))
cam=bpy.data.objects.new('Echo review camera',bpy.data.cameras.new('Echo review camera'));scene.collection.objects.link(cam);scene.camera=cam;cam.location=(0,-10,2.9);aim(cam,(0,0,1.13));cam.data.type='ORTHO';cam.data.ortho_scale=5.0
ns['relaxed_pose'](c);deps=bpy.context.evaluated_depsgraph_get()
temp=bpy.data.collections.new('Review copies');scene.collection.children.link(temp)
for view,angle in enumerate((0,math.pi/2,math.pi)):
    for obj in c.parts:
        if obj.get('display_only'):continue
        mesh=bpy.data.meshes.new_from_object(obj.evaluated_get(deps),depsgraph=deps);copy=bpy.data.objects.new(obj.name+' / view',mesh);temp.objects.link(copy);copy.rotation_euler.z=angle;copy.location.x=(view-1)*1.55
collection.hide_render=True;scene.render.filepath=str(DEST/'Echo-turnaround.png');bpy.ops.render.render(write_still=True)
result.update({'ok':True,'baseSourceSha256':original_hash,'sourceUnchanged':original_hash==hashlib.sha256(SOURCE.read_bytes()).hexdigest(),'restSkeletonUnchanged':True,'bindPositionsUnchanged':True,'retainedRuntimeClips':'Existing KayKit legacy clips; tested separately in Unity','blendSha256':hashlib.sha256((DEST/'Echo_Refined.blend').read_bytes()).hexdigest(),'wandLengthMetres':.65})
(DEST/'validation.json').write_text(json.dumps(result,indent=2))
print('ERA_ECHO_REFINED',json.dumps(result),flush=True)
