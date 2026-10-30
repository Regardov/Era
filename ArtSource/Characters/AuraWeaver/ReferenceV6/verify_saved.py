import bpy,bmesh,json,sys,hashlib,math
from pathlib import Path
folder=Path(__file__).resolve().parent;root=folder.parents[3]
sys.path[:0]=[str(root/'ArtSource/CharacterFactory'),str(folder.parent)]
from era_character_factory.audit import digest,action_payload
bpy.ops.wm.open_mainfile(filepath=str(folder/'AuraWeaver_V6.blend'));r=bpy.data.objects['Archer_Rig'];r.data.pose_position='REST';bpy.context.view_layer.update()
def bounds(o,group=None):
 g=o.vertex_groups.get('PART_'+group) if group else None
 p=[o.matrix_world@v.co for v in o.data.vertices if not group or g and any(x.group==g.index for x in v.groups)]
 return {'min':[min(v[i] for v in p) for i in range(3)],'max':[max(v[i] for v in p) for i in range(3)]}
d={}
for n,g in [('AW_Hat',None),('AW_Belt',None),('AW_ShoulderMantle',None),('AW_Head','AW_HumanFace'),('AW_OuterCoat',None),('AW_Sleeve_L',None),('AW_Sleeve_R',None)]:d[n]=bounds(bpy.data.objects[n],g)
o=bpy.data.objects['AW_HoodAssembly'];bm=bmesh.new();bm.from_mesh(o.data)
d['hood_mesh']={'vertices':len(bm.verts),'faces':len(bm.faces),'boundary_edges':sum(e.is_boundary for e in bm.edges),'nonmanifold_edges':sum(not e.is_manifold for e in bm.edges)};bm.free()
# Intersect side/back rays over the cowl opening region, independently of its shell being manifold.
from mathutils.bvhtree import BVHTree
tree=BVHTree.FromPolygons([v.co for v in o.data.vertices],[list(f.vertices) for f in o.data.polygons])
rays=[]
from mathutils import Vector
for z in [1.60,1.66,1.72,1.78]:
 for origin,direction,label in [((0,1,z),(0,-1,0),'back'),((1,.025,z),(-1,0,0),'sideL'),((-1,.025,z),(1,0,0),'sideR')]:
  hit=tree.ray_cast(Vector(origin),Vector(direction))[0];rays.append({'z_local':z,'direction':label,'hit':hit is not None})
d['hood_rays']=rays
assert all(v['hit'] for v in rays)
assert d['hood_mesh']['boundary_edges']==0 and d['hood_mesh']['nonmanifold_edges']==0
(folder/'measurements.json').write_text(json.dumps(d,indent=2),encoding='utf8')
print(json.dumps(d,indent=1))
