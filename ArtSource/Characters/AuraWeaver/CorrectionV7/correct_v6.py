"""Surgical V7 pass on V6: connected curled gloves, shaped shoes,
rear chevron cape and body-supported pouches. No Unity writes.
"""
import bpy,bmesh,sys,math,json,hashlib
from pathlib import Path
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree
HERE=Path(__file__).resolve().parent;SOURCE=HERE.parent;ERA=SOURCE.parents[2]
sys.path[:0]=[str(SOURCE),str(ERA/'ArtSource/CharacterFactory')]
from weaver_geometry import Builder
from era_character_factory.audit import action_payload,digest,islands
from era_character_factory.qa import mesh_report,evaluated_bounds,surface_contacts
from animate_aura_weaver import set_action
src=SOURCE/'ReferenceV6/AuraWeaver_V6.blend';source_hash=hashlib.sha256(src.read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(src));scene=bpy.context.scene;rig=bpy.data.objects['Archer_Rig'];rig.data.pose_position='REST';scene.frame_set(1)
actions={a.name:digest(action_payload(a)) for a in bpy.data.actions}
rest={b.name:[list(row) for row in b.matrix_local] for b in rig.data.bones}
optional={'AW_RunicFocus_Independent','AW_FocusHolster'}
def meshes():return [o for o in rig.children if o.type=='MESH' and o.name not in optional]
M={m.name[3:]:m for m in bpy.data.materials if m.name.startswith('AW_')}
col=bpy.data.collections.new('V7 / targeted detail corrections');scene.collection.children.link(col);B=Builder(col,M,rig)
out=HERE/'Previews';out.mkdir(exist_ok=True)
before=evaluated_bounds(meshes())
def part(o,name):
    g=o.vertex_groups.get('PART_'+name)
    return [v for v in o.data.vertices if g and any(w.group==g.index and w.weight>.5 for w in v.groups)]
def join(name,items):
    bpy.ops.object.select_all(action='DESELECT')
    for o in items:o.select_set(True)
    bpy.context.view_layer.objects.active=items[0]
    if len(items)>1:bpy.ops.object.join()
    items[0].name=name;return items[0]
def tree(o):return BVHTree.FromPolygons([v.co for v in o.data.vertices],[list(f.vertices) for f in o.data.polygons])
def matid(o,key):
    if M[key] not in list(o.data.materials):o.data.materials.append(M[key])
    return list(o.data.materials).index(M[key])
def detail_camera(name,target,direction,scale):
    o=bpy.data.objects.get(name)
    if not o:o=bpy.data.objects.new(name,bpy.data.cameras.new(name));scene.collection.objects.link(o)
    o.location=Vector(target)+Vector(direction).normalized()*3
    o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
    o.data.type='ORTHO';o.data.ortho_scale=scale
    return o

