"""Ten deterministic, bilateral fabric paint layouts from a sampled reference.
The atlas is a color-family guide; it is not a texture or sunlight baked into albedo.
"""
import bpy,json,math
from pathlib import Path
from mathutils import Vector
from weaver_geometry import material

CONFIGS=[
 {'id':1,'label':'Broad planes','nx':6,'ny':12,'du':0,'dt':0,'skew':0,'tone':0},
 {'id':2,'label':'Medium staggered','nx':8,'ny':16,'du':-.025,'dt':.010,'skew':0,'tone':0},
 {'id':3,'label':'Narrow amber facets','nx':10,'ny':18,'du':.025,'dt':-.020,'skew':0,'tone':0},
 {'id':4,'label':'Diagonal cloth fields','nx':10,'ny':20,'du':0,'dt':.025,'skew':.09,'tone':0},
 {'id':5,'label':'Reference balanced','nx':12,'ny':24,'du':0,'dt':0,'skew':0,'tone':0},
 {'id':6,'label':'Tall fine fields','nx':14,'ny':26,'du':0,'dt':-.012,'skew':0,'tone':0},
 {'id':7,'label':'Crossed wedges','nx':8,'ny':22,'du':0,'dt':-.020,'skew':-.06,'tone':0},
 {'id':8,'label':'Deep ochre layers','nx':12,'ny':20,'du':-.018,'dt':0,'skew':0,'tone':1},
 {'id':9,'label':'Dense reference facets','nx':16,'ny':30,'du':.012,'dt':.008,'skew':0,'tone':0},
 {'id':10,'label':'Soft sand facets','nx':12,'ny':26,'du':-.010,'dt':-.005,'skew':0,'tone':-1}]
HEX=['F1DEB7','E7D2A6','DCC193','E2BA82','D2AE77','D7A047','C68F41','D9953C','BF7C29','885029','67412C','4D3027','3B2925']

