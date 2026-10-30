"""V8 geometry edits on V7. Does not alter the skeleton or Actions."""
import bpy,bmesh,math
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree
from weaver_geometry import Builder

def part(o,name):
    g=o.vertex_groups.get('PART_'+name)
    return [v for v in o.data.vertices if g and any(w.group==g.index and w.weight>.99 for w in v.groups)]
def tree(o):return BVHTree.FromPolygons([v.co for v in o.data.vertices],[list(f.vertices) for f in o.data.polygons])
def swapped(name):
    for a,b in [('.L','.R'),('_L','_R')]:
        if name.endswith(a):return name[:-len(a)]+b
        if name.endswith(b):return name[:-len(b)]+a
    return name
def join(name,items):
    bpy.ops.object.select_all(action='DESELECT')
    for o in items:o.select_set(True)
    bpy.context.view_layer.objects.active=items[0]
    if len(items)>1:bpy.ops.object.join()
    items[0].name=name;return items[0]
def recalc(o):
    bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()
    for f in o.data.polygons:f.use_smooth=False

def mirror_cloth(o):
    # Copy the positive half, including exact face layout and material roles.
    names=[g.name for g in o.vertex_groups]
    for name in names:
        target=swapped(name)
        if not o.vertex_groups.get(target):o.vertex_groups.new(name=target)
    mapping={g.index:o.vertex_groups[swapped(g.name)].index for g in o.vertex_groups}
    bm=bmesh.new();bm.from_mesh(o.data)
    bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=1e-7,plane_co=(0,0,0),plane_no=(1,0,0))
    bmesh.ops.delete(bm,geom=[v for v in bm.verts if v.co.x<-1e-7],context='VERTS')
    seamfaces=[f for f in bm.faces if all(abs(v.co.x)<1e-7 for v in f.verts)]
    bmesh.ops.delete(bm,geom=seamfaces,context='FACES_ONLY')
    # Independent cloth layers may overlap on X=0. Keep their seam welding
    # separate; a global seam merge would weld unrelated rear panel returns.
    component=bm.verts.layers.int.get('V8_component') or bm.verts.layers.int.new('V8_component')
    pending=set(bm.verts);component_count=0
    while pending:
        todo=[pending.pop()]
        while todo:
            v=todo.pop();v[component]=component_count
            for e in v.link_edges:
                other=e.other_vert(v)
                if other in pending:pending.remove(other);todo.append(other)
        component_count+=1
    layer=bm.verts.layers.deform.verify()
    result=bmesh.ops.duplicate(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces))
    for v in result['geom']:
        if isinstance(v,bmesh.types.BMVert):
            v.co.x=-v.co.x;weights=dict(v[layer]);v[layer].clear()
            for i,w in weights.items():v[layer][mapping[i]]=w
    bmesh.ops.reverse_faces(bm,faces=[f for f in result['geom'] if isinstance(f,bmesh.types.BMFace)])
    for i in range(component_count):
        bmesh.ops.remove_doubles(bm,verts=[v for v in bm.verts if abs(v.co.x)<1e-7 and v[component]==i],dist=1e-7)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()

def sleeve_pair():
    left=bpy.data.objects['AW_Sleeve_L'];right=bpy.data.objects['AW_Sleeve_R']
    paths=[]
    for o,s in [(left,'L'),(right,'R')]:
        v=part(o,'V3_BellSleeve_'+s);assert len(v)>=48
        paths.append([sum((p.co for p in v[i*8:i*8+8]),Vector())/8 for i in range(6)])
    def frame(a,b,sign):
        d=(b-a).normalized();u=Vector((sign,0,0));u=(u-d*u.dot(d)).normalized();n=d.cross(u)
        if n.y>0:n=-n
        return d,u,n
    mesh=left.data.copy();old=right;new=left.copy();new.data=mesh;old.users_collection[0].objects.link(new)
    for v in mesh.vertices:
        best=None
        for k in range(5):
            a,b=paths[0][k:k+2];t=max(0,min(1,(v.co-a).dot(b-a)/(b-a).length_squared));p=a.lerp(b,t)
            value=(v.co-p).length_squared
            if best is None or value<best[0]:best=(value,k,t,p)
        _,k,t,p=best;d,u,n=frame(paths[0][k],paths[0][k+1],1);dd,uu,nn=frame(paths[1][k],paths[1][k+1],-1)
        offset=v.co-p;v.co=paths[1][k].lerp(paths[1][k+1],t)+uu*offset.dot(u)+nn*offset.dot(n)
    rename=[(g,swapped(g.name)) for g in new.vertex_groups]
    for i,(g,target) in enumerate(rename):g.name='V8_TEMP_'+str(i)
    for g,target in rename:g.name=target
    bpy.data.objects.remove(old,do_unlink=True);new.name='AW_Sleeve_R';recalc(new)
    return paths