# 1. Retain the entire connected palm/finger topology and its original weights.
# Reposition its rings into a relaxed curled glove instead of four thin rods.
hand_report=[]
for side in ['L','R']:
    o=bpy.data.objects['AW_ConnectedGlove_'+side];assert len(o.data.vertices)==116
    old=[v.co.copy() for v in o.data.vertices]
    wrist=sum((old[i] for i in [0,9,30,39]),Vector())/4
    root=sum((old[i] for i in [20,29,50,59]),Vector())/4
    d=(root-wrist).normalized();u=((old[29]+old[59])-(old[20]+old[50])).normalized();u=(u-d*u.dot(d)).normalized();n=d.cross(u).normalized()
    if n.dot(old[20]-old[50])<0:n=-n
    # Knuckle mass is formed by the same palm vertices, no disconnected palm cap.
    for i in range(30):
        mid=(old[i]+old[i+30])*.5;row=i//10
        mid+=u*(mid-root).dot(u)*.06
        mid-=d*(mid-wrist).dot(d)*(.23 if row else 0)
        edge=min(1,abs((mid-root).dot(u))/.034)
        half=[.009,.013,.012][row]*(1-.32*edge)
        o.data.vertices[i].co=mid+n*half
        o.data.vertices[i+30].co=mid-n*[.008,.010,.009][row]
    roots=[(1,2,'Index'),(3,4,'Middle'),(5,6,'Ring'),(7,8,'Little')]
    for finger,(a,b,name) in enumerate(roots):
        indices=[20+a,20+b,50+b,50+a]
        base=sum((o.data.vertices[i].co for i in indices),Vector())/4
        oldbase=sum((old[i] for i in indices),Vector())/4
        offset=60+finger*12;previous=0;center=base.copy()
        for k,degrees in enumerate([38,80,105]):
            start=offset+k*4;oldcenter=sum(old[start:start+4],Vector())/4
            distance=(oldcenter-oldbase).dot(d);length=distance-previous;previous=distance
            angle=math.radians(degrees);tangent=d*math.cos(angle)-n*math.sin(angle)
            normal=n*math.cos(angle)+d*math.sin(angle);center+=tangent*length
            width=abs((old[start+1]-old[start]).dot(u))*.5*[1.19,1.15,1.09][k]
            depth=(old[start]-old[start+3]).length*.5*1.23
            for j,(su,sn) in enumerate([(-1,1),(1,1),(1,-1),(-1,-1)]):o.data.vertices[start+j].co=center+u*su*width+normal*sn*depth
    oldbase=sum((old[i] for i in [10,20,50,40]),Vector())/4
    base=sum((o.data.vertices[i].co for i in [10,20,50,40]),Vector())/4
    for k in range(2):
        start=108+k*4;oldcenter=sum(old[start:start+4],Vector())/4
        center=base+(oldcenter-oldbase)*[.94,.83][k]-n*[.004,.010][k]+u*(.004*k)
        for j in range(4):o.data.vertices[start+j].co=center+(old[start+j]-oldcenter)*1.20
    leather=matid(o,'leather');charcoal=matid(o,'charcoal');dark=matid(o,'sole')
    o.data.update()
    for f in o.data.polygons:
        center=sum((o.data.vertices[i].co for i in f.vertices),Vector())/len(f.vertices)
        if max(f.vertices)<60 and f.normal.dot(n)>.45 and .018<(center-wrist).dot(d)<(root-wrist).length+.008:f.material_index=leather
        else:f.material_index=charcoal if max(f.vertices)<108 else dark
    hand_report.append({'side':side,'vertices':len(o.data.vertices),'connected_islands':len(islands(o.data)),'finger_ring_angles_degrees':[38,80,105],'topology_and_weights_preserved':True})

