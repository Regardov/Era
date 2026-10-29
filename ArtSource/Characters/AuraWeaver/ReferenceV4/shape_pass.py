"""V4: sculpted cowl, volumetric wrapping collar and coherent outer robe.
Opens the existing V3 asset; does not recreate its character or modify Unity.
"""
import bpy,bmesh,sys,math,json,hashlib
from pathlib import Path
from mathutils import Vector,Matrix
HERE=Path(__file__).resolve().parent;SOURCE=HERE.parent;ERA=SOURCE.parents[2]
sys.path[:0]=[str(SOURCE),str(ERA/'ArtSource/CharacterFactory')]
from weaver_geometry import Builder,arm_ring,blend_z,material
from era_character_factory.audit import action_payload,digest
from era_character_factory.qa import mesh_report,evaluated_bounds,surface_contacts
from era_character_factory.materials import srgb_to_linear
from animate_aura_weaver import set_action
src=SOURCE/'ReconstructionV3/AuraWeaver_Reconstructed.blend'
original_hash=hashlib.sha256(src.read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(src));scene=bpy.context.scene;rig=bpy.data.objects['Archer_Rig']
rig.data.pose_position='REST';scene.frame_set(1)
rest={b.name:[list(row) for row in b.matrix_local] for b in rig.data.bones}
actions={a.name:digest(action_payload(a)) for a in bpy.data.actions}
optional={'AW_RunicFocus_Independent','AW_FocusHolster'}
def model_meshes():return [o for o in rig.children if o.type=='MESH' and o.name not in optional]
before=evaluated_bounds(model_meshes())
out=HERE/'Previews';out.mkdir(exist_ok=True)

# Existing studio, consistent cameras and a uniform neutral world background.
# Hide the large floor to remove its distracting horizon from the art study.
scene.render.film_transparent=False
floor=bpy.data.objects.get('Studio ground')
if floor is not None:floor.hide_render=True
scene.cycles.samples=32;scene.render.resolution_percentage=100
for name in ['Front','Side','Back']:
    c=bpy.data.objects['Camera_'+name];c.data.ortho_scale=2.32
def render_view(name,path):
    scene.camera=bpy.data.objects['Camera_'+name];scene.render.filepath=str(out/path);bpy.ops.render.render(write_still=True)
if '--draft' not in sys.argv:
    for name in ['Front','Side','Back','Perspective']:
        if not (out/('Before-'+name.lower()+'.png')).exists():render_view(name,'Before-'+name.lower()+'.png')

M={m.name[3:]:m for m in bpy.data.materials if m.name.startswith('AW_')}
collection=bpy.data.collections.new('V4 / shaped cloth volumes');scene.collection.children.link(collection)
B=Builder(collection,M,rig);groups={}
def lift(z):return z+.015*max(0,min(1,(z-.30)/.66))+.012*max(0,min(1,(z-1.05)/.40))
def point(p):return Vector((p[0],p[1],lift(p[2])))
def weights_hood(p):
    t=max(0,min(1,(p.z-lift(1.35))/.26));return {'Chest':1-t,'Head':t}
def torso_weights(p):return blend_z(p,lift(1.05),lift(1.25),'Pelvis','Chest')
def coat_weights(p,label):
    z=p.z
    if z>=lift(.98):return {'Pelvis':1}
    if z>lift(.89):
        t=(z-lift(.89))/(lift(.98)-lift(.89));return {'Pelvis':t,'Coat1.'+label:1-t}
    return blend_z(p,.33,.79,'Coat2.'+label,'Coat1.'+label)
def remember(name,start):groups.setdefault(name,[]).extend(B.meshes[start:])

