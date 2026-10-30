"""V6 reference shape pass on V5. Real bpy geometry, no Unity asset writes.

The illustration is not a topology blueprint. Image landmarks are approximate
observations; material families are interpreted independently of highlights.
"""
import bpy,bmesh,sys,math,json,hashlib
from pathlib import Path
from mathutils import Vector
HERE=Path(__file__).resolve().parent;SOURCE=HERE.parent;ERA=SOURCE.parents[2]
sys.path[:0]=[str(SOURCE),str(ERA/'ArtSource/CharacterFactory')]
from weaver_geometry import Builder,blend_z
from era_character_factory.materials import srgb_to_linear
from era_character_factory.audit import action_payload,digest
from era_character_factory.qa import mesh_report,evaluated_bounds
from animate_aura_weaver import set_action
src=SOURCE/'CorrectionV5/AuraWeaver_V5.blend';source_hash=hashlib.sha256(src.read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(src));scene=bpy.context.scene;rig=bpy.data.objects['Archer_Rig']
rig.data.pose_position='REST';scene.frame_set(1)
actions={a.name:digest(action_payload(a)) for a in bpy.data.actions}
rest={b.name:[list(row) for row in b.matrix_local] for b in rig.data.bones}
optional={'AW_RunicFocus_Independent','AW_FocusHolster'}
def meshes():return [o for o in rig.children if o.type=='MESH' and o.name not in optional]
before=evaluated_bounds(meshes())
M={m.name[3:]:m for m in bpy.data.materials if m.name.startswith('AW_')}
col=bpy.data.collections.new('V6 / reference-shaped garments');scene.collection.children.link(col);B=Builder(col,M,rig)
out=HERE/'Previews';out.mkdir(exist_ok=True)
def lift(z):return z+.015*max(0,min(1,(z-.30)/.66))+.012*max(0,min(1,(z-1.05)/.4))
def part(o,name):
    g=o.vertex_groups.get('PART_'+name)
    return [v for v in o.data.vertices if g and any(w.group==g.index and w.weight>.5 for w in v.groups)]
def join(name,items):
    bpy.ops.object.select_all(action='DESELECT')
    for o in items:o.select_set(True)
    bpy.context.view_layer.objects.active=items[0]
    if len(items)>1:bpy.ops.object.join()
    items[0].name=name;return items[0]
def shell(name,rows,matfn,weight,thickness=.006):
    width=len(rows[0]);v=[Vector((x,y,lift(z))) for row in rows for x,y,z in row];n=len(v);f=[];fm=[]
    for k in range(len(rows)-1):
        for j in range(width-1):
            a=k*width+j;b=a+1;c=b+width;d=a+width
            # Intentional diagonals along long fabric planes, not random noise.
            # Matching explicit diagonals on both sides prevent the lining
            # poking through a twisted non-planar outer quad.
            f.extend([(a,b,c),(a,c,d)]);fm.extend([matfn(k,j),matfn(k,j)])
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
            a,b=values[0];faces.append((a,a+n,b+n,b));mats.append('honey')
    return B.weight(B.mesh(name,v+inner,faces,'ivory',mats),weight)

# Closed SIDE and BACK fabric, explicitly requested even though the drawing
# shows a dark hair band at the rear. Only the face opening remains open.
bpy.data.objects.remove(bpy.data.objects['AW_HoodAssembly'],do_unlink=True)
left=[
 [(-.096,-.132,1.740),(-.127,-.110,1.738),(-.149,.008,1.739),(-.110,.120,1.738)],
 [(-.113,-.157,1.586),(-.145,-.127,1.568),(-.174,.031,1.591),(-.131,.151,1.593)],
 [(-.091,-.168,1.461),(-.137,-.137,1.436),(-.191,.045,1.474),(-.132,.162,1.470)],
 [(-.130,-.132,1.349),(-.160,-.109,1.355),(-.161,.059,1.350),(-.103,.140,1.340)]]