def clamp(x,a=0,b=1):return max(a,min(b,x))
class Patterns:
    def __init__(self,folder):
        self.atlas=json.loads((Path(folder)/'reference_atlas.json').read_text(encoding='utf8'))
        self.palette=[]
        for i,color in enumerate(HEX):
            self.palette.append(bpy.data.materials.get('AW_Pixel'+str(i).zfill(2)) or material('Pixel'+str(i).zfill(2),color))
        self.records={};self._record()
    def at(self,region,u,t):
        x=int(clamp(u)*self.atlas['width']);y=int((1-clamp(t))*self.atlas['height'])
        return self.atlas['regions'][region][min(self.atlas['height']-1,y)][min(self.atlas['width']-1,x)]
    def query(self,region,u,t,cfg):
        u=clamp(u+cfg['du']+cfg['skew']*(t-.5));t=clamp(t+cfg['dt'])
        u=(math.floor(u*cfg['nx'])+.5)/cfg['nx'];t=(math.floor(t*cfg['ny'])+.5)/cfg['ny']
        role=self.at(region,u,t)
        if cfg['tone'] and role not in [0,12]:role=int(clamp(role+cfg['tone'],0,12))
        return role
    def _record(self):
        names=['AW_OuterCoat','AW_ShoulderMantle','AW_HoodAssembly','AW_Sleeve_L']
        sleeve=bpy.data.objects['AW_Sleeve_L'];g=sleeve.vertex_groups['PART_V3_BellSleeve_L']
        bell=[v for v in sleeve.data.vertices if any(w.group==g.index and w.weight>.99 for w in v.groups)]
        path=[sum((v.co for v in bell[k*8:k*8+8]),Vector())/8 for k in range(6)]
        for name in names:
            o=bpy.data.objects[name];rows=[]
            for f in o.data.polygons:
                p=sum((o.data.vertices[i].co for i in f.vertices),Vector())/len(f.vertices);key=o.data.materials[f.material_index].name
                r={'original':key,'region':None,'u':0,'t':0,'fixed':None}
                radial=Vector((p.x,p.y,0));outer=f.normal.dot(radial)>0
                if name=='AW_OuterCoat' and outer:
                    t=clamp((p.z-.18)/1.05);r.update(region='front' if p.y<0 else 'back',u=clamp(abs(p.x)/(.110+.205*(1-t))),t=t)
                elif name=='AW_Sleeve_L':
                    best=None
                    for k in range(5):
                        a,b=path[k:k+2];t=clamp((p-a).dot(b-a)/(b-a).length_squared);center=a.lerp(b,t);v=(p-center).length_squared
                        if best is None or v<best[0]:best=(v,k,t,center)
                    _,k,t,center=best;d=(path[k+1]-path[k]).normalized();u=Vector((1,0,0));u=(u-d*u.dot(d)).normalized();offset=p-center
                    cuff=o.vertex_groups.get('PART_V3_CuffReturn_L');lining=all(any(w.group==cuff.index and w.weight>.99 for w in o.data.vertices[i].groups) for i in f.vertices) if cuff else False
                    if f.normal.dot(offset)>0 and not lining:
                        radius=max(.03,max(abs((v.co-center).dot(u)) for v in bell[max(0,k*8):min(48,(k+2)*8)]))
                        # Z fitting is stable across the inherited elbow cuts;
                        # the reference forearm bands rise from the cuff to elbow.
                        r.update(region='sleeve',u=clamp(.5+.5*offset.dot(u)/radius),t=clamp((p.z-.95)/.45))
                elif name in ['AW_HoodAssembly','AW_ShoulderMantle'] and (outer or f.normal.z>.45):
                    if key in ['AW_honey','AW_amber']:r['fixed']=5 if key=='AW_honey' else 8
                    elif key in ['AW_ivory','AW_light','AW_sand']:
                        r.update(region='cream',u=clamp(abs(p.x)/.30),t=clamp((p.z-1.24)/.60))
                rows.append(r)
            self.records[name]=rows
    def apply(self,cfg):
        for name,rows in self.records.items():
            o=bpy.data.objects[name]
            for m in self.palette:
                if m not in list(o.data.materials):o.data.materials.append(m)
            for f,r in zip(o.data.polygons,rows):
                if r['fixed'] is not None:role=r['fixed']
                elif r['region']=='cream':
                    role=self.query('back',r['u'],.70+.28*r['t'],cfg)
                    role=min(role,4)
                elif r['region']:role=self.query(r['region'],r['u'],r['t'],cfg)
                else:
                    f.material_index=list(o.data.materials).index(bpy.data.materials[r['original']]);continue
                f.material_index=list(o.data.materials).index(self.palette[role])
        # Each sleeve has identical topology in its own fitted arm pose.
        a=bpy.data.objects['AW_Sleeve_L'];b=bpy.data.objects['AW_Sleeve_R'];assert len(a.data.polygons)==len(b.data.polygons)
        for ma in a.data.materials:
            if ma not in list(b.data.materials):b.data.materials.append(ma)
        for fa,fb in zip(a.data.polygons,b.data.polygons):fb.material_index=list(b.data.materials).index(a.data.materials[fa.material_index])
        # The dorsal leather glove panels are also paired, without changing hands.
        a=bpy.data.objects['AW_ConnectedGlove_L'];b=bpy.data.objects['AW_ConnectedGlove_R']
        for ma in a.data.materials:
            if ma not in list(b.data.materials):b.data.materials.append(ma)
        for fa,fb in zip(a.data.polygons,b.data.polygons):fb.material_index=list(b.data.materials).index(a.data.materials[fa.material_index])
        bpy.data.objects['Archer_Rig']['ERA_pattern_variant']=cfg['id']
    def score(self,cfg):
        # Family-layout error over an unlit normalized atlas. This is NOT a
        # percentage of overall character similarity or a rendered-light score.
        palette=[tuple(int(h[i:i+2],16)/255 for i in (0,2,4)) for h in self.atlas['quantization_srgb']]
        sums={}
        for region in ['front','back','sleeve']:
            total=0
            for j in range(48):
                t=(j+.5)/48
                for i in range(32):
                    u=(i+.5)/32;a=palette[self.at(region,u,t)];b=palette[self.query(region,u,t,cfg)]
                    total+=math.sqrt(sum((a[k]-b[k])**2 for k in range(3))/3)
            sums[region]=total/(48*32)
        return {'mean_family_layout_error':(sums['front']+sums['back']+.45*sums['sleeve'])/2.45,'regions':sums}

def symmetry_report(names):
    from collections import Counter
    result=[]
    for name in names:
        o=bpy.data.objects[name];positive=Counter();negative=Counter()
        for f in o.data.polygons:
            c=sum(o.data.vertices[i].co.x for i in f.vertices)/len(f.vertices)
            if abs(c)<1e-7:continue
            coords=tuple(sorted((round(abs(o.data.vertices[i].co.x),5),round(o.data.vertices[i].co.y,5),round(o.data.vertices[i].co.z,5)) for i in f.vertices))
            (positive if c>0 else negative)[(coords,o.data.materials[f.material_index].name)]+=1
        result.append({'object':name,'unmatched_painted_faces':sum((positive-negative).values())+sum((negative-positive).values())})
    left=bpy.data.objects['AW_Sleeve_L'];right=bpy.data.objects['AW_Sleeve_R']
    result.append({'object':'Sleeve pair, local garment coordinates','unmatched_painted_faces':sum(left.data.materials[a.material_index]!=right.data.materials[b.material_index] for a,b in zip(left.data.polygons,right.data.polygons))})
    return result