def cloth_grid(name,rows,colors,thickness=.006,cyclic=False,weights=None,coords_lift=True,creases=()):
    """Explicit cloth grid, closed thickness along averaged surface normals.
    No remeshing: only chosen crease quads are triangulated.
    """
    width=len(rows[0]);count=len(rows)*width
    v=[point(p) if coords_lift else Vector(p) for row in rows for p in row]
    faces=[];mats=[]
    for k in range(len(rows)-1):
        for j in range(width if cyclic else width-1):
            a=k*width+j;b=k*width+(j+1)%width;c=b+width;d=a+width
            color=colors(k,j) if callable(colors) else colors[min(k,len(colors)-1)]
            if (k,j) in creases:faces.extend([(a,b,c),(a,c,d)]);mats.extend([color,color])
            else:faces.append((a,b,c,d));mats.append(color)
    # Front/side/back panels have different column directions. Orient the
    # whole patch outward before constructing its thickness; otherwise the
    # dark lining would become the visible side of half the robe.
    score=0.0
    for face in faces:
        center=sum((v[i] for i in face),Vector())/len(face)
        normal=(v[face[1]]-v[face[0]]).cross(v[face[2]]-v[face[0]])
        score+=normal.dot(Vector((center.x,center.y,0)))
    if score<0:faces=[tuple(reversed(f)) for f in faces]
    normals=[Vector() for p in v]
    for face in faces:
        n=(v[face[1]]-v[face[0]]).cross(v[face[2]]-v[face[0]])
        for i in face:normals[i]+=n
    inner=[p-(n.normalized() if n.length>1e-10 else Vector((0,1,0)))*thickness for p,n in zip(v,normals)]
    allfaces=list(faces)+[tuple(i+count for i in reversed(f)) for f in faces];allmats=mats+['fold' if m in ['ivory','light','sand'] else m for m in mats]
    # Boundary counts work for both an open strip and a closed circumferential band.
    edges={}
    for face in faces:
        for a,b in zip(face,face[1:]+face[:1]):
            key=tuple(sorted((a,b)));edges.setdefault(key,[]).append((a,b))
    for pairs in edges.values():
        if len(pairs)==1:
            a,b=pairs[0];allfaces.append((a,a+count,b+count,b));allmats.append('fold')
    o=B.mesh(name,v+inner,allfaces,'ivory',allmats)
    return B.weight(o,weights or (lambda p:{'Chest':1}))

for n in ['AW_HoodAssembly','AW_Scarf','AW_ShoulderMantle','AW_OuterCoat']:
    bpy.data.objects.remove(bpy.data.objects[n],do_unlink=True)

# One U-shaped drapery around the face, sides and nape. Its top edge dips
# behind the skull, leaving the reference's dark hair exposed under the hat.
# Different row depths make actual returns and overhangs, not hanging boards.
start=len(B.meshes)
left=[
 [(-.097,-.122,1.718),(-.119,-.101,1.703),(-.155,-.003,1.624),(-.127,.105,1.536)],
 [(-.116,-.157,1.571),(-.151,-.143,1.539),(-.204,.033,1.470),(-.157,.145,1.445)],
 [(-.090,-.158,1.456),(-.137,-.123,1.425),(-.183,.067,1.391),(-.131,.151,1.385)],
 [(-.132,-.123,1.348),(-.164,-.100,1.345),(-.173,.060,1.350),(-.104,.130,1.322)]]
centers=[(0,.145,1.495),(.009,.190,1.405),(-.010,.182,1.343),(0,.160,1.283)]
rows=[]
for k,row in enumerate(left):
    right=[(-x,y+(0.004 if k==1 else 0),z+(.008 if k in [1,2] else 0)) for x,y,z in reversed(row)]
    rows.append(row+[centers[k]]+right)
def hood_colors(k,j):
    if j in [0,7]:return 'honey' if k in [0,1] else 'sand'
    if (k,j) in [(0,2),(1,3),(1,5),(2,2)]:return 'sand'
    return 'ivory'
cloth_grid('V4_DrapedCowl',rows,hood_colors,.007,weights=weights_hood,creases=[(0,1),(0,6),(1,2),(1,5),(2,3)])
remember('AW_HoodAssembly',start)