centers=[(0,.144,1.738),(.006,.175,1.592),(-.006,.183,1.465),(0,.165,1.325)]
rows=[row+[centers[k]]+[(-x,y,z+.002 if j<2 else z) for j,(x,y,z) in enumerate(reversed(row))] for k,row in enumerate(left)]
hood=shell('AW_HoodAssembly',rows,lambda k,j:'honey' if j in [0,7] else 'sand' if j in [2,5] and k in [1,2] else 'ivory',lambda p:blend_z(p,lift(1.37),lift(1.60),'Chest','Head'))

# The reference belt and shoulders sit substantially HIGHER than V5.
# Apply the same continuous landmark warp to clothing and attachments.
# Bone rest matrices and original Actions remain byte-for-byte equivalent;
# this is a mesh fitting pass, not a new animation certification.
anchors=[(0,0),(.15,.15),(.78,.85),(.90,1.00),(1.05,1.225),(1.35,1.535),(1.48,1.665),(1.75,1.805),(2.009,2.009)]
def warp(z):
    for (a,b),(c,d) in zip(anchors,anchors[1:]):
        if z<=c:return b+(z-a)*(d-b)/(c-a)
    return z

# Broaden the very small leather cases without scaling their belt anchors.
for side,s in [('L',1),('R',-1)]:
    for name in ['AW_PouchAssembly_'+side,'AW_PouchFittings_'+side]:
        o=bpy.data.objects[name]
        for v in o.data.vertices:
            t=max(0,min(1,(1.02-v.co.z)/.11));cx=s*(.182 if side=='L' else .174)
            v.co.x=cx+(v.co.x-cx)*(1+.23*t);v.co.y-=.014*t
    # Existing connected fingers are retained, with slightly wider palms.
    glove=bpy.data.objects['AW_ConnectedGlove_'+side];cx=s*.225
    for v in glove.data.vertices:v.co.x=cx+(v.co.x-cx)*1.08

# Real collar planes form a lower, pointed wrap, instead of a detached bib.
scarf=bpy.data.objects['AW_Scarf']
for v in part(scarf,'V4_ChestFold'):
    if v.co.z<lift(1.255):v.co.z-=.050*max(0,1-abs(v.co.x)/.13)

# Broader upper sleeve bell: the shoulder mantle is the actual sleeve cap.
mantle=bpy.data.objects['AW_ShoulderMantle']
capeverts=part(mantle,'V4_ShoulderCape')
for i,v in enumerate(capeverts):
    if (i%36)//9==2:v.co.z+=.046
    t=max(0,min(1,(lift(1.36)-v.co.z)/.18));v.co.x*=1+.095*t
    if abs(v.co.x)>.16 and v.co.z<lift(1.30):v.co.z-=.035*t

# Sleeve bands occupy a larger cloth area; angular openings remain hollow.
for side,s in [('L',1),('R',-1)]:
    o=bpy.data.objects['AW_Sleeve_'+side];fore=rig.data.bones['Forearm.'+side]
    for v in o.data.vertices:
        t=max(0,min(1,(1.15-v.co.z)/.30));cx=s*.225;cy=-.15 if side=='L' else -.055
        v.co.x=cx+(v.co.x-cx)*(1+.11*t);v.co.y=cy+(v.co.y-cy)*(1+.07*t)
    for key in ['honey','amber','leatherEdge']:
        if M[key] not in list(o.data.materials):o.data.materials.append(M[key])
    for key in ['ochre','brown']:
        if M[key] not in list(o.data.materials):o.data.materials.append(M[key])
    bm=bmesh.new();bm.from_mesh(o.data)
    for height in [.89,.94,1.00]:
        bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=1e-6,plane_co=(0,0,height),plane_no=(0,0,1),clear_inner=False,clear_outer=False)
    bm.to_mesh(o.data);bm.free()
    for f in o.data.polygons:
        old=o.data.materials[f.material_index].name
        if old not in ['AW_brown','AW_charcoal']:
            z=sum(o.data.vertices[i].co.z for i in f.vertices)/len(f.vertices)
            key='brown' if z<.89 else 'ochre' if z<.94 else 'amber' if z<1.00 else 'ivory'
            f.material_index=list(o.data.materials).index(M[key])

# Reference-size forearm-length cases and belt survive as coherent groups.
for o in meshes():
    for v in o.data.vertices:v.co.z=warp(v.co.z)

