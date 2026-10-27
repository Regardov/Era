"""Small explicit faceted builders used only by the Aura Weaver source model."""
import math
import bpy
from mathutils import Vector
from era_character_factory.geometry import mesh_object
from era_character_factory.materials import srgb_to_linear


def material(name, color, roughness=.9, metallic=0):
    color=color.lstrip('#'); rgba=[srgb_to_linear(int(color[i:i+2],16)/255) for i in (0,2,4)]+[1]
    m=bpy.data.materials.new('AW_'+name);m.use_nodes=True;m.diffuse_color=rgba
    bsdf=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    bsdf.inputs['Base Color'].default_value=rgba;bsdf.inputs['Roughness'].default_value=roughness;bsdf.inputs['Metallic'].default_value=metallic
    return m


class Builder:
    def __init__(self, collection, palette, rig):
        self.collection=collection;self.palette=palette;self.rig=rig;self.meshes=[]

    def mesh(self,name,vertices,faces,mat,face_mats=None):
        o=mesh_object(name,vertices,faces,self.collection,self.palette[mat]);self.meshes.append(o)
        if face_mats:
            for key in dict.fromkeys(face_mats):
                if self.palette[key] not in list(o.data.materials):o.data.materials.append(self.palette[key])
            for p,key in zip(o.data.polygons,face_mats):p.material_index=list(o.data.materials).index(self.palette[key])
        o.parent=self.rig
        mod=o.modifiers.new('Aura Weaver skin','ARMATURE');mod.object=self.rig
        return o

    def weight(self,obj,function):
        for group in list(obj.vertex_groups):obj.vertex_groups.remove(group)
        for v in obj.data.vertices:
            w=function(v.co);w={n:a for n,a in w.items() if a>1e-7};total=sum(w.values())
            for name,value in w.items():
                g=obj.vertex_groups.get(name) or obj.vertex_groups.new(name=name);g.add([v.index],value/total,'REPLACE')
        return obj

    def rigid(self,obj,bone):return self.weight(obj,lambda p:{bone:1})

    def loft(self,name,rings,mat,bone=None,sections=None,facets=None,cap=True):
        n=len(rings[0]);v=[p for r in rings for p in r];f=[];m=[]
        for k in range(len(rings)-1):
            for j in range(n):
                a=k*n+j;b=k*n+(j+1)%n;c=(k+1)*n+(j+1)%n;d=(k+1)*n+j
                key=sections[k] if sections else mat
                if facets and (k,j) in facets:
                    f.extend([(a,b,c),(a,c,d)]);m.extend(facets[k,j])
                else:f.append((a,b,c,d));m.append(key)
        if cap:f.extend([tuple(reversed(range(n))),tuple(range((len(rings)-1)*n,len(v)))]);m.extend([mat,mat])
        o=self.mesh(name,v,f,mat,m)
        return self.rigid(o,bone) if bone else o

    def panel(self,name,outline,thickness,mat,bone=None,normal=(0,1,0)):
        p=list(map(Vector,outline));delta=Vector(normal)*thickness;n=len(p)
        o=self.mesh(name,p+[q+delta for q in p],[tuple(reversed(range(n))),tuple(range(n,n*2))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],mat)
        return self.rigid(o,bone) if bone else o

    def folded_panel(self,name,rows,mats,thickness=.009,back=False,normal=None):
        width=len(rows[0]);front=[Vector(p) for r in rows for p in r];n=len(front)
        delta=Vector(normal).normalized()*thickness if normal else Vector((0,-thickness if back else thickness,0));v=front+[p+delta for p in front];f=[];fm=[]
        for k in range(len(rows)-1):
            for j in range(width-1):
                a=k*width+j;b=a+1;c=b+width;d=a+width
                # Fixed diagonals emphasize the large constructed cloth planes.
                for tri in [(a,b,c),(a,c,d)]:f.extend([tri,tuple(i+n for i in reversed(tri))]);fm.extend([mats[k],mats[k]])
        edge=list(range(width))+[k*width+width-1 for k in range(1,len(rows))]+list(range(n-2,n-width-1,-1))+[k*width for k in range(len(rows)-2,0,-1)]
        for a,b in zip(edge,edge[1:]+edge[:1]):f.append((a,b,b+n,a+n));fm.append(mats[-1])
        return self.mesh(name,v,f,mats[0],fm)

    def ribbon(self,name,path,width,mat,bone,depth=.006):
        p=list(map(Vector,path));v=[]
        for i,a in enumerate(p):
            tangent=p[min(i+1,len(p)-1)]-p[max(0,i-1)];side=Vector((-tangent.z,0,tangent.x)).normalized()*width/2;v.extend([a-side,a+side])
        n=len(v);v += [q+Vector((0,depth,0)) for q in v];f=[]
        for i in range(len(p)-1):q=(2*i,2*i+1,2*i+3,2*i+2);f += [q,tuple(j+n for j in reversed(q))]
        edge=[0]+list(range(1,n,2))+list(reversed(range(2,n,2)))
        f += [(a,b,b+n,a+n) for a,b in zip(edge,edge[1:]+edge[:1])]
        return self.rigid(self.mesh(name,v,f,mat),bone)


def octagon(w,front,back,z,cx=0,cy=0):
    return [(cx+u*w,cy+y,z) for u,y in [(-.66,-front),(.66,-front),(1,-front*.5),(1,back*.56),(.64,back),(-.64,back),(-1,back*.56),(-1,-front*.5)]]


def arm_ring(center,axis,radius,depth,sides=8):
    center=Vector(center);axis=Vector(axis).normalized();u=axis.cross(Vector((0,1,0))).normalized();v=axis.cross(u).normalized()
    return [center+u*math.cos(i*math.tau/sides)*radius+v*math.sin(i*math.tau/sides)*depth for i in range(sides)]


def blend_z(p, low, high, lower, upper):
    t=max(0,min(1,(p.z-low)/(high-low)));return {lower:1-t,upper:t}