# Actual circumferential collar beneath two curled, crossing cloth wraps.
start=len(B.meshes);n=12
base=[]
for k in range(3):
    row=[]
    for i in range(n):
        a=i*math.tau/n;front=max(0,math.cos(a));x=math.sin(a)*[.080,.137,.123][k]
        y=-math.cos(a)*[.080,.151,.124][k]
        z=[1.438,1.365,1.313][k]-front*[.009,.020,.058][k]+.008*math.sin(a)
        row.append((x,y,z))
    base.append(row)
cloth_grid('V4_InnerCollar',base,['charcoal','inner'],.007,cyclic=True)
angles=[-1.85,-1.25,-.65,0,.65,1.25,1.85]
for which,center,width,mat,twist in [('Upper',1.378,.067,'brown',.032),('Lower',1.299,.071,'leather',-.021)]:
    rows=[]
    for k in range(3):
        row=[]
        for a in angles:
            front=max(0,math.cos(a));radius=.143+(.010 if k==1 else 0)
            z=center+twist*math.sin(a)-front*.033+(1-k)*width/2
            row.append((math.sin(a)*radius,-math.cos(a)*(.162+(.013 if k==1 else 0)),z))
        rows.append(row)
    cloth_grid('V4_WrappedCollar_'+which,rows,[mat,mat],.008,creases=[(0,1),(1,4)])
# A diagonal front fold closes the empty upper sternum without a flat full bib.
cloth_grid('V4_ChestFold',[
 [(-.112,-.132,1.287),(-.018,-.178,1.251),(.102,-.139,1.303)],
 [(-.104,-.146,1.238),(.012,-.164,1.178),(.084,-.148,1.250)]],['brown'],.006)
remember('AW_Scarf',start)

# One capelet around the shoulder arc; folded front revers are continuous
# through the upper torso instead of isolated little shoulder plaques.
start=len(B.meshes)
cape_rows=[
 [(-.108,-.116,1.365),(-.171,-.088,1.387),(-.222,.012,1.366),(-.157,.125,1.387),(0,.166,1.341),(.157,.125,1.383),(.222,.012,1.365),(.171,-.088,1.388),(.108,-.116,1.365)],
 [(-.121,-.143,1.286),(-.221,-.126,1.308),(-.269,.030,1.287),(-.214,.142,1.287),(0,.196,1.241),(.213,.146,1.293),(.270,.030,1.288),(.221,-.129,1.309),(.123,-.146,1.287)],
 [(-.136,-.142,1.252),(-.238,-.142,1.212),(-.290,.043,1.180),(-.219,.143,1.188),(0,.194,1.116),(.219,.147,1.198),(.284,.042,1.184),(.238,-.145,1.218),(.137,-.143,1.256)],
 [(-.137,-.144,1.228),(-.242,-.143,1.185),(-.293,.045,1.157),(-.221,.145,1.165),(0,.196,1.093),(.222,.149,1.174),(.287,.043,1.161),(.242,-.147,1.191),(.138,-.145,1.232)]]
def shoulder_weights(p):
    a=max(0,min(1,(abs(p.x)-.130)/.135))*.82
    return {'Chest':1-a,'UpperArm.'+('L' if p.x>0 else 'R'):a}
cloth_grid('V4_ShoulderCape',cape_rows,lambda k,j:'amber' if k==2 else 'sand' if k==1 and j in [0,2,5,7] else 'ivory',.009,weights=shoulder_weights,creases=[(0,0),(0,3),(0,4),(0,7),(1,1),(1,6)])
for side,s in [('L',1),('R',-1)]:
    rows=[[(s*.097,-.141,1.348),(s*.137,-.122,1.363),(s*.171,-.072,1.337)],
          [(s*.108,-.158,1.227),(s*.143,-.132,1.242),(s*.160,-.079,1.238)],
          [(s*.087,-.145,1.111),(s*.116,-.128,1.119),(s*.145,-.065,1.120)],
          [(s*.092,-.136,1.039),(s*.117,-.125,1.040),(s*.135,-.063,1.040)]]
    cloth_grid('V4_TorsoRever_'+side,rows,lambda k,j:'honey' if j==0 and k==0 else 'ivory' if j==0 else 'sand',.007,weights=torso_weights,creases=[(0,0),(1,1)])
    # Retain one functional shoulder strap/clasp; no ornamental clutter.
    path=[(s*.108,-.149,lift(1.343)),(s*.180,-.151,lift(1.329)),(s*.250,-.111,lift(1.309))]
    o=B.ribbon('V4_ShoulderRetainer_'+side,path,.022,'leather','Chest',.004);B.weight(o,shoulder_weights)