# Keep a readable human face: the broad torso fitting must not squash the
# face by the same amount. Forehead stays beneath the hat; chin drops 5 cm.
head=bpy.data.objects['AW_Head'];headtop=warp(1.721)
for v in head.data.vertices:v.co.z=headtop+(v.co.z-headtop)*1.43
for v in part(head,'AW_HumanFace'):
    if v.co.z<1.64:v.co.x*=.68
for v in part(head,'AW_Nose'):
    v.co.y=-.113+(v.co.y+.113)*.68
hair=bpy.data.objects['AW_Hair']
for name in ['AW_FrontLock_0','AW_FrontLock_1','AW_FrontLock_2','AW_FrontLock_3']:
    for v in part(hair,name):
        t=max(0,min(1,(warp(1.71)-v.co.z)/.14));v.co.z-=.012*t
start=len(B.meshes)
for side,s,zlow in [('Long',-1,1.40),('Short',1,1.52)]:
    root=Vector((s*.066,-.114,1.790));mid=Vector((s*.094,-.185,1.65));tip=Vector((s*.130,-.204,zlow))
    v=[root+Vector((-.019,0,0)),root+Vector((.019,0,0)),mid+Vector((s*.020,-.008,0)),tip,mid+Vector((-s*.013,.009,0))]
    B.rigid(B.mesh('V6_FaceFramingLock_'+side,v,[(0,1,2),(0,2,4),(4,2,3),(1,3,2),(0,4,3),(0,3,1)],'hair',['hair','hairPlane','hair','hair','hairPlane','hair']),'Head')
join('AW_Hair',[hair]+B.meshes[start:])

for name in ['AW_OuterCoat','AW_InnerCoat','AW_RearRobeInlay']:
    for v in bpy.data.objects[name].data.vertices:
        t=max(0,min(1,(1.20-v.co.z)/1.0));v.co.x*=1+.07*t
coat=bpy.data.objects['AW_OuterCoat']
for name in ['V4_Coat_Front.L','V4_Coat_Front.R']:
    for v in part(coat,name):
        if v.co.z<.24:v.co.z-=.022*max(0,1-abs(v.co.x)/.30)*max(0,(.24-v.co.z)/.13)
inner=bpy.data.objects['AW_InnerCoat']
for name in ['V3_DarkInnerRobe_False']:
    for v in part(inner,name):
        if v.co.z<.20:v.co.z-=.028*max(0,(.20-v.co.z)/.085)*max(0,1-abs(v.co.x)/.16)

# Dark leather keepers, metallic center buckle. V5's repeated gold bars
# incorrectly made the belt look like a bright ammunition bandolier.
belt=bpy.data.objects['AW_Belt']
for f in belt.data.polygons:
    c=sum((belt.data.vertices[i].co for i in f.vertices),Vector())/len(f.vertices)
    if belt.data.materials[f.material_index]==M['bronze'] and abs(c.x)>.045:
        f.material_index=list(belt.data.materials).index(M['leatherEdge'])

# Hat: wide, swept brim, continuous folded crown, visible open buckle.
hat=bpy.data.objects['AW_Hat'];brim=part(hat,'V3_HatBrim')
for i,v in enumerate(brim):
    if i<12 or 24<=i<36:
        a=(i%12)*math.tau/12
        v.co.x*=.98;v.co.y*=1.015
        v.co.z+=.014*math.sin(a)-.016*max(0,math.cos(a))
crown=part(hat,'V3_HatBentCrown')
for v in crown:
    if v.co.x>.09:v.co.x=.09+(v.co.x-.09)*1.28;v.co.z-=.014
for v in part(hat,'V3_HatLeatherBand'):v.co.z=warp(lift(1.774))+(v.co.z-warp(lift(1.774)))*1.20
for v in part(hat,'V3_HatOpenBuckle'):
    center=Vector((.056,-.122,warp(lift(1.778))))
    v.co=center+(v.co-center)*1.35
from mathutils import Matrix
hatpivot=Vector((0,.014,warp(lift(1.744))));hattilt=Matrix.Rotation(.085,3,'Y')
for v in hat.data.vertices:v.co=hatpivot+hattilt@(v.co-hatpivot)

