"""V5 corrections to the existing V4 mage: nape, level hat, flat shoes,
readable belt, rear robe and practical rear pouches. No Unity writes.
"""
import bpy,bmesh,sys,math,json,hashlib
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
HERE=Path(__file__).resolve().parent;SOURCE=HERE.parent;ERA=SOURCE.parents[2]
sys.path[:0]=[str(SOURCE),str(ERA/'ArtSource/CharacterFactory')]
from weaver_geometry import Builder,octagon,blend_z
from era_character_factory.audit import action_payload,digest
from era_character_factory.qa import mesh_report,evaluated_bounds,surface_contacts
from animate_aura_weaver import set_action
src=SOURCE/'ReferenceV4/AuraWeaver_V4.blend';source_hash=hashlib.sha256(src.read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(src));scene=bpy.context.scene;rig=bpy.data.objects['Archer_Rig'];rig.data.pose_position='REST';scene.frame_set(1)
actions={a.name:digest(action_payload(a)) for a in bpy.data.actions}
rest={b.name:[list(row) for row in b.matrix_local] for b in rig.data.bones}
optional={'AW_RunicFocus_Independent','AW_FocusHolster'}
def meshes():return [o for o in rig.children if o.type=='MESH' and o.name not in optional]
def part(o,name):
    g=o.vertex_groups.get('PART_'+name)
    return [v for v in o.data.vertices if g and any(w.group==g.index and w.weight>.5 for w in v.groups)]
def lift(z):return z+.015*max(0,min(1,(z-.30)/.66))+.012*max(0,min(1,(z-1.05)/.4))
before=evaluated_bounds(meshes());growth=1.02
out=HERE/'Previews';out.mkdir(exist_ok=True)
scene.cycles.samples=32
for name in ['Front','Back','Side']:
    cam=bpy.data.objects['Camera_'+name];cam.data.ortho_scale=2.38;cam.location.z=1.08
    cam.rotation_euler=(Vector((0,0,1.08))-cam.location).to_track_quat('-Z','Y').to_euler()
def render_view(name,file):
    scene.camera=bpy.data.objects['Camera_'+name];scene.render.filepath=str(out/file);bpy.ops.render.render(write_still=True)
if '--draft' not in sys.argv:
    for name in ['Side','Back']:
        if not (out/('Before-'+name.lower()+'.png')).exists():render_view(name,'Before-'+name.lower()+'.png')
M={m.name[3:]:m for m in bpy.data.materials if m.name.startswith('AW_')}
col=bpy.data.collections.new('V5 / targeted corrections');scene.collection.children.link(col);B=Builder(col,M,rig)
def join(name,items):
    bpy.ops.object.select_all(action='DESELECT')
    for o in items:o.select_set(True)
    bpy.context.view_layer.objects.active=items[0]
    if len(items)>1:bpy.ops.object.join()
    items[0].name=name;return items[0]
def closed_grid(name,rows,matfn,weight,thickness=.006):
    width=len(rows[0]);v=[Vector((x,y,lift(z))) for row in rows for x,y,z in row];n=len(v);f=[];fm=[]
    for k in range(len(rows)-1):
        for j in range(width-1):
            a=k*width+j;b=a+1;c=b+width;d=a+width
            f.append((a,b,c,d));fm.append(matfn(k,j))
    score=sum((v[q[1]]-v[q[0]]).cross(v[q[2]]-v[q[0]]).dot(Vector((sum(v[i].x for i in q),sum(v[i].y for i in q),0))) for q in f)
    if score<0:f=[tuple(reversed(q)) for q in f]
    normals=[Vector() for p in v];edges={}
    for q in f:
        normal=(v[q[1]]-v[q[0]]).cross(v[q[2]]-v[q[0]])
        for i in q:normals[i]+=normal
        for a,b in zip(q,q[1:]+q[:1]):edges.setdefault(tuple(sorted((a,b))),[]).append((a,b))
    inner=[p-normal.normalized()*thickness for p,normal in zip(v,normals)]
    faces=f+[tuple(i+n for i in reversed(q)) for q in f];mats=fm+['fold']*len(f)
    for values in edges.values():
        if len(values)==1:
            a,b=values[0];faces.append((a,a+n,b+n,b));mats.append('sand')
    return B.weight(B.mesh(name,v+inner,faces,'ivory',mats),weight)