remember('AW_ShoulderMantle',start)

# Broad robe panels, five columns per panel describe two real cloth folds.
# The hems and ridges are staggered, so they no longer read as flat striped rails.
start=len(B.meshes)
for label in ['Front.L','Front.R','Back.L','Back.R','Side.L','Side.R']:
    s=1 if label.endswith('L') else -1;rear=label.startswith('Back');side=label.startswith('Side')
    rows=[]
    for k,(z,w,d) in enumerate([(1.024,.128,.132),(.82,.163,.160),(.56,.206,.183),(.32,.246,.201),(.222,.266,.211),(.146,.276,.214),(.115,.279,.213)]):
        if side:
            row=[(s*w,-d*.94,z+.010),(s*(w+.014),-d*.43,z+.018),(s*(w+.026),.005,z+.027),(s*(w+.012),d*.45,z+.012),(s*w,d*.94,z+.009)]
        else:
            inner=(.024+.044*(1-z)) if not rear else -.007
            xs=[inner,inner+(w-inner)*.25,inner+(w-inner)*.53,inner+(w-inner)*.78,w]
            heights=[0,.008,.023,-.003,.015]
            if not rear and s==-1:heights=[a+.008*(1-z) for a in heights]
            # Positive rear / negative front ridge variation is geometrical.
            depths=[d,d+.022,d-.008,d+.017,d*.91]
            row=[(s*x,(1 if rear else -1)*y,z+dz) for x,y,dz in zip(xs,depths,heights)]
        rows.append(row)
    def coat_color(k,j,rear=rear,side=side):
        if k>=5:return 'brown'
        if k==4:return 'amber'
        if k==3:return 'honey' if j in [0,1] else 'sand'
        if j in [1,3]:return 'sand' if k==2 else 'ivory'
        return 'ivory' if j!=2 else 'light'
    cloth_grid('V4_Coat_'+label,rows,coat_color,.008,weights=lambda p,label=label:coat_weights(p,label),creases=[(1,0),(2,3)])
remember('AW_OuterCoat',start)

# Preserve successful existing parts, adjust supporting shapes locally.
def part(o,name):
    g=o.vertex_groups.get('PART_'+name)
    return [v for v in o.data.vertices if g and any(x.group==g.index and x.weight>.5 for x in v.groups)]
bodice=bpy.data.objects['AW_Bodice']
for v in bodice.data.vertices:
    if lift(1.07)<v.co.z<lift(1.29) and v.co.y<-.045:
        v.co.y-=.012*max(0,1-abs(v.co.x)/.17)
hat=bpy.data.objects['AW_Hat']
brim=part(hat,'V3_HatBrim')
for i,v in enumerate(brim):
    if 12<=i<24 or 36<=i<48:
        # Overlap the 12-sided brim opening with the 8-sided crown base;
        # mismatched inscribed radii previously left dark triangular cracks.
        v.co.x*=.88;v.co.y=.014+(v.co.y-.014)*.88
    if v.co.y<-.19:v.co.z-=.015;v.co.y-=.007
    if v.co.x<-.20:v.co.z+=.012
crown=part(hat,'V3_HatBentCrown')
specs=[(0,.014,1.737,.148,.130),(-.010,.018,1.815,.135,.115),(-.028,.030,1.918,.085,.073),
       (.006,.040,1.978,.041,.041),(.063,.050,1.949,.038,.030),(.134,.074,1.855,.028,.022),(.208,.092,1.776,.001,.001)]