start=len(B.meshes)
# Cut the graphic into the actual cape's triangle domains. This avoids a
# large planar badge bridging folds and projecting past the sleeve.
from mathutils.geometry import tessellate_polygon
def clip_triangle(poly,domain):
    result=list(poly)
    for a,b in zip(domain,domain[1:]+domain[:1]):
        def cross(p):return (b.x-a.x)*(p.y-a.y)-(b.y-a.y)*(p.x-a.x)
        old=result;result=[]
        if not old:break
        for p,q in zip(old,old[1:]+old[:1]):
            cp,cq=cross(p),cross(q)
            if cp>=-1e-9:result.append(p)
            if (cp>0)!=(cq>0) and abs(cp-cq)>1e-10:result.append(p+(q-p)*(cp/(cp-cq)))
    return result
for s in [-1,1]:
    def sw(p):
        t=max(0,min(1,(abs(p.x)-.13)/.135))*.82
        return {'Chest':1-t,'UpperArm.'+('L' if p.x>0 else 'R'):t}
    field=[(.22,.20),(1.84,.20),(1.80,.95),(1.15,1.65),(.24,.86)]
    chevron=[(.30,.36),(1.13,1.28),(1.74,.57),(1.74,.84),(1.13,1.56),(.29,.65)]
    for outline,mat,offset in [(field,'ochre',.002),(chevron,'honey',.0035)]:
        shape=[Vector((x,y+.72,0)) for x,y in outline]
        for shape_tri in tessellate_polygon([shape]):
            shape_tri=[shape[p] if isinstance(p,int) else p for p in shape_tri]
            for row in range(3):
                for column in range(2):
                    uv=[Vector((column,row,0)),Vector((column+1,row,0)),Vector((column+1,row+1,0)),Vector((column,row+1,0))]
                    for indices in [(0,1,2),(0,2,3)]:
                        domain=[uv[i] for i in indices];clipped=clip_triangle(list(shape_tri),domain)
                        if len(clipped)<3:continue
                        def on_cape(p):
                            # Barycentric mapping exactly follows the stored
                            # triangle, including the existing asymmetry.
                            a,b,c=domain;den=(b.y-c.y)*(a.x-c.x)+(c.x-b.x)*(a.y-c.y)
                            u=((b.y-c.y)*(p.x-c.x)+(c.x-b.x)*(p.y-c.y))/den
                            v=((c.y-a.y)*(p.x-c.x)+(a.x-c.x)*(p.y-c.y))/den
                            points=[]
                            for q in domain:
                                colidx=int(q.x) if s<0 else 8-int(q.x)
                                points.append(capeverts[int(q.y)*9+colidx].co.copy())
                            normal=(points[1]-points[0]).cross(points[2]-points[0]).normalized()
                            if normal.y>0:normal=-normal
                            return points[0]*u+points[1]*v+points[2]*(1-u-v)+normal*offset
                        verts=[]
                        for p in clipped:
                            point=on_cape(p)
                            if not verts or (point-verts[-1]).length>1e-6:verts.append(point)
                        if len(verts)>1 and (verts[0]-verts[-1]).length<1e-6:verts.pop()
                        if len(verts)<3:continue
                        area=sum((verts[i]-verts[0]).cross(verts[i+1]-verts[0]).length for i in range(1,len(verts)-1))
                        if area<2e-6:continue
                        o=B.panel('V6_StitchedShoulderGraphic',verts,.001,'ochre' if mat=='ochre' else 'honey',normal=(0,1,0));B.weight(o,sw)
if B.meshes[start:]:join('AW_ShoulderInlay',B.meshes[start:])

# Intentional low-poly coloration, with wide dark brown hem and ochre wedges.
# Preserve cloth topology. Split only chosen quads to create diagonal insets.
coat=bpy.data.objects['AW_OuterCoat']
bm=bmesh.new();bm.from_mesh(coat.data)
for height in [.23,.32,.42,.54]:
    bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=1e-6,plane_co=(0,0,height),plane_no=(0,0,1),clear_inner=False,clear_outer=False)
selected=[f for f in bm.faces if len(f.verts)==4 and .30<f.calc_center_median().z<.92]
bmesh.ops.triangulate(bm,faces=selected,quad_method='FIXED');bm.to_mesh(coat.data);bm.free()
for key in ['ivory','light','sand','honey','amber','ochre','brown','leatherEdge']:
    if M[key] not in list(coat.data.materials):coat.data.materials.append(M[key])
