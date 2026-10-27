"""One surgical art pass on AuraWeaver.blend; no regeneration or Unity writes."""
import bpy,sys,math,json,hashlib
from pathlib import Path
from mathutils import Vector

HERE=Path(__file__).resolve().parent;SOURCE=HERE.parent;ERA=SOURCE.parents[2]
sys.path[:0]=[str(SOURCE),str(ERA/'ArtSource/CharacterFactory')]
from weaver_geometry import Builder,material,arm_ring
from era_character_factory.audit import action_payload,digest
from era_character_factory.qa import mesh_report

original_hash=hashlib.sha256((SOURCE/'AuraWeaver.blend').read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(SOURCE/'AuraWeaver.blend'))
scene=bpy.context.scene;rig=bpy.data.objects['Archer_Rig'];rig.data.pose_position='REST'
old_meshes=[o for o in rig.children if o.type=='MESH']
actions_before={a.name:digest(action_payload(a)) for a in bpy.data.actions}
rest_before={b.name:[list(row) for row in b.matrix_local] for b in rig.data.bones}

# Local material colors: deeper amber/ochre, still matte ivory on the main robe.
colors={'ivory':'DFD0AC','sand':'C7AC79','fold':'AB8652','light':'E9DABC',
        'honey':'CB9C4D','amber':'B67A32','ochre':'97632F','bronze':'A87C3E',
        'leather':'744C2D','leatherEdge':'946438','brown':'4B3025',
        'hair':'232422','hairPlane':'37332B','skin':'D0A07C','skinShade':'A97859'}
def recolor(mat,color):
    from era_character_factory.materials import srgb_to_linear
    rgba=[srgb_to_linear(int(color[i:i+2],16)/255) for i in (0,2,4)]+[1]
    mat.diffuse_color=rgba;node=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED');node.inputs['Base Color'].default_value=rgba
for key,color in colors.items():recolor(bpy.data.materials['AW_'+key],color)

def part(obj,name):
    group=obj.vertex_groups.get('PART_'+name)
    return [v for v in obj.data.vertices if group and any(g.group==group.index and g.weight>.5 for g in v.groups)]

# Existing brim gets a deliberate asymmetric crease; crown retains its construction.
hat=bpy.data.objects['AW_Hat']
for v in part(hat,'AW_WideHatBrim'):
    if abs(v.co.x)>.235 or abs(v.co.y)>.22:
        v.co.z+=.010 if v.co.x>0 else -.004
        if v.co.y<-.16:v.co.z-=.012
for v in part(hat,'AW_BentHatCrown'):
    if v.co.z>1.84:v.co.z+=.012*(v.co.z-1.84)/.13

# Flare only the existing cuff area, keep wrist/arm lengths and skin groups.
for side in ['L','R']:
    obj=bpy.data.objects['AW_Sleeve_'+side];bone=rig.data.bones['Forearm.'+side]
    wrist=bone.tail_local;axis=(bone.tail_local-bone.head_local).normalized()
    for v in obj.data.vertices:
        t=(v.co-wrist).dot(axis)
        if t>-.055:
            center=wrist+axis*t;radial=v.co-center;v.co=center+radial*1.045
            if abs(v.co.x)>abs(wrist.x)+.035:v.co.z-=.012
mantle=bpy.data.objects['AW_ShoulderMantle']
for v in mantle.data.vertices:
    if abs(v.co.x)>.215 and v.co.z<1.29:
        v.co.x+=math.copysign(.006,v.co.x);v.co.z-=.012

# Readable eyes, calmer mouth, slightly more asymmetric rooted fringe.
head=bpy.data.objects['AW_Head']
for side in [-1,1]:
    for v in part(head,'AW_Eye_'+str(side)):
        v.co.z=1.561+(v.co.z-1.561)*1.20+.002*max(0,min(1,(abs(v.co.x)-.025)/.04))
    for v in part(head,'AW_Brow_'+str(side)):
        v.co.z+=.0015 if abs(v.co.x)>.045 else -.001
for v in part(head,'AW_Mouth'):
    if abs(v.co.x)>.010:v.co.z-=.0018
hair=bpy.data.objects['AW_Hair']
for v in part(hair,'AW_FrontLock_1'):
    if v.co.z<1.54:v.co.x-=.006;v.co.z+=.009
for v in part(hair,'AW_FrontLock_2'):
    if v.co.z<1.55:v.co.x+=.004;v.co.y-=.003

palette={m.name[3:]:m for m in bpy.data.materials if m.name.startswith('AW_')}
palette['focusWood']=material('FocusWood','593C28',.9)
palette['focusCrystal']=material('FocusCrystal','D59B42',.42,.03)
collection=bpy.data.collections.new('POLISH / independent focus and small accents');scene.collection.children.link(collection)
B=Builder(collection,palette,rig)

# Secular geometric shoulder edging, matching the original cape's two-bone skin.
for side,s in [('L',1),('R',-1)]:
    o=B.ribbon('AW_PolishChevron_'+side,[(s*.145,-.118,1.285),(s*.198,-.121,1.245),(s*.245,-.104,1.283)],.010,'bronze','Chest',.0025)
    B.weight(o,lambda p,side=side:{'Chest':.42,'UpperArm.'+side:.58})