assert len(crown)==56
for i,v in enumerate(crown):
    x,y,z,rx,ry=specs[i//8];a=(i%8)*math.tau/8
    v.co=point((x+math.sin(a)*rx,y-math.cos(a)*ry,z+.006*math.sin(a)))
indexset={v.index for v in crown}
for key in ['sand','fold','charcoal']:
    if M[key] not in list(hat.data.materials):hat.data.materials.append(M[key])
for face in hat.data.polygons:
    if all(i in indexset for i in face.vertices):
        center=sum((hat.data.vertices[i].co for i in face.vertices),Vector())/len(face.vertices)
        if center.x>.14:
            key='sand' if center.y<.080 else 'charcoal';face.material_index=list(hat.data.materials).index(M[key])
# Match the underside diagonal to the upper brim triangles. A nonplanar
# underside quad can otherwise poke through a thin bent brim as a dark wedge.
bm=bmesh.new();bm.from_mesh(hat.data);bm.verts.ensure_lookup_table()
ids=[v.index for v in brim]
for i in range(12):
    j=(i+1)%12;a,b,c,d=[ids[t] for t in [24+j,24+i,36+i,36+j]]
    old=next((f for f in bm.faces if len(f.verts)==4 and {v.index for v in f.verts}=={a,b,c,d}),None)
    if old is not None:
        mat=old.material_index;bm.faces.remove(old)
        for tri in [(a,b,d),(b,c,d)]:
            f=bm.faces.new([bm.verts[t] for t in tri]);f.material_index=mat
bm.to_mesh(hat.data);bm.free()
for side,s in [('L',1),('R',-1)]:
    sleeve=bpy.data.objects['AW_Sleeve_'+side]
    # Existing sleeve topology stays skinned. Pull the outer hanging corners
    # into a fabric point, while keeping the inner wrist opening shorter.
    wrist=rig.data.bones['Forearm.'+side].tail_local.copy();wrist.z+=.016
    for v in sleeve.data.vertices:
        outer=s*v.co.x-s*wrist.x
        if v.co.z<1.02:
            if outer>.055:v.co.z-=.019;v.co.x+=s*.009
            elif outer<-.045:v.co.x+=s*.012
    pouch=bpy.data.objects['AW_PouchAssembly_'+side]
    for v in pouch.data.vertices:
        if v.co.z>1.015:v.co.x*=.77;v.co.y-=.010
    # Fingers and soles keep their original shared topology and weights.
head=bpy.data.objects['AW_Head']
for v in part(head,'AW_Mouth'):v.co.z=lift(1.493)
hair=bpy.data.objects['AW_Hair']
for v in part(hair,'AW_FrontLock_2'):
    if v.co.z<1.60:v.co.x-=.008;v.co.z-=.013
for v in part(hair,'AW_FrontLock_0'):
    if v.co.z<1.51:v.co.x-=.003

colors={'ivory':'DEC99F','light':'EAD8B3','sand':'C5A36D','fold':'9D7D55','honey':'BF8D40','amber':'B4752D',
        'ochre':'8C5B2D','brown':'403027','leather':'6E4A30','charcoal':'222628','inner':'302E29','skin':'CB9672','skinShade':'A76E52'}
for key,col in colors.items():
    m=M[key];rgba=[srgb_to_linear(int(col[i:i+2],16)/255) for i in (0,2,4)]+[1]
    m.diffuse_color=rgba;next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED').inputs['Base Color'].default_value=rgba

# Coherent objects, no global weld. All rebuilt grids have their own closed
# fabric thickness; material seams are not disconnected surface strips.
for name,objects in groups.items():
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:
        g=o.vertex_groups.new(name='PART_'+o.name);g.add(list(range(len(o.data.vertices))),1,'REPLACE');o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    if len(objects)>1:bpy.ops.object.join()
    objects[0].name=name
meshes=model_meshes()
for o in meshes:
    bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()
    for f in o.data.polygons:f.use_smooth=False
qa=mesh_report(rig,meshes)
assert all(m['finite'] and not m['zero_area_faces'] and not m['subdivision'] for m in qa['meshes']),str([m for m in qa['meshes'] if m['zero_area_faces']])
assert all(not w['unweighted'] and not w['non_normalized'] and w['max_influences']<=4 for w in qa['weights'])
assert actions=={a.name:digest(action_payload(a)) for a in bpy.data.actions}
assert rest=={b.name:[list(row) for row in b.matrix_local] for b in rig.data.bones}
bpy.context.view_layer.update();after=evaluated_bounds(meshes)
scene.camera=bpy.data.objects['Camera_Perspective'];bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'AuraWeaver_V4.blend'),compress=True)
if '--draft' in sys.argv:
    scene.render.resolution_percentage=60;scene.cycles.samples=16
    for name in ['Front','Side','Back','Perspective']:render_view(name,'Draft-'+name.lower()+'.png')