# 1. The front framing stays tall. The rear is ONLY a low neck turn,
# with no separate ivory hood sack behind the exposed dark hair.
bpy.data.objects.remove(bpy.data.objects['AW_HoodAssembly'],do_unlink=True)
left=[
 [(-.097,-.122,1.718),(-.119,-.101,1.703),(-.151,-.003,1.624),(-.105,.116,1.445)],
 [(-.116,-.157,1.571),(-.147,-.143,1.539),(-.183,.028,1.470),(-.110,.135,1.426)],
 [(-.090,-.158,1.456),(-.134,-.123,1.425),(-.174,.058,1.391),(-.108,.135,1.409)],
 [(-.132,-.123,1.348),(-.157,-.100,1.345),(-.166,.054,1.350),(-.103,.130,1.390)]]
center=[(0,.142,1.445),(0,.160,1.427),(0,.161,1.407),(0,.154,1.388)]
rows=[row+[center[k]]+[(-x,y,z+.003 if j<2 else z) for j,(x,y,z) in enumerate(reversed(row))] for k,row in enumerate(left)]
def hoodweights(p):
    t=max(0,min(1,(p.z-lift(1.35))/.26));return {'Chest':1-t,'Head':t}
cowl=closed_grid('AW_HoodAssembly',rows,lambda k,j:'honey' if j in [0,7] and k<2 else 'ivory' if j not in [3,4] else 'sand',hoodweights,.007)

# Dark rear hair is tapered and layered instead of a square column.
hair=bpy.data.objects['AW_Hair']
for v in part(hair,'AW_HairScalp'):
    if v.co.z<lift(1.43):
        v.co.x*=.84
        if v.co.y>0:v.co.y*=.88
        v.co.z+=.025
    elif v.co.z<lift(1.52) and v.co.y>0:v.co.x*=.96
locks=[]
for i,(x,w,z) in enumerate([(-.078,.023,1.440),(-.046,.026,1.421),(-.015,.027,1.445),(.019,.025,1.425),(.051,.025,1.438),(.080,.020,1.449)]):
    rootz=lift(1.630+.012*(i%2));tipz=lift(z)
    verts=[(x-w,.106,rootz),(x+w,.106,rootz),(x,.127,rootz-.045),(x+w*.30,.112,tipz),(x-w*.72,.105,rootz-.056)]
    o=B.rigid(B.mesh('V5_RearHairLock_'+str(i),verts,[(0,1,2),(0,2,4),(4,2,3),(1,3,2),(0,4,3),(0,3,1)],'hairPlane' if i in [1,4] else 'hair'),'Head');locks.append(o)
join('AW_Hair',[hair]+locks)

# 2. Level the back brim and shorten/raise the continuous folded tail.
hat=bpy.data.objects['AW_Hat'];brim=part(hat,'V3_HatBrim')
assert len(brim)==48
for i,v in enumerate(brim):
    a=(i%12)*math.tau/12
    if i<12 or 24<=i<36:
        v.co.z=lift(1.718)-.028*max(0,math.cos(a))+.003*math.sin(2*a)-(.007 if i>=24 else 0)
    elif i>=36:v.co.z=lift(1.744)-.007
crown=part(hat,'V3_HatBentCrown');assert len(crown)==56
specs=[(0,.014,1.737,.148,.130),(-.010,.018,1.815,.135,.115),(-.028,.030,1.918,.085,.073),
       (.006,.040,1.978,.041,.041),(.056,.049,1.950,.035,.030),(.119,.064,1.884,.026,.022),(.178,.076,1.817,.0015,.0015)]