# 2. Replace only the old wedge foot islands, preserving leg/shaft geometry.
# Six shaped axial sections, a continuous sole, broad toe and two instep straps.
shoe_report=[]
for side in ['L','R']:
    boot=bpy.data.objects['AW_Boot_'+side];oldpart=part(boot,'Boot foot '+side);assert len(oldpart)==28
    bone=rig.data.bones['Foot.'+side];origin=bone.head_local.copy();origin.x*=.95
    d=bone.tail_local-bone.head_local;d.x*=.95;d.z=0;d.normalize();u=d.cross(Vector((0,0,1))).normalized()
    amin=min((v.co-origin).dot(d) for v in oldpart);amax=max((v.co-origin).dot(d) for v in oldpart)
    lo=min((v.co-origin).dot(u) for v in oldpart);hi=max((v.co-origin).dot(u) for v in oldpart);uc=(lo+hi)/2;half=(hi-lo)*.52
    remove={v.index for v in oldpart};bm=bmesh.new();bm.from_mesh(boot.data);bm.verts.ensure_lookup_table();bmesh.ops.delete(bm,geom=[v for v in bm.verts if v.index in remove],context='VERTS');bm.to_mesh(boot.data);bm.free()
    start=len(B.meshes)
    specs=[(amin,half*.80,.096),(amin+.027,half*.88,.142),(.026,half,.127),(.075,half*.99,.079),(amax-.029,half*.91,.058),(amax,half*.71,.046)]
    rings=[]
    for t,w,h in specs:
        rings.append([origin+d*t+u*(uc+across*w)+Vector((0,0,z-origin.z)) for across,z in [(-.73,0),(-1,.012),(-1,.024),(-.81,h*.88),(0,h),(.81,h*.88),(1,.024),(1,.012),(.73,0)]])
    foot=B.loft('V7_ShapedFoot_'+side,rings,'brown','Foot.'+side)
    for f in foot.data.polygons:
        c=sum((foot.data.vertices[i].co for i in f.vertices),Vector())/len(f.vertices)
        if c.z<.025:f.material_index=matid(foot,'sole')
        elif (c-origin).dot(d)>.070:f.material_index=matid(foot,'leather')
    surface=tree(foot)
    for t,width,key in [(.045,.019,'leatherEdge'),(.111,.016,'brown')]:
        rows=[]
        for tt in [t-width/2,t+width/2]:
            row=[]
            for across in [-.82,-.48,0,.48,.82]:
                pt=origin+d*tt+u*(uc+across*half);hit=surface.ray_cast(Vector((pt.x,pt.y,.4)),Vector((0,0,-1)))[0]
                assert hit is not None
                row.append(hit+Vector((0,0,.0025)))
            rows.append(row)
        strap=B.folded_panel('V7_InstepStrap_'+side,rows,[key],.002,normal=(0,0,1));B.rigid(strap,'Foot.'+side)
    for obj in B.meshes[start:]:
        g=obj.vertex_groups.get('PART_Boot foot '+side) or obj.vertex_groups.new(name='PART_Boot foot '+side);g.add(list(range(len(obj.data.vertices))),1,'REPLACE')
    join('AW_Boot_'+side,[boot]+B.meshes[start:])
    shoe_report.append({'side':side,'axial_sections':len(specs),'continuous_floor_z':0,'instep_straps':2,'separate_old_shafts_preserved':True})

# Expose the ankle/instep shown in the reference; the old long hem swallowed
# almost the entire shaped upper shoe. Fade this small lift out above the hem.
for name in ['AW_OuterCoat','AW_InnerCoat','AW_RearRobeInlay']:
    for v in bpy.data.objects[name].data.vertices:
        if v.co.z<.30:v.co.z+=.045*max(0,min(1,(.30-v.co.z)/.16))

# 3. Surface-following rear mantle: a deeper V, folded broad planes and
# two distinct ochre/amber edging bands, laid over the existing capelet.
mantle=bpy.data.objects['AW_ShoulderMantle'];cape=part(mantle,'V4_ShoulderCape');assert len(cape)==72
base=[[cape[k*9+j].co.copy() for j in [3,4,5]] for k in range(4)]
def expand(row):return [row[0],row[0].lerp(row[1],.5),row[1],row[1].lerp(row[2],.5),row[2]]
top=expand(base[0]);upper=expand(base[1]);bottom=expand(base[3])
for j,p in enumerate(bottom):p.z-=[0,.026,.045,.026,0][j]
rows=[top,upper,[p+Vector((0,0,.069)) for p in bottom],[p+Vector((0,0,.032)) for p in bottom],bottom]
for k,row in enumerate(rows):
    for j,p in enumerate(row):p.y+=.008+(.004 if j in [1,3] else 0)
def shoulder_weight(p):
    a=max(0,min(1,(abs(p.x)-.12)/.12))*.70
    return {'Chest':1-a,'UpperArm.'+('L' if p.x>0 else 'R'):a}
patch=B.folded_panel('V7_RearChevronMantle',rows,['ivory','ivory','honey','amber'],.006,back=True)
B.weight(patch,shoulder_weight)
for f in patch.data.polygons:
    c=sum((patch.data.vertices[i].co for i in f.vertices),Vector())/len(f.vertices)
    if f.material_index==list(patch.data.materials).index(M['ivory']) and abs(c.x)<.075:f.material_index=matid(patch,'light')
    # Broad shoulder corner fields follow the actual cape triangles.
    if f.index in [2,12]:f.material_index=matid(patch,'leather')
    elif f.index in [16,30]:f.material_index=matid(patch,'honey')