else:
    for name in ['Front','Side','Back','Perspective']:render_view(name,'AuraWeaver-'+name.lower()+'.png')

# Evaluated animation samples. No existing Action is rewritten.
rig.data.pose_position='POSE';samples=[]
pairs=[('AW_Sleeve_L','AW_Bodice'),('AW_Sleeve_R','AW_Bodice'),('AW_Sleeve_L','AW_OuterCoat'),('AW_Sleeve_R','AW_OuterCoat')]
if '--draft' not in sys.argv:
    for action in bpy.data.actions:
        set_action(rig,action);first,last=action.frame_range
        for t in [0,.5,1]:
            scene.frame_set(round(first+(last-first)*t));bpy.context.view_layer.update();bounds=evaluated_bounds(meshes)
            assert all(math.isfinite(v) and abs(v)<8 for v in bounds['min']+bounds['max'])
            samples.append({'action':action.name,'frame':scene.frame_current,'bounds':bounds,'contacts':surface_contacts(meshes,pairs,max_pairs=2)})
        if action.name in ['AuraWeaver_Run','AuraWeaver_Cast']:
            scene.frame_set(round(first+(last-first)*.33));render_view('Perspective','Pose-'+action.name+'.png')

report={'source':str(src),'source_sha256':original_hash,'source_unchanged':original_hash==hashlib.sha256(src.read_bytes()).hexdigest(),
 'rest_bones_unchanged':True,'original_actions_unchanged':True,'bones':len(rest),'actions':list(actions),
 'before_bounds':before,'after_bounds':after,'mesh_count':len(meshes),'triangles':sum(len(f.vertices)-2 for o in meshes for f in o.data.polygons),
 'qa':qa,'samples':samples,'colors_srgb':colors,'rebuilt':['AW_HoodAssembly','AW_Scarf','AW_ShoulderMantle','AW_OuterCoat'],
 'remaining':['Illustration is not an orthographic blueprint.','Motion samples do not certify complete gameplay deformation.','Unity, aura systems and Windows release unchanged.']}
(HERE/'validation.json').write_text(json.dumps(report,indent=2),encoding='utf8')

# Separate concept demonstration using the existing two-hand channel pose.
if '--draft' not in sys.argv:
    set_action(rig,bpy.data.actions['AuraWeaver_AuraChannel']);scene.frame_set(22);bpy.context.view_layer.update()
    with bpy.data.libraries.load(str(SOURCE/'ReconstructionV3/AuraWeaver_MagicDemo.blend'),link=False) as (available,wanted):
        wanted.collections=[n for n in available.collections if n.startswith('DEMO ONLY')]
    for c in wanted.collections:
        if c is not None:scene.collection.children.link(c)
    render_view('Perspective','AuraWeaver-MagicDemo.png')
    bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'AuraWeaver_V4_MagicDemo.blend'),compress=True)
print('V4_SHAPE_PASS_OK',json.dumps({k:report[k] for k in ['mesh_count','triangles','source_unchanged','rest_bones_unchanged','original_actions_unchanged']}))