def densify(o,threshold=.10,cuts=1):
    bm=bmesh.new();bm.from_mesh(o.data)
    selected=[e for e in bm.edges if e.calc_length()>threshold]
    if selected:bmesh.ops.subdivide_edges(bm,edges=selected,cuts=cuts,use_grid_fill=True)
    bmesh.ops.triangulate(bm,faces=[f for f in bm.faces if len(f.verts)>4],quad_method='FIXED')
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()

def fit_bag_skin(rig):
    """Bind leather and mounts to their supporting garment field, not a
    stationary hip alone while the animated robe moves through the cases.
    Rest geometry, bones and animation curves remain unchanged.
    """
    coat=bpy.data.objects['AW_OuterCoat'];coat.data.calc_loop_triangles();vertices=[v.co.copy() for v in coat.data.vertices];surfaces={}
    for sign in [-1,1]:
        triangles=[tuple(t.vertices) for t in coat.data.loop_triangles if sum(vertices[i].y for i in t.vertices)*sign>0]
        surfaces[sign]=(BVHTree.FromPolygons(vertices,triangles,all_triangles=True),triangles)
    for name,sign in [('AW_PouchAssembly_L',-1),('AW_PouchAssembly_R',-1),('AW_RearPouch_Long',1),('AW_RearPouch_Short',1)]:
        o=bpy.data.objects[name];surface,triangles=surfaces[sign]
        for v in o.data.vertices:
            if v.co.z>1.18:weights={'Pelvis':1}
            else:
                hit=surface.ray_cast(Vector((v.co.x,sign,v.co.z)),Vector((0,-sign,0)))
                if hit[0] is None:hit=surface.find_nearest(Vector((v.co.x,sign*.12,v.co.z)))
                p,normal,face,distance=hit;a,b,c=[vertices[i] for i in triangles[face]]
                v0=b-a;v1=c-a;v2=p-a;d00=v0.dot(v0);d01=v0.dot(v1);d11=v1.dot(v1);den=d00*d11-d01*d01
                wb=(d11*v2.dot(v0)-d01*v2.dot(v1))/den;wc=(d00*v2.dot(v1)-d01*v2.dot(v0))/den
                weights={}
                for i,t in zip(triangles[face],[1-wb-wc,wb,wc]):
                    for g in coat.data.vertices[i].groups:
                        bone=coat.vertex_groups[g.group].name
                        if bone in rig.data.bones:weights[bone]=weights.get(bone,0)+max(0,t)*g.weight
                weights=dict(sorted(weights.items(),key=lambda p:p[1],reverse=True)[:4]);total=sum(weights.values());weights={n:w/total for n,w in weights.items() if w>1e-8}
            for g in o.vertex_groups:
                if g.name in rig.data.bones:g.remove([v.index])
            for bone,value in weights.items():
                g=o.vertex_groups.get(bone) or o.vertex_groups.new(name=bone);g.add([v.index],value,'REPLACE')