patch_tree=tree(patch)
bpy.data.objects.remove(bpy.data.objects['AW_RearMantleAccent'],do_unlink=True)
start=len(B.meshes)
for name,w,h,key,depth in [('Border',.053,.089,'honey',.003),('Inset',.033,.058,'amber',.005)]:
    z=1.453;outline=[]
    for x,zz in [(0,z+h/2),(w/2,z),(0,z-h/2),(-w/2,z)]:
        hit=patch_tree.ray_cast(Vector((x,1,zz)),Vector((0,-1,0)))[0];assert hit is not None
        outline.append((x,hit.y+depth,zz))
    B.rigid(B.panel('V7_MantleDiamond'+name,outline,.002,key,normal=(0,-1,0)),'Chest')
join('AW_RearMantleAccent',B.meshes[start:])
join('AW_ShoulderMantle',[mantle,patch])

# 4. Seat each rear bag on the robe, replacing floating bridge straps.
coat=bpy.data.objects['AW_OuterCoat'];coat_tree=tree(coat);belt_tree=tree(bpy.data.objects['AW_Belt'])
def support(x,z):
    hit=coat_tree.ray_cast(Vector((x,1,z)),Vector((0,-1,0)))[0]
    if hit is None:raise RuntimeError('No robe support at '+str((x,z)))
    return hit.y
def body_plane(samples):
    n=len(samples);xx=sum(x*x for x,z,y in samples);xz=sum(x*z for x,z,y in samples);zz=sum(z*z for x,z,y in samples)
    sx=sum(x for x,z,y in samples);sz=sum(z for x,z,y in samples)
    A=Matrix(((xx,xz,sx),(xz,zz,sz),(sx,sz,n)));rhs=Vector((sum(x*y for x,z,y in samples),sum(z*y for x,z,y in samples),sum(y for x,z,y in samples)))
    return A.inverted()@rhs