for i,v in enumerate(crown):
    x,y,z,rx,ry=specs[i//8];a=(i%8)*math.tau/8;v.co=(x+math.sin(a)*rx,y-math.cos(a)*ry,lift(z+.004*math.sin(a)))
crownids={v.index for v in crown}
for f in hat.data.polygons:
    if all(i in crownids for i in f.vertices):
        c=sum((hat.data.vertices[i].co for i in f.vertices),Vector())/len(f.vertices)
        if c.x>.13:f.material_index=list(hat.data.materials).index(M['charcoal'])

# 3. Grow the whole compatible character by 2%, with identical bone proportions.
# Delta scale is outside keyed Action scale channels; no leg stretching or
# edited rest matrices is needed for a four-centimetre stature increase.
rig.delta_scale*=growth

# 4. Replace ONLY the detached old foot islands, keeping shafts/legs and rig.
# Every section has a bottom at z=0; there is no arch gap or elevated heel.
shoe_data=[]
for side in ['L','R']:
    boot=bpy.data.objects['AW_Boot_'+side];oldpart=part(boot,'Boot foot '+side)
    bone=rig.data.bones['Foot.'+side];origin=bone.head_local.copy();d=bone.tail_local-origin;d.z=0;d.normalize();u=d.cross(Vector((0,0,1))).normalized()
    amin=min((v.co-origin).dot(d) for v in oldpart);amax=max((v.co-origin).dot(d) for v in oldpart)
    ulo=min((v.co-origin).dot(u) for v in oldpart);uhi=max((v.co-origin).dot(u) for v in oldpart);uc=(ulo+uhi)/2;half=(uhi-ulo)*.49
    remove={v.index for v in oldpart};bm=bmesh.new();bm.from_mesh(boot.data);bm.verts.ensure_lookup_table();bmesh.ops.delete(bm,geom=[v for v in bm.verts if v.index in remove],context='VERTS');bm.to_mesh(boot.data);bm.free()
    rings=[]
    for t,w,h in [(amin,half*.83,.078),(.015,half,.123),(amax-.040,half*.91,.063),(amax,half*.64,.045)]:
        rings.append([origin+d*t+u*(uc+across*w)+Vector((0,0,z-origin.z)) for across,z in [(-.70,0),(-1,.018),(-.80,h*.90),(0,h),(.80,h*.90),(1,.018),(.70,0)]])
    shoe=B.loft('V5_FlatShoe_'+side,rings,'brown','Foot.'+side)
    for key in ['sole','leatherEdge']:
        if M[key] not in list(shoe.data.materials):shoe.data.materials.append(M[key])
    for f in shoe.data.polygons:
        z=sum(shoe.data.vertices[i].co.z for i in f.vertices)/len(f.vertices)
        if z<.025:f.material_index=list(shoe.data.materials).index(M['sole'])
    marker=shoe.vertex_groups.new(name='PART_Boot foot '+side);marker.add(list(range(len(shoe.data.vertices))),1,'REPLACE')
    shoe_data.append({'side':side,'length_local_m':amax-amin,'width_local_m':half*2,'bottom_z':0,'construction':'four shaped cross-sections with one continuous flat underside; no heel block'})
    join('AW_Boot_'+side,[boot,shoe])

# 5. Rear robe: a surface-conforming sewn lozenge/placket and mantle diamond.
coat=bpy.data.objects['AW_OuterCoat'];tree=BVHTree.FromPolygons([v.co for v in coat.data.vertices],[list(f.vertices) for f in coat.data.polygons])
def backpoint(x,z,offset=.003):
    hit=tree.ray_cast(Vector((x,1,z)),Vector((0,-1,0)))
    if hit[0] is None:raise RuntimeError('No rear cloth support at '+str((x,z)))
    return Vector((x,hit[0].y+offset,z))
def backweights(p):
    t=max(0,min(1,(p.z-.33)/(.79-.33)));a=max(0,min(1,.5+p.x/.10))
    if p.z>.98:return {'Pelvis':1}
    return {'Coat1.Back.L':t*a,'Coat2.Back.L':(1-t)*a,'Coat1.Back.R':t*(1-a),'Coat2.Back.R':(1-t)*(1-a)}
start=len(B.meshes)
# Integrate the broad lozenge into the robe's actual facets, avoiding a flat
# card crossing its deep pleats. Only the intended pattern area is triangulated.
bm=bmesh.new();bm.from_mesh(coat.data)
pattern_faces=[f for f in bm.faces if len(f.verts)==4 and f.calc_center_median().y>0 and abs(f.calc_center_median().x)<.19 and lift(.16)<f.calc_center_median().z<lift(.78)]
bmesh.ops.triangulate(bm,faces=pattern_faces);bm.to_mesh(coat.data);bm.free()
for key in ['honey','amber']:
    if M[key] not in list(coat.data.materials):coat.data.materials.append(M[key])
for face in coat.data.polygons:
    c=sum((coat.data.vertices[i].co for i in face.vertices),Vector())/len(face.vertices)
    if c.y>0 and abs(c.x)/.15+abs(c.z-lift(.43))/.25<1:
        face.material_index=list(coat.data.materials).index(M['amber'] if c.z<lift(.43) else M['honey'])
rows=[]
for z,w in [(lift(.96),.012),(lift(.72),.012),(lift(.43),.014),(lift(.15),.017),(lift(.12),.020)]:rows.append([tuple(backpoint(-w,z,.006)),tuple(backpoint(0,z,.006)),tuple(backpoint(w,z,.006))])
o=B.folded_panel('V5_RearPlacket',rows,['honey']*4,.002,True);B.weight(o,backweights)
rear_pattern=join('AW_RearRobeInlay',B.meshes[start:])
# Small raised secular diamond on the shoulder mantle, fixed to its central chest skin.
start=len(B.meshes)
for name,w,h,mat,y in [('Border',.055,.079,'honey',.205),('Center',.035,.054,'amber',.210)]:
    z=lift(1.237);B.panel('V5_MantleDiamond'+name,[(0,y,z+h/2),(w/2,y,z),(0,y,z-h/2),(-w/2,y,z)],.004,mat,'Chest',normal=(0,-1,0))
join('AW_RearMantleAccent',B.meshes[start:])

# 6. The old belt's main ring was inside the robe depth. Build a closed belt
# outside the upper robe in front AND back, with a buckle that has an opening.
bpy.data.objects.remove(bpy.data.objects['AW_Belt'],do_unlink=True);start=len(B.meshes)
def belt_ring(w,front,back,z):
    return [(-w*.86,-front,z),(w*.86,-front,z),(w,-front*.58,z),(w,back*.60,z),(w*.86,back,z),(-w*.86,back,z),(-w,back*.60,z),(-w,-front*.58,z)]
for name,low,high,w,f,b,mat in [('Main',1.000,1.066,.159,.168,.172,'leather'),('UpperEdge',1.057,1.070,.161,.170,.175,'leatherEdge'),('LowerEdge',.993,1.007,.161,.171,.176,'brown')]:
    B.loft('V5_Belt'+name,[belt_ring(w,f,b,lift(low)),belt_ring(w,f,b,lift(high))],mat,'Pelvis')
for x in [-.113,-.060,.060,.113]:
    for rear in [False,True]:
        y=.182 if rear else -.180
        B.panel('V5_BeltKeeper',[(x-.008,y,lift(.995)),(x+.008,y,lift(.995)),(x+.008,y,lift(1.073)),(x-.008,y,lift(1.073))],.005,'bronze','Pelvis',normal=(0,-1 if rear else 1,0))
z=lift(1.034);y=-.184;w=.073;h=.050
outer=[(-w/2,y,z-h/2),(w/2,y,z-h/2),(w/2,y,z+h/2),(-w/2,y,z+h/2)]
inner=[(-w*.30,y,z-h*.28),(w*.30,y,z-h*.28),(w*.30,y,z+h*.28),(-w*.30,y,z+h*.28)]
v=outer+inner+[Vector(p)+Vector((0,.009,0)) for p in outer+inner];f=[]
for i in range(4):
    j=(i+1)%4;f.extend([(i,j,4+j,4+i),(8+i,12+i,12+j,8+j),(i,8+i,8+j,j),(4+i,4+j,12+j,12+i)])
B.rigid(B.mesh('V5_BeltBuckle',v,f,'bronze'),'Pelvis')
B.ribbon('V5_BuckleTongue',[(-.012,-.188,z),(.028,-.188,z)],.007,'bronze','Pelvis',.003)
join('AW_Belt',B.meshes[start:])

# 7. Two independent rear utility bags, with real belt-to-flap leather straps.
rear_pouches=[]
for name,x,bottom,top,w,cy in [('Short',-.110,.829,.967,.058,.225),('Long',.142,.746,.927,.062,.222)]:
    start=len(B.meshes);bottom=lift(bottom);top=lift(top)
    B.loft('V5_RearBag'+name,[octagon(w*.90,.024,.041,bottom,cx=x,cy=cy),octagon(w,.027,.047,top,cx=x,cy=cy)],'leather','Pelvis')
    y=cy+.053
    B.panel('V5_RearFlap'+name,[(x-w-.004,y,top+.006),(x+w+.004,y,top+.006),(x+w*.91,y+.004,top-.042),(x,y+.009,top-.063),(x-w*.91,y+.004,top-.042)],.007,'brown','Pelvis',normal=(0,-1,0))
    for dx in [-w*.56,w*.56]:
        attach_x=x+dx;attach_y=.172-max(0,(abs(attach_x)-.159*.86)/(.159*.14))*.0688+.004
        B.ribbon('V5_RearBeltAttachment'+name,[(attach_x,attach_y,lift(1.055)),(attach_x,.178,lift(.990)),(attach_x,y+.004,top-.030)],.017,'leatherEdge','Pelvis',.005)
    B.panel('V5_RearBagCatch'+name,[(x-.009,y+.013,top-.026),(x+.009,y+.013,top-.026),(x+.009,y+.013,top-.045),(x-.009,y+.013,top-.045)],.004,'bronze','Pelvis',normal=(0,-1,0))
    rear_pouches.append(join('AW_RearPouch_'+name,B.meshes[start:]))

# Health, rest contract and actual animation samples.
allmeshes=meshes()
for o in allmeshes:
    bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()
    for face in o.data.polygons:face.use_smooth=False
qa=mesh_report(rig,allmeshes)
assert all(m['finite'] and not m['zero_area_faces'] and not m['subdivision'] for m in qa['meshes']),str([m for m in qa['meshes'] if m['zero_area_faces']])
assert all(not w['unweighted'] and not w['non_normalized'] and w['max_influences']<=4 for w in qa['weights'])
assert actions=={a.name:digest(action_payload(a)) for a in bpy.data.actions}
assert rest=={b.name:[list(row) for row in b.matrix_local] for b in rig.data.bones}
bpy.context.view_layer.update();after=evaluated_bounds(allmeshes)
scene.camera=bpy.data.objects['Camera_Front'];bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'AuraWeaver_V5.blend'),compress=True)
if '--draft' in sys.argv:
    scene.render.resolution_percentage=65;scene.cycles.samples=16
    for name in ['Front','Side','Back']:render_view(name,'Draft-'+name.lower()+'.png')