for f in coat.data.polygons:
    c=sum((coat.data.vertices[i].co for i in f.vertices),Vector())/len(f.vertices)
    # Retain dark inner surfaces. Visible outer planes get designed regions.
    old=coat.data.materials[f.material_index].name[3:]
    if old=='fold':continue
    x=abs(c.x);z=c.z
    if z<.23:key='brown'
    elif z<.32:key='ochre' if x>.15 else 'leatherEdge'
    elif z<.42:key='amber' if x>.11 else 'honey'
    elif z<.53:key='honey' if x>.16 else 'sand'
    elif z<.84 and abs(x-(.11+.16*(.84-z)))<.041:key='amber' if x>.12 else 'honey'
    else:key='light' if x<.10 else 'ivory' if old!='sand' else 'sand'
    if c.y>0 and .32<z<.80 and x<.18*max(0,1-abs(z-.51)/.30):key='honey' if z>.51 else 'amber'
    f.material_index=list(coat.data.materials).index(M[key])

# Palette values are LOCAL pigments, not sampled sunlight colors.
colors={'ivory':'E7D2A6','light':'F1DEB7','sand':'D2AE77','fold':'96774F','honey':'D7A047','amber':'BF7C29',
 'ochre':'885029','brown':'3B2925','leather':'633D2A','leatherEdge':'8F5630','bronze':'BC8435',
 'charcoal':'242323','inner':'302923','hair':'1B2020','hairPlane':'34332E','skin':'D19B79','skinShade':'AC7454'}
for key,colhex in colors.items():
    m=M[key];rgba=[srgb_to_linear(int(colhex[i:i+2],16)/255) for i in (0,2,4)]+[1]
    m.diffuse_color=rgba;next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED').inputs['Base Color'].default_value=rgba

# Final normalized FRONT/SIDE silhouette fit. The concept has a slim belt,
# a less bulky upper body and a wide hem. Reduce upper widths/depths only;
# do not uniformly shrink the whole figure or stretch the legs.
def width_fit(z):
    if z<.20:return 1
    if z<1.225:return 1-.20*(z-.20)/1.025
    if z<1.55:return .80+.10*(z-1.225)/.325
    return .90
for o in meshes():
    if o.name in ['AW_Head','AW_Hair','AW_Hat','AW_Neck']:continue
    for v in o.data.vertices:
        if o.name=='AW_HoodAssembly':
            v.co.y=-.15+(v.co.y+.15)*.87
        elif o.name.startswith('AW_ConnectedGlove_'):
            cx=.225 if v.co.x>0 else -.225;v.co.x=cx*.87+(v.co.x-cx)*.95;v.co.y*=.90
        elif o.name.startswith('AW_Sleeve_'):
            v.co.x*=.87;v.co.y*=.90
        elif o.name.startswith(('AW_Boot_','AW_Leg_')):
            v.co.x*=.95
        else:
            v.co.x*=.90 if o.name in ['AW_ShoulderMantle','AW_ShoulderInlay'] else width_fit(v.co.z)
            v.co.y*=.85
for v in bpy.data.objects['AW_Hair'].data.vertices:
    if v.co.y>0:v.co.y*=.72

allmeshes=meshes()
for o in allmeshes:
    bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()
    for f in o.data.polygons:f.use_smooth=False