# Carried rear-hip focus: an actual shaft, bronze diamond cage and retained gem.
# Independent of the body; no rune/VFX behind the back and no combat integration.
a=Vector((.200,.238,.620));b=Vector((.365,.238,1.120));axis=(b-a).normalized()
u=Vector((axis.z,0,-axis.x));v=Vector((0,1,0))
def ring(c,r,n=6):return [c+u*math.cos(i*math.tau/n)*r+v*math.sin(i*math.tau/n)*r for i in range(n)]
shaft=B.loft('AW_FocusShaft',[ring(a,.010),ring(a.lerp(b,.58),.014),ring(b,.012)],'focusWood','Pelvis')
for i,t in enumerate([.08,.60,.72,.95]):
    c=a.lerp(b,t);B.loft('AW_FocusFerrule_'+str(i),[ring(c-axis*.010,.018),ring(c+axis*.010,.018)],'bronze','Pelvis')
center=b+axis*.079
outer=[center+axis*.115,center+u*.061,center-axis*.100,center-u*.061]
inner=[center+axis*.090,center+u*.042,center-axis*.080,center-u*.042]
points=outer+inner+[p+v*.014 for p in outer]+[p+v*.014 for p in inner]
faces=[]
for i in range(4):
    j=(i+1)%4;faces.extend([(i,j,4+j,4+i),(8+i,12+i,12+j,8+j),(i,8+i,8+j,j),(4+j,12+j,12+i,4+i)])
B.rigid(B.mesh('AW_FocusDiamondCage',points,faces,'bronze'),'Pelvis')
gem=[center-axis*.077,center+axis*.080,center+u*.033,center+v*.021,center-u*.033,center-v*.021]
B.rigid(B.mesh('AW_FocusAmberGem',gem,[(0,2,3),(0,3,4),(0,4,5),(0,5,2),(1,3,2),(1,4,3),(1,5,4),(1,2,5)],'focusCrystal'),'Pelvis')
for s in [-1,1]:
    c=center+u*(s*.037)
    B.loft('AW_GemRetainer_'+str(s),[ring(c-axis*.009,.008),ring(c+axis*.009,.008)],'bronze','Pelvis')

# Leather attachment connects the focus to the back of the belt, not thin air.
for i,t in enumerate([.63,.76]):
    c=a.lerp(b,t)
    B.loft('AW_FocusHolder_'+str(i),[ring(c-axis*.016,.023),ring(c+axis*.016,.023)],'leather','Pelvis')
    B.panel('AW_HolderBridge_'+str(i),[(.112,.128,1.014-i*.045),(.135,.128,1.014-i*.045),(c.x+.016,c.y-.018,c.z+.014),(c.x-.016,c.y-.018,c.z-.014)],.012,'leather','Pelvis',normal=(0,1,0))

def join_named(name,objects):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();objects[0].name=name;return objects[0]
weapon=[o for o in B.meshes if o.name.startswith(('AW_FocusShaft','AW_FocusFerrule','AW_FocusDiamond','AW_FocusAmber','AW_GemRetainer'))]
holders=[o for o in B.meshes if o.name.startswith(('AW_FocusHolder','AW_HolderBridge'))]
focus=join_named('AW_RunicFocus_Independent',weapon)
focus['attachment']='Pelvis / rear-hip leather mount; independent mesh, not fused to body'
holder=join_named('AW_FocusHolster',holders)

# Uniform presentation growth: no bone rest edits, no disproportionate leg stretch,
# no applying transforms to the keyed skeleton. Delta scale is not an Action channel.
rig.delta_scale=Vector((1.045,1.045,1.045))
meshes=[o for o in rig.children if o.type=='MESH'];qa=mesh_report(rig,meshes)
assert all(not w['unweighted'] and not w['non_normalized'] and w['max_influences']<=4 for w in qa['weights'])
assert actions_before=={a.name:digest(action_payload(a)) for a in bpy.data.actions}
assert rest_before=={b.name:[list(row) for row in b.matrix_local] for b in rig.data.bones}
points=[o.matrix_world@v.co for o in meshes for v in o.data.vertices];bpy.context.view_layer.update()
body_points=[o.matrix_world@v.co for o in old_meshes for v in o.data.vertices]
height=max(p.z for p in body_points)-min(p.z for p in body_points)
scene.frame_set(1);scene.camera=bpy.data.objects['Camera_Perspective']
report={'source_sha256':original_hash,'source_unchanged':hashlib.sha256((SOURCE/'AuraWeaver.blend').read_bytes()).hexdigest()==original_hash,
 'single_polish_pass':True,'height_m':height,'uniform_delta_scale':1.045,'rest_bones_unchanged':True,'actions_unchanged':True,
 'meshes':len(meshes),'triangles':sum(len(p.vertices)-2 for o in meshes for p in o.data.polygons),'palette_srgb_changes':colors,
 'weapon':'Separate amber/bronze diamond-cage wand in a leather rear-hip mount. No religious symbols, back rune, or new VFX.',
 'remaining':['Existing Death mid-fall cloth contacts are not addressed by this art pass.','Focus has not been validated through every combat pose.','Unity FBX, prefab, gameplay and release are intentionally unchanged.'], 'qa':qa}
(HERE/'polish-report.json').write_text(json.dumps(report,indent=2),encoding='utf8')
preview=HERE/'Previews';preview.mkdir(exist_ok=True)
for name in ['Front','Side','Back','Perspective']:
    scene.camera=bpy.data.objects['Camera_'+name];scene.render.filepath=str(preview/('AuraWeaver-'+name.lower()+'.png'));bpy.ops.render.render(write_still=True)
rig.data.pose_position='POSE';scene.frame_set(1);scene.camera=bpy.data.objects['Camera_Perspective']
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'AuraWeaver_Polished.blend'),compress=True)
print('AURA_POLISH_OK',json.dumps({k:report[k] for k in ['height_m','meshes','triangles','actions_unchanged','source_unchanged']}))