else:
    render_view('Front','Front-Neutral.png');render_view('Side','Side.png');render_view('Back','Back.png')
samples=[];rig.data.pose_position='POSE'
if '--draft' not in sys.argv:
    pairs=[('AW_Sleeve_L','AW_OuterCoat'),('AW_Sleeve_R','AW_OuterCoat'),('AW_RearPouch_Long','AW_OuterCoat'),('AW_RearPouch_Short','AW_OuterCoat')]
    for action in bpy.data.actions:
        set_action(rig,action);low,high=action.frame_range
        for t in [0,.5,1]:
            scene.frame_set(round(low+(high-low)*t));bpy.context.view_layer.update();bounds=evaluated_bounds(allmeshes)
            assert all(math.isfinite(x) and abs(x)<8 for x in bounds['min']+bounds['max'])
            samples.append({'action':action.name,'frame':scene.frame_current,'bounds':bounds,'contacts':surface_contacts(allmeshes,pairs,max_pairs=2)})
    set_action(rig,bpy.data.actions['AuraWeaver_AuraChannel']);scene.frame_set(22);bpy.context.view_layer.update()
    with bpy.data.libraries.load(str(SOURCE/'ReferenceV4/AuraWeaver_V4_MagicDemo.blend'),link=False) as (available,wanted):
        wanted.collections=[n for n in available.collections if n.startswith('DEMO ONLY')]
    for c in wanted.collections:
        if c is not None:
            scene.collection.children.link(c)
            for o in c.all_objects:o.scale*=growth;o.location*=growth
    render_view('Front','Front-Magic.png')
    bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'AuraWeaver_V5_Magic.blend'),compress=True)
report={'source':str(src),'source_sha256':source_hash,'source_unchanged':source_hash==hashlib.sha256(src.read_bytes()).hexdigest(),
 'bone_count':len(rest),'rest_bones_unchanged':True,'original_actions_unchanged':True,'actions':list(actions),
 'before_bounds':before,'after_bounds':after,'uniform_growth':growth,'delta_scale':list(rig.delta_scale),'mesh_count':len(allmeshes),
 'triangles':sum(len(f.vertices)-2 for o in allmeshes for f in o.data.polygons),'flat_shoes':shoe_data,'qa':qa,'pose_samples':samples,
 'remaining':['Height change is unapplied uniform delta scale, not altered bone proportions; future export scale must be validated.',
 'Sampled poses are not a full animation/contact certificate.','Unity files, gameplay and release unchanged.']}
(HERE/'validation.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('V5_CORRECTION_OK',json.dumps({k:report[k] for k in ['mesh_count','triangles','source_unchanged','before_bounds','after_bounds']}))