qa=mesh_report(rig,allmeshes)
assert all(m['finite'] and not m['zero_area_faces'] and not m['subdivision'] for m in qa['meshes']),str([m for m in qa['meshes'] if m['zero_area_faces']])
assert all(not w['unweighted'] and not w['non_normalized'] and w['max_influences']<=4 for w in qa['weights'])
assert actions=={a.name:digest(action_payload(a)) for a in bpy.data.actions}
assert rest=={b.name:[list(row) for row in b.matrix_local] for b in rig.data.bones}
bpy.context.view_layer.update();after=evaluated_bounds(allmeshes)
scene.render.resolution_percentage=65 if '--draft' in sys.argv else 100;scene.cycles.samples=16 if '--draft' in sys.argv else 40
# Existing studio lamps, a stronger neutral key, and a light CAMERA-only
# background. Pigment colors above do not include sunlight highlights.
for name,energy in [('Key',750),('Fill',260),('Rim',420)]:bpy.data.objects[name].data.energy=energy
world=scene.world;nodes=world.node_tree.nodes;links=world.node_tree.links
background=next(n for n in nodes if n.type=='BACKGROUND');output=next(n for n in nodes if n.type=='OUTPUT_WORLD')
background.inputs['Strength'].default_value=.27
try:scene.view_settings.look='AgX - Medium High Contrast'
except TypeError:pass
camera_bg=nodes.new('ShaderNodeBackground');camera_bg.inputs['Color'].default_value=(.90,.91,.90,1);camera_bg.inputs['Strength'].default_value=.8
lightpath=nodes.new('ShaderNodeLightPath');mix=nodes.new('ShaderNodeMixShader')
links.new(lightpath.outputs['Is Camera Ray'],mix.inputs[0]);links.new(background.outputs[0],mix.inputs[1]);links.new(camera_bg.outputs[0],mix.inputs[2]);links.new(mix.outputs[0],output.inputs['Surface'])
for name in ['Front','Side','Back']:
    cam=bpy.data.objects['Camera_'+name];cam.data.ortho_scale=2.38;cam.location.z=1.08
    cam.rotation_euler=(Vector((0,0,1.08))-cam.location).to_track_quat('-Z','Y').to_euler()
def render_view(name,file):
    scene.camera=bpy.data.objects['Camera_'+name];scene.render.filepath=str(out/file);bpy.ops.render.render(write_still=True)
scene.camera=bpy.data.objects['Camera_Perspective']
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'AuraWeaver_V6.blend'),compress=True)
for name in ['Front','Side','Back','Perspective']:render_view(name,('Draft-' if '--draft' in sys.argv else '')+name+'.png')

samples=[]
if '--draft' not in sys.argv:
    rig.data.pose_position='POSE'
    for a in bpy.data.actions:
        set_action(rig,a);first,last=a.frame_range
        for t in [0,.5,1]:
            scene.frame_set(round(first+(last-first)*t));bpy.context.view_layer.update()
            bounds=evaluated_bounds(allmeshes)
            assert all(math.isfinite(x) and abs(x)<8 for x in bounds['min']+bounds['max'])
            samples.append({'action':a.name,'frame':scene.frame_current,'bounds':bounds})
    set_action(rig,bpy.data.actions['AuraWeaver_AuraChannel']);scene.frame_set(22);bpy.context.view_layer.update()
    # Reuse the existing demo rune, raised with the newly fitted torso.
    with bpy.data.libraries.load(str(SOURCE/'CorrectionV5/AuraWeaver_V5_Magic.blend'),link=False) as (available,wanted):
        wanted.collections=[n for n in available.collections if n.startswith('DEMO ONLY')]
    for c in wanted.collections:
        if c:
            scene.collection.children.link(c)
            for o in c.all_objects:o.location.z+=.11
    render_view('Front','Front-Magic.png')
    bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'AuraWeaver_V6_Magic.blend'),compress=True)
report={'source':str(src),'source_sha256':source_hash,'source_unchanged':source_hash==hashlib.sha256(src.read_bytes()).hexdigest(),
 'rest_bones_unchanged':True,'original_actions_unchanged':True,'actions':list(actions),'bone_count':len(rest),
 'before_bounds':before,'after_bounds':after,'mesh_count':len(allmeshes),'triangles':sum(len(f.vertices)-2 for o in allmeshes for f in o.data.polygons),
 'mesh_landmark_warp':anchors,'qa':qa,'pose_samples':samples,'colors_srgb':colors,
 'limits':['Illustration polygons cannot be measured as actual 3D topology. No objective 90-percent score.',
 'Closed rear cowl follows user instruction; differs from illustrated exposed rear hair.',
 'Rig and Actions preserved, but new garment fitting requires deformation and Unity revalidation. No game files modified.']}
(HERE/'validation.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('V6_PASS_OK',json.dumps({k:report[k] for k in ['mesh_count','triangles','before_bounds','after_bounds']}))