bag_report=[]
for name in ['AW_RearPouch_Short','AW_RearPouch_Long']:
    o=bpy.data.objects[name];assert len(o.data.vertices)==58
    original_cx=sum(o.data.vertices[i].co.x for i in range(16))/16
    desired_cx=.092 if name.endswith('Long') else -.080
    for v in o.data.vertices:v.co.x+=desired_cx-original_cx
    body=[v.co.copy() for v in list(o.data.vertices)[:16]]
    xmin=min(p.x for p in body);xmax=max(p.x for p in body);zmin=min(p.z for p in body);zmax=max(p.z for p in body);oldy=min(p.y for p in body)
    # The front contact plane of the octagonal bag occupies 66% of its width.
    cx=(xmin+xmax)/2;w=(xmax-xmin)/2;sample_x=[cx+v*w*.66 for v in [-1,-.5,0,.5,1]]
    samples=[(x,z,support(x,z)) for x in sample_x for z in [zmin,(zmin+zmax)/2,zmax]]
    plane=body_plane(samples);residual=max(y-plane.x*x-plane.y*z-plane.z for x,z,y in samples)
    gap=.004;before_gaps=[oldy-support(x,z) for x,z,y in samples]
    def fit_y(x,z):return plane.x*x+plane.y*z+plane.z+residual+gap
    # Keep body/flap/catch; remove only the old two 12-vertex straps.
    bm=bmesh.new();bm.from_mesh(o.data);bm.verts.ensure_lookup_table();bmesh.ops.delete(bm,geom=[v for v in bm.verts if 26<=v.index<50],context='VERTS');bm.to_mesh(o.data);bm.free()
    for v in o.data.vertices:v.co.y=fit_y(v.co.x,v.co.z)+(v.co.y-oldy)
    outer_offset=max(v.co.y-fit_y(v.co.x,v.co.z) for v in o.data.vertices)
    # A rigid flat plane still bridges deep robe folds. Give ONLY the hidden
    # back contact surface a small conforming grid; keep the outer case/flap.
    contact_group=o.vertex_groups.new(name='PART_BagContactBack');contact_group.add([0,1,8,9],1,'REPLACE')
    bm=bmesh.new();bm.from_mesh(o.data);bm.verts.ensure_lookup_table()
    back_face=next(f for f in bm.faces if {v.index for v in f.verts}=={0,1,8,9})
    bmesh.ops.subdivide_edges(bm,edges=list(back_face.edges),cuts=5,use_grid_fill=True)
    bm.to_mesh(o.data);bm.free()
    contact_vertices=[v for v in o.data.vertices if any(g.group==contact_group.index and g.weight>.99 for g in v.groups)]
    assert len(contact_vertices)>=25
    for v in contact_vertices:v.co.y=support(v.co.x,v.co.z)+.004
    start=len(B.meshes)
    for x in [cx-w*.53,cx+w*.53]:
        path=[]
        for z in [1.250,1.215,1.180,1.145,zmax+.014]:
            if z<zmax+.012:continue
            h=belt_tree.ray_cast(Vector((x,1,z)),Vector((0,-1,0)))[0]
            if h is not None:y=h.y+.004
            else:y=max(support(xx,z) for xx in [x-.008,x,x+.008])+.006
            path.append((x,y,z))
        # Fold the strap over the lid, then onto the outer flap.
        path.extend([(x,support(x,zmax)+.006,zmax+.010),(x,fit_y(x,zmax)+outer_offset+.006,zmax+.010),
                     (x,fit_y(x,zmax-.040)+outer_offset+.006,zmax-.040)])
        strap=B.ribbon('V7_RearBeltLoop',path,.018,'leatherEdge','Pelvis',.0035)
        mount=strap.vertex_groups.new(name='PART_BeltMount');mount.add(list(range(len(strap.data.vertices))),1,'REPLACE')
    join(name,[o]+B.meshes[start:])
    contact_indices=[v.index for v in o.data.vertices if any(g.group==o.vertex_groups['PART_BagContactBack'].index and g.weight>.99 for g in v.groups)]
    # A bounded geometric clearance check, not a redesign loop.
    clearance=.004
    for clearance in ([.008] if name.endswith('Short') else [.004,.006,.008]):
        for i in contact_indices:
            v=o.data.vertices[i];v.co.y=support(v.co.x,v.co.z)+clearance
        bpy.context.view_layer.update()
        if not tree(o).overlap(coat_tree):break
    after_gaps=[o.data.vertices[i].co.y-support(o.data.vertices[i].co.x,o.data.vertices[i].co.z) for i in contact_indices]
    # The beginning of JumpStart bows the robe toward the lid. A local 8-mm
    # allowance at the cloth-side turn avoids that contact without moving
    # the whole case away from the body or changing rig/animation weights.
    mount_index=o.vertex_groups['PART_BeltMount'].index
    for v in o.data.vertices:
        if v.co.z<zmax+.005 or not any(g.group==mount_index and g.weight>.99 for g in v.groups):continue
        h=coat_tree.ray_cast(Vector((v.co.x,1,v.co.z)),Vector((0,-1,0)))[0]
        if h is not None and 0<v.co.y-h.y<.025:v.co.y+=.008
    bag_report.append({'object':name,'original_center_x':original_cx,'fitted_center_x':desired_cx,'contact_plane_samples':len(samples),'before_depth_fit_gap_local_m':{'min':min(before_gaps),'max':max(before_gaps)},
                       'after_gap_local_m':{'min':min(after_gaps),'max':max(after_gaps)},'contact_back_vertices':len(contact_indices),'leather_belt_loops':2,'plane_coefficients':list(plane)})

# Checks preserve source and all original animation/rest data.
allmeshes=meshes()
for o in allmeshes:
    bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()
    for f in o.data.polygons:f.use_smooth=False