def run(rig,M,collection):
    B=Builder(collection,M,rig);report={'heels':[],'front_pouches':[]}
    # Refine only the existing clothing meshes. Exact matching halves make
    # the painted pattern auditable independently of light and pose.
    mirror_names=['AW_OuterCoat','AW_InnerCoat','AW_HoodAssembly','AW_ShoulderMantle','AW_ShoulderInlay','AW_RearMantleAccent','AW_RearRobeInlay']
    for name in mirror_names:
        o=bpy.data.objects[name]
        if name in ['AW_OuterCoat','AW_ShoulderMantle']:densify(o,.105)
        mirror_cloth(o)
    # Equal sleeve topology, fitted to the existing left/right centerlines.
    paths=sleeve_pair();densify(bpy.data.objects['AW_Sleeve_L'],.078)
    # Densification is copied through the same fitting to keep face pairing exact.
    # Original centerline anchors remain the first 48 part vertices.
    sleeve_pair()

    for side in ['L','R']:
        o=bpy.data.objects['AW_Boot_'+side];foot=part(o,'Boot foot '+side);assert len(foot)==94
        body=foot[:54];bone=rig.data.bones['Foot.'+side];origin=bone.head_local.copy();origin.x*=.95
        d=bone.tail_local-bone.head_local;d.x*=.95;d.z=0;d.normalize();u=d.cross(Vector((0,0,1))).normalized()
        amin=min((v.co-origin).dot(d) for v in body);amax=max((v.co-origin).dot(d) for v in body)
        lo=min((v.co-origin).dot(u) for v in body);hi=max((v.co-origin).dot(u) for v in body);uc=(lo+hi)/2;w=(hi-lo)*.46
        for k in range(6):
            t=sum((body[k*9+j].co-origin).dot(d) for j in range(9))/9
            raise_z=.028 if t<=.030 else .028*(1-(t-.030)/.115) if t<.145 else 0
            for j in [0,1,2,6,7,8]:body[k*9+j].co.z+=max(0,raise_z)
        start=len(B.meshes)
        outline=[(-.75,amin),(.75,amin),(1,amin+.011),(1,.020),(.77,.032),(-.77,.032),(-1,.020),(-1,amin+.011)]
        rings=[[origin+d*t+u*(uc+a*w)+Vector((0,0,z-origin.z)) for a,t in outline] for z in [0,.030]]
        heel=B.loft('V8_BlockHeel_'+side,rings,'sole','Foot.'+side)
        g=heel.vertex_groups.new(name='PART_BlockHeel_'+side);g.add(list(range(len(heel.data.vertices))),1,'REPLACE')
        join(o.name,[o,heel]);report['heels'].append({'side':side,'height_local_m':.030,'arch_clearance_max_local_m':.028,'floor_preserved':True})

    coat=bpy.data.objects['AW_OuterCoat'];coat_tree=tree(coat);belt_tree=tree(bpy.data.objects['AW_Belt'])
    other_support=[tree(bpy.data.objects[n]) for n in ['AW_InnerCoat','AW_Bodice','AW_Belt']]
    def support(x,z,sign):
        h=coat_tree.ray_cast(Vector((x,sign,z)),Vector((0,-sign,0)))[0]
        if h is None or h.y*sign<0:
            hits=[t.ray_cast(Vector((x,sign,z)),Vector((0,-sign,0)))[0] for t in other_support]
            hits=[p for p in hits if p is not None and p.y*sign>0]
            if not hits:raise RuntimeError('No cloth support at '+str((x,z,sign)))
            h=max(hits,key=lambda p:p.y*sign)
        return h.y
    for side,target in [('L',.102),('R',-.090)]:
        o=join('AW_PouchAssembly_'+side,[bpy.data.objects['AW_PouchAssembly_'+side],bpy.data.objects['AW_PouchFittings_'+side]])
        body=part(o,'AW_Pouch_'+side);assert len(body)==16
        cx=sum(v.co.x for v in body)/16;dx=target-cx
        keep_hanging={v.index for name in ['AW_HangingStrap_'+side,'AW_StrapTip_'+side] for v in part(o,name)}
        for v in o.data.vertices:
            if v.index not in keep_hanging:v.co.x+=dx
        body=[v.co.copy() for v in part(o,'AW_Pouch_'+side)]
        xmin=min(p.x for p in body);xmax=max(p.x for p in body);zmin=min(p.z for p in body);zmax=max(p.z for p in body);oldy=max(p.y for p in body)
        w=(xmax-xmin)/2;cx=(xmin+xmax)/2
        # Keep the body/flap/fittings; remove only the obsolete bag strap.
        delete={v.index for v in part(o,'AW_PouchStrap_'+side)}
        bm=bmesh.new();bm.from_mesh(o.data);bm.verts.ensure_lookup_table();bmesh.ops.delete(bm,geom=[v for v in bm.verts if v.index in delete],context='VERTS');bm.to_mesh(o.data);bm.free()
        # The case can lean against the sloping cloth, while its outer front
        # retains its thickness. Clamp lateral samples to its contact face.
        samples=[-support(cx+a*w*.55,z,-1) for a in [-1,0,1] for z in [zmin,zmax]]
        ybase=-max(samples)-.006
        hanging={v.index for name in ['AW_HangingStrap_'+side,'AW_StrapTip_'+side] for v in part(o,name)}
        hanging_gap={}
        for name in ['AW_HangingStrap_'+side,'AW_StrapTip_'+side]:
            vertices=part(o,name);half=len(vertices)//2
            for k,v in enumerate(vertices):hanging_gap[v.index]=.010 if k<half else .005
        for v in o.data.vertices:
            if v.index in hanging:
                v.co.y=support(v.co.x,v.co.z,-1)-hanging_gap[v.index]
            else:v.co.y=ybase+(v.co.y-oldy)*.82
        bodyids={v.index for v in part(o,'AW_Pouch_'+side)}
        backface=max((f for f in o.data.polygons if all(i in bodyids for i in f.vertices)),key=lambda f:sum(o.data.vertices[i].co.y for i in f.vertices)/len(f.vertices))
        assert len(backface.vertices)==4
        ids=set(backface.vertices);g=o.vertex_groups.new(name='PART_FrontContactBack');g.add(list(ids),1,'REPLACE')
        bm=bmesh.new();bm.from_mesh(o.data);bm.verts.ensure_lookup_table();face=next(f for f in bm.faces if {v.index for v in f.verts}==ids)
        bmesh.ops.subdivide_edges(bm,edges=list(face.edges),cuts=5,use_grid_fill=True);bm.to_mesh(o.data);bm.free()
        ci=[v.index for v in o.data.vertices if any(x.group==g.index and x.weight>.99 for x in v.groups)]
        for i in ci:
            v=o.data.vertices[i];v.co.y=support(v.co.x,v.co.z,-1)-.008
        start=len(B.meshes);outer=min(v.co.y for v in o.data.vertices if v.index not in hanging)
        for x in [cx-w*.45,cx+w*.45]:
            path=[]
            for z in [1.250,1.215,1.180,1.145,zmax+.020]:
                if z<zmax+.019:continue
                rootx=max(-.100,min(.100,x));xx=rootx+(x-rootx)*max(0,min(1,(1.250-z)/max(.05,1.250-zmax-.012)))
                h=belt_tree.ray_cast(Vector((xx,-1,z)),Vector((0,1,0)))[0]
                y=h.y-.004 if h is not None else min(support(uu,z,-1) for uu in [xx-.008,xx,xx+.008])-.012
                path.append((xx,y,z))
            path.extend([(x,support(x,zmax,-1)-.012,zmax+.012),(x,outer-.004,zmax+.012),(x,outer-.004,zmax-.025)])
            clean=[]
            for p in path:
                if not clean or (Vector(p)-Vector(clean[-1])).length>1e-5:clean.append(p)
            path=list(map(Vector,clean));rings=[]
            for k,p in enumerate(path):
                tangent=(path[min(k+1,len(path)-1)]-path[max(0,k-1)]).normalized()
                side_axis=Vector((1,0,0));side_axis=(side_axis-tangent*side_axis.dot(tangent)).normalized()
                normal=tangent.cross(side_axis).normalized()
                rings.append([p+side_axis*a*.009+normal*b*.00175 for a,b in [(-1,-1),(1,-1),(1,1),(-1,1)]])
            B.loft('V8_FrontBeltLoop',rings,'leatherEdge','Pelvis')
        join(o.name,[o]+B.meshes[start:]);report['front_pouches'].append({'side':side,'center_x':target,'contact_vertices':len(ci),'clearance_local_m':.008,'belt_loops':2})

    # Re-seat the retained rear backing on the now-symmetric robe.
    for name,gap in [('AW_RearPouch_Long',.008),('AW_RearPouch_Short',.008)]:
        o=bpy.data.objects[name];g=o.vertex_groups['PART_BagContactBack']
        for v in o.data.vertices:
            if any(w.group==g.index and w.weight>.99 for w in v.groups):v.co.y=support(v.co.x,v.co.z,1)+gap
    for o in rig.children:
        if o.type=='MESH':recalc(o)
    fit_bag_skin(rig)
    report['mirror_objects']=mirror_names;return report