qa=mesh_report(rig,allmeshes)
assert all(m['finite'] and not m['zero_area_faces'] and not m['subdivision'] for m in qa['meshes']),str([m for m in qa['meshes'] if m['zero_area_faces']])
assert all(not w['unweighted'] and not w['non_normalized'] and w['max_influences']<=4 for w in qa['weights'])
assert all(h['connected_islands']==1 for h in hand_report)
assert actions=={a.name:digest(action_payload(a)) for a in bpy.data.actions}
assert rest=={b.name:[list(row) for row in b.matrix_local] for b in rig.data.bones}
bpy.context.view_layer.update()
pairs=[('AW_RearPouch_Long','AW_OuterCoat'),('AW_RearPouch_Short','AW_OuterCoat')]
contacts=surface_contacts(allmeshes,pairs,max_pairs=4)
assert all(c['face_pairs']==0 for c in contacts),str(contacts)
after=evaluated_bounds(allmeshes)

# The studio/cameras/light/colors are inherited unchanged from V6.
scene.cycles.samples=16 if '--draft' in sys.argv else 40;scene.render.resolution_percentage=65 if '--draft' in sys.argv else 100
scene.camera=bpy.data.objects['Camera_Perspective']
hand=bpy.data.objects['AW_ConnectedGlove_L'];handcenter=sum((hand.matrix_world@v.co for v in hand.data.vertices),Vector())/len(hand.data.vertices)
detail_camera('Camera_DetailHands',handcenter+Vector((0,0,-.025)),(1,-2,.30),.255)
detail_camera('Camera_DetailShoes',(0,-.07,.13),(.85,-1,.55),.72)
detail_camera('Camera_DetailMantle',(0,.15,1.57),(0,1,.10),.82)
detail_camera('Camera_DetailBags',(.06,.14,1.12),(1,.1,.04),.78)
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'AuraWeaver_V7.blend'),compress=True)
def render(name):
    scene.camera=bpy.data.objects['Camera_'+name];scene.render.filepath=str(out/(('Draft-' if '--draft' in sys.argv else '')+name+'.png'))
    hidden=[]
    if name=='DetailHands':
        for o in allmeshes:
            if o.name!='AW_ConnectedGlove_L':hidden.append((o,o.hide_render));o.hide_render=True
    bpy.ops.render.render(write_still=True)
    for o,state in hidden:o.hide_render=state
for name in ['Front','Side','Back','Perspective','DetailHands','DetailShoes','DetailMantle','DetailBags']:render(name)
samples=[]
if '--draft' not in sys.argv:
    rig.data.pose_position='POSE'
    for action in bpy.data.actions:
        set_action(rig,action);low,high=action.frame_range
        for t in [0,.5,1]:
            scene.frame_set(round(low+(high-low)*t));bpy.context.view_layer.update();bounds=evaluated_bounds(allmeshes)
            assert all(math.isfinite(x) and abs(x)<8 for x in bounds['min']+bounds['max'])
            samples.append({'action':action.name,'frame':scene.frame_current,'bounds':bounds,'bag_coat_contacts':surface_contacts(allmeshes,pairs,max_pairs=1)})
report={'source':str(src),'source_sha256':source_hash,'source_unchanged':source_hash==hashlib.sha256(src.read_bytes()).hexdigest(),
 'rest_bones_unchanged':True,'original_actions_unchanged':True,'bone_count':len(rest),'actions':list(actions),'before_bounds':before,'after_bounds':after,
 'mesh_count':len(allmeshes),'triangles':sum(len(f.vertices)-2 for o in allmeshes for f in o.data.polygons),
 'hands':hand_report,'shoes':shoe_report,'rear_pouches':bag_report,'rest_bag_coat_contacts':contacts,'qa':qa,'pose_samples':samples,
 'limits':['Finger rest mesh was curled; unchanged Actions require renewed full deformation testing.',
 'Surface intersection sampling is not a cloth collision solver or Unity animation certification.',
 'Unity project, game release, existing V6 source and main silhouette/closed hood unchanged.']}
(HERE/'validation.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('V7_PASS_OK',json.dumps({k:report[k] for k in ['mesh_count','triangles','rear_pouches','rest_bag_coat_contacts']}))
