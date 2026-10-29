"""Reference-directed garment reconstruction on the existing V2 character.
Keeps skeleton, Actions, connected hands, face core, legs and source files.
"""
import bpy,bmesh,sys,math,json,hashlib
from pathlib import Path
from mathutils import Vector,Matrix
HERE=Path(__file__).resolve().parent;SOURCE=HERE.parent;ERA=SOURCE.parents[2]
sys.path[:0]=[str(SOURCE),str(ERA/'ArtSource/CharacterFactory')]
from weaver_geometry import Builder,material,octagon,arm_ring,blend_z
from era_character_factory.audit import action_payload,digest
from era_character_factory.qa import mesh_report,evaluated_bounds,surface_contacts
from animate_aura_weaver import set_action

src=SOURCE/'PolishV2/AuraWeaver_Polished.blend'
source_hash=hashlib.sha256(src.read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(src))
scene=bpy.context.scene;rig=bpy.data.objects['Archer_Rig'];rig.data.pose_position='REST'
rest={b.name:[list(r) for r in b.matrix_local] for b in rig.data.bones}
actions={a.name:digest(action_payload(a)) for a in bpy.data.actions}
before_meshes=[o for o in rig.children if o.type=='MESH']
before=evaluated_bounds(before_meshes)
out=HERE/'Previews';out.mkdir(exist_ok=True)
scene.cycles.samples=32
# Neutral, identical comparison conditions; existing studio and cameras reused.
for name in ['Front','Side','Back']:
    cam=bpy.data.objects['Camera_'+name];cam.data.ortho_scale=2.32
    cam.location.z=1.06;cam.rotation_euler=(Vector((0,0,1.06))-cam.location).to_track_quat('-Z','Y').to_euler()
for name in ['Front','Side','Back','Perspective']:
    scene.camera=bpy.data.objects['Camera_'+name]
    scene.render.filepath=str(out/('Before-'+name.lower()+'.png'))
    if not Path(scene.render.filepath).exists():bpy.ops.render.render(write_still=True)

palette={m.name[3:]:m for m in bpy.data.materials if m.name.startswith('AW_')}
if 'sole' not in palette:palette['sole']=material('sole','272725')
from era_character_factory.materials import srgb_to_linear
colors={'ivory':'E0CCA4','sand':'CCAC72','fold':'AB834D','light':'EAD8B6',
 'honey':'BF8C3F','amber':'B2732C','ochre':'94602D','bronze':'A77937',
 'leather':'70452C','leatherEdge':'976135','brown':'493126','charcoal':'232629',
 'inner':'33312D','hair':'222525','hairPlane':'393832','skin':'C89573','skinShade':'A97455'}
for key,col in colors.items():
    m=palette[key];rgba=[srgb_to_linear(int(col[i:i+2],16)/255) for i in (0,2,4)]+[1]
    m.diffuse_color=rgba;next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED').inputs['Base Color'].default_value=rgba

# Preserve the optional focus, but remove it from the character silhouette.
optional=bpy.data.collections.new('OPTIONAL / carried wand V2');scene.collection.children.link(optional)
for name in ['AW_RunicFocus_Independent','AW_FocusHolster']:
    o=bpy.data.objects[name]
    for c in list(o.users_collection):c.objects.unlink(o)
    optional.objects.link(o);o.hide_render=True;o.hide_set(True)
optional.hide_render=True

replace=['AW_HoodAssembly','AW_Scarf','AW_ShoulderMantle','AW_Hat','AW_Sleeve_L','AW_Sleeve_R',
         'AW_OuterCoat','AW_InnerCoat','AW_PolishChevron_L','AW_PolishChevron_R']
for name in replace:bpy.data.objects.remove(bpy.data.objects[name],do_unlink=True)
collection=bpy.data.collections.new('V3 / reconstructed reference garments');scene.collection.children.link(collection)
B=Builder(collection,palette,rig)
groups={}
def remember(label,start):groups.setdefault(label,[]).extend(B.meshes[start:])
def mark():return len(B.meshes)
def head_chest(p):
    t=max(0,min(1,(p.z-1.37)/.20));return {'Chest':1-t,'Head':t}
def diamond(name,c,w,h,mat,bone,back=False):
    x,y,z=c
    return B.panel(name,[(x,y,z+h/2),(x+w/2,y,z),(x,y,z-h/2),(x-w/2,y,z)],.005,mat,bone,normal=(0,-1 if back else 1,0))

# A fitted open shell. Its rear is shallow and folded, not a closed oval bag.
start=mark()
front=[(-.123,-.096,1.338),(-.174,-.065,1.407),(-.164,-.044,1.492),(-.133,-.028,1.615),(-.104,-.016,1.700),
       (.098,-.018,1.698),(.130,-.028,1.622),(.159,-.044,1.505),(.178,-.064,1.418),(.131,-.098,1.337)]
mid=[(-.104,.092,1.346),(-.162,.105,1.416),(-.153,.115,1.486),(-.135,.109,1.606),(-.091,.085,1.694),
     (.087,.082,1.694),(.130,.108,1.610),(.153,.117,1.494),(.163,.105,1.415),(.102,.092,1.344)]
rear=[(-.018,.134,1.312),(-.083,.151,1.379),(-.133,.154,1.454),(-.120,.145,1.578),(-.067,.118,1.667),
      (.063,.117,1.667),(.115,.147,1.581),(.128,.153,1.458),(.081,.148,1.383),(.015,.136,1.307)]
shell=B.rigid(B.loft('V3_HoodShell',[front,mid,rear],'ivory',cap=False,facets={(0,1):('ivory','sand'),(0,7):('sand','fold'),(1,2):('sand','ivory'),(1,6):('fold','sand')}),'Head')
# Inward shell duplicates keep the opening hollow with a turned fabric thickness.
inside=[[(x*.97,y+.006,z) for x,y,z in ring] for ring in [front,mid,rear]]
lining=B.rigid(B.loft('V3_HoodInterior',inside,'fold',cap=False),'Head')
B.rigid(B.loft('V3_HoodTurnedEdge',[front,inside[0]],'honey',cap=False),'Head')
# The concept shows dark rear hair above the collapsed cowl. Open the upper
# rear shell instead of enclosing the whole head in an ivory sack.
for o in [shell,lining]:
    bm=bmesh.new();bm.from_mesh(o.data)
    kill=[f for f in bm.faces if f.calc_center_median().y>.10 and f.calc_center_median().z>1.55]
    bmesh.ops.delete(bm,geom=kill,context='FACES');bm.to_mesh(o.data);bm.free()
rearrows=[[(-.119,.139,1.558),(.006,.153,1.530),(.117,.139,1.558)],
          [(-.151,.153,1.461),(-.016,.176,1.439),(.148,.152,1.463)],
          [(-.109,.147,1.381),(.016,.164,1.344),(.106,.147,1.382)],
          [(-.053,.138,1.336),(0,.145,1.304),(.052,.138,1.336)]]
B.rigid(B.folded_panel('V3_HoodRearFold',rearrows,['ivory','sand','ivory'],.007,True),'Head')
for side,s in [('L',1),('R',-1)]:
    # Opening edge bends inward at the cheek, outward at the gathered shoulder.
    rows=[[(s*.100,-.114,1.687),(s*.128,-.068,1.679),(s*.143,-.027,1.659)],
          [(s*.114,-.133,1.561),(s*.142,-.087,1.544),(s*.163,-.045,1.525)],
          [(s*.131,-.139,1.458),(s*.157,-.111,1.432),(s*.179,-.060,1.423)],
          [(s*.109,-.155,1.354),(s*.149,-.126,1.352),(s*.173,-.090,1.370)],
          [(s*.132,-.135,1.292),(s*.160,-.109,1.311),(s*.171,-.075,1.340)]]
    o=B.folded_panel('V3_HoodLapel_'+side,rows,['ivory','ivory','sand','ivory'],.006);B.weight(o,head_chest)
    o=B.ribbon('V3_HoodAmberFacing_'+side,[(r[0][0],r[0][1]-.003,r[0][2]) for r in rows],.015,'honey','Head',.003);B.weight(o,head_chest)
remember('AW_HoodAssembly',start)

# Crooked cloth crown, a continuous taper around the bend, with no attached spike.
start=mark();n=12
outer=[];hole=[]
for i in range(n):
    a=math.tau*i/n
    outer.append((math.sin(a)*(.318 if math.sin(a)>0 else .329),-math.cos(a)*.294,
                  1.718+.028*math.sin(a)-.035*math.cos(a)+(.012 if i in [3,8] else 0)))
    hole.append((math.sin(a)*.146,-math.cos(a)*.127+.014,1.744))
v=outer+hole+[(x,y,z-.006) for x,y,z in outer]+[(x,y,z-.006) for x,y,z in hole];f=[];fm=[]
for i in range(n):
    j=(i+1)%n;f.extend([(i,j,n+j),(i,n+j,n+i),(2*n+j,2*n+i,3*n+i,3*n+j),(i,2*n+i,2*n+j,j),(n+j,3*n+j,3*n+i,n+i)])
    fm.extend(['light' if i in [0,1,10,11] else 'ivory','sand' if i in [3,7,8] else 'ivory','charcoal','ochre','charcoal'])
B.rigid(B.mesh('V3_HatBrim',v,f,'ivory',fm),'Head')
def hr(x,y,z,rx,ry):return [(x+math.sin(i*math.tau/8)*rx,y-math.cos(i*math.tau/8)*ry,z+.006*math.sin(i*math.tau/8)) for i in range(8)]
B.loft('V3_HatBentCrown',[hr(*r) for r in [(0,.014,1.737,.148,.130),(-.006,.019,1.821,.133,.116),
  (-.021,.030,1.919,.083,.073),(.017,.041,1.978,.039,.038),(.067,.048,1.946,.031,.029),(.120,.061,1.872,.018,.018),(.173,.073,1.795,.002,.002)]],
 'ivory','Head',sections=['ivory','ivory','ivory','sand','fold','charcoal'],facets={(1,3):('sand','fold'),(2,5):('sand','ivory'),(3,2):('fold','sand')})
B.loft('V3_HatLeatherBand',[hr(0,.013,1.749,.151,.135),hr(-.005,.020,1.803,.141,.128)],'leather','Head',facets={(0,0):('amber','leatherEdge'),(0,7):('leatherEdge','leather')})
# Actual open diamond buckle, not a flat gold sticker.
c=Vector((.057,-.119,1.778));outerd=[c+Vector((0,0,.030)),c+Vector((.026,0,0)),c+Vector((0,0,-.030)),c+Vector((-.026,0,0))]
innerd=[c+(p-c)*.52 for p in outerd]
v=outerd+innerd+[p+Vector((0,.007,0)) for p in outerd+innerd];f=[]
for i in range(4):
 j=(i+1)%4;f.extend([(i,j,4+j,4+i),(8+i,12+i,12+j,8+j),(i,8+i,8+j,j),(4+i,4+j,12+j,12+i)])
B.rigid(B.mesh('V3_HatOpenBuckle',v,f,'bronze'),'Head');remember('AW_Hat',start)

# Layered draped chest wraps: slanting folds connect neck and chest.
start=mark()
wraps=[('Under',[(-.114,-.093,1.431),(.108,-.094,1.430),(.148,-.113,1.347),(.091,-.141,1.245),(-.010,-.153,1.181),(-.134,-.121,1.283)],'charcoal'),
 ('Upper',[(-.131,-.124,1.383),(-.024,-.158,1.348),(.120,-.134,1.413),(.133,-.146,1.361),(.014,-.177,1.288),(-.113,-.146,1.334)],'brown'),
 ('Cross',[(-.122,-.148,1.330),(-.008,-.181,1.286),(.128,-.150,1.353),(.105,-.164,1.299),(-.004,-.183,1.235),(-.111,-.152,1.287)],'leather'),
 ('Lower',[(-.112,-.152,1.287),(-.005,-.185,1.236),(.103,-.161,1.296),(.076,-.165,1.237),(-.009,-.178,1.199),(-.097,-.153,1.254)],'brown')]
for name,outline,mat in wraps:B.panel('V3_Wrap'+name,outline,.008,mat,'Chest')
remember('AW_Scarf',start)

# Capelet, cloth shoulder lobes and leather retaining straps.
start=mark()
for side,s in [('L',1),('R',-1)]:
    rows=[[(s*.111,-.098,1.363),(s*.172,-.079,1.378),(s*.218,-.022,1.344)],
          [(s*.135,-.124,1.285),(s*.219,-.119,1.279),(s*.267,-.041,1.269)],
          [(s*.143,-.132,1.210),(s*.228,-.130,1.196),(s*.280,-.052,1.213)],
          [(s*.146,-.133,1.184),(s*.234,-.133,1.170),(s*.284,-.055,1.186)]]
    o=B.folded_panel('V3_ShoulderDrape_'+side,rows,['ivory','sand','amber'],.009)
    B.weight(o,lambda p,side=side:{'Chest':.32,'UpperArm.'+side:.68})
    # Lobe wraps around rear upper arm, no giant block-shaped armor.
    backrows=[[(s*.110,.082,1.365),(s*.180,.099,1.358),(s*.219,.071,1.336)],
              [(s*.144,.105,1.258),(s*.219,.116,1.256),(s*.267,.066,1.258)],
              [(s*.146,.108,1.181),(s*.234,.122,1.173),(s*.282,.070,1.187)]]
    o=B.folded_panel('V3_ShoulderBack_'+side,backrows,['ivory','amber'],.009,True);B.weight(o,lambda p,side=side:{'Chest':.32,'UpperArm.'+side:.68})
    B.ribbon('V3_ShoulderStrap_'+side,[(s*.105,-.117,1.349),(s*.177,-.144,1.322),(s*.251,-.126,1.298)],.024,'leather','Chest',.006)
    diamond('V3_ShoulderClasp_'+side,(s*.164,-.149,1.326),.029,.038,'bronze','Chest')
    o=B.ribbon('V3_ShoulderChevron_'+side,[(s*.151,-.143,1.275),(s*.208,-.151,1.238),(s*.253,-.131,1.266)],.012,'amber','Chest',.003)
    B.weight(o,lambda p,side=side:{'Chest':.32,'UpperArm.'+side:.68})
backrows=[[(-.112,.124,1.393),(0,.160,1.358),(.109,.124,1.396)],
          [(-.230,.116,1.310),(0,.191,1.266),(.230,.116,1.317)],
          [(-.236,.124,1.226),(0,.186,1.129),(.236,.124,1.231)],
          [(-.238,.126,1.202),(0,.189,1.100),(.238,.126,1.208)]]
B.rigid(B.folded_panel('V3_BackMantle',backrows,['ivory','ivory','honey'],.010,True),'Chest')
B.panel('V3_BackCollar',[(-.120,.127,1.431),(0,.165,1.411),(.116,.128,1.429),(.147,.148,1.380),(0,.198,1.286),(-.146,.146,1.379)],.009,'light','Chest',normal=(0,-1,0))
diamond('V3_BackMantleDiamond',(0,.201,1.237),.045,.065,'honey','Chest',True)
remember('AW_ShoulderMantle',start)

# Bespoke octagonal bell sleeves: tapered upper arm, dropped angular cuffs,
# inward return and dark lining with a real open wrist aperture.
for side,s in [('L',1),('R',-1)]:
    start=mark();upper=rig.data.bones['UpperArm.'+side];fore=rig.data.bones['Forearm.'+side]
    a=upper.head_local;elbow=fore.head_local;wrist=fore.tail_local;axis=(wrist-elbow).normalized()
    centers=[a,a.lerp(elbow,.68),elbow,elbow.lerp(wrist,.52),wrist+axis*.018,wrist+axis*.045]
    rings=[]
    for k,(c,rx,ry) in enumerate(zip(centers,[.069,.077,.081,.103,.126,.129],[.069,.074,.077,.102,.122,.123])):
        ring=arm_ring(c,axis,rx,ry)
        if k>=3:
            for j,p in enumerate(ring):
                # A long outer cuff point gives the heavy hanging-cloth silhouette.
                outer=max(0,(s*p.x-s*c.x)/rx);p.z-=outer*(.015 if k==3 else .044)
                if j in [1,5]:p.y-=.008
        rings.append(ring)
    sleeve=B.loft('V3_BellSleeve_'+side,rings,'ivory',sections=['ivory','ivory','ivory','honey','brown'],cap=False,
                  facets={(2,1):('light','ivory'),(3,5):('amber','honey')})
    def weights(p,side=side,elbow=elbow,axis=axis):
        t=max(0,min(1,((p-elbow).dot(axis)+.056)/.105));return {'UpperArm.'+side:1-t,'Forearm.'+side:t}
    B.weight(sleeve,weights)
    inner=[wrist+axis*.045+(p-(wrist+axis*.045))*.89 for p in rings[-1]]
    cuff=B.loft('V3_CuffReturn_'+side,[rings[-1],inner,arm_ring(wrist-axis*.075,axis,.083,.079)],'brown','Forearm.'+side,sections=['brown','charcoal'],cap=False)
    remember('AW_Sleeve_'+side,start)

# Coherent long coat: folds flow from belt to hem, colors follow horizontal hems
# and axial pleats, not arbitrarily recolored triangles.
start=mark()
def coatweight(p,label):
    if p.z>=.965:return {'Pelvis':1}
    if p.z>.89:
        t=(p.z-.89)/.075;return {'Pelvis':t,'Coat1.'+label:1-t}
    return blend_z(p,.32,.78,'Coat2.'+label,'Coat1.'+label)
for label in ['Front.L','Front.R','Back.L','Back.R','Side.L','Side.R']:
    s=1 if label.endswith('L') else -1;back=label.startswith('Back');side=label.startswith('Side')
    rows=[]
    for k,(z,outerw,depth) in enumerate([(1.026,.128,.132),(.83,.161,.155),(.60,.195,.175),(.36,.238,.190),(.25,.254,.199),(.175,.266,.203),(.125,.272,.204)]):
        if side:
            # Side gussets overlap outer panels; central ridge is modest.
            rows.append([(s*outerw,-depth*.92,z+.010),(s*(outerw+.025),.014,z+.020),(s*outerw,depth*.92,z+.014)])
        else:
            inner=(.024+.045*(1-z)) if not back else -.008;cy=depth*(1 if back else -1)
            if not back:inner+=(.006 if s==1 else .012)*(1-z)
            offset=(.018 if s==1 else 0)*(1-z) if not back else (.008 if s==-1 else 0)*(1-z)
            rows.append([(s*inner,cy,z+offset),(s*(inner+outerw)*.52,cy+(.020 if back else -.020),z+.012+offset),(s*outerw,cy*.90,z+.015)])
    o=B.folded_panel('V3_Coat_'+label,rows,['ivory','ivory','sand','honey','amber','brown'],.008,back,normal=(s,0,0) if side else None)
    B.weight(o,lambda p,label=label:coatweight(p,label))
    if not side:
        o=B.ribbon('V3_CoatOpeningFacing_'+label,[(r[0][0],r[0][1]+(.004 if back else -.004),r[0][2]) for r in rows],.016,'honey','Pelvis',.003)
        B.weight(o,lambda p,label=label:coatweight(p,label))
    # Narrow tonal ridge follows a real fold and ends above the colored hem.
    if not side:
        path=[(r[1][0],r[1][1]+(.003 if back else -.003),r[1][2]) for r in rows[:4]]
        o=B.ribbon('V3_CoatPleat_'+label,path,.015,'light','Pelvis',.002);B.weight(o,lambda p,label=label:coatweight(p,label))
remember('AW_OuterCoat',start)
start=mark()
for back in [False,True]:
    rows=[]
    for z,w,d in [(1.02,.126,.125),(.80,.153,.145),(.55,.189,.163),(.30,.226,.177),(.115,.253,.187)]:
        y=d*(1 if back else -1);rows.append([(-w,y,z),(0,y+(.009 if back else -.009),z), (w,y,z)])
    o=B.folded_panel('V3_DarkInnerRobe_'+str(back),rows,['charcoal']*4,.006,back)
    B.weight(o,lambda p,back=back:coatweight(p,'Back.L' if back else 'Front.L'))
rows=[[(-.019,-.151,1.014),(0,-.157,1.014),(.019,-.151,1.014)],
      [(-.026,-.175,.75),(0,-.183,.75),(.026,-.175,.75)],
      [(-.031,-.194,.43),(0,-.203,.43),(.031,-.194,.43)],
      [(-.025,-.210,.145),(0,-.216,.115),(.025,-.210,.145)]]
o=B.folded_panel('V3_CentralStole',rows,['honey','honey','amber'],.006);B.weight(o,lambda p:coatweight(p,'Front.L'))
remember('AW_InnerCoat',start)

# Keep the head core, reposition features onto its existing planes; no metal mask.
def part(obj,name):
    g=obj.vertex_groups.get('PART_'+name)
    return [v for v in obj.data.vertices if g and any(i.group==g.index and i.weight>.5 for i in v.groups)]
head=bpy.data.objects['AW_Head']
for v in part(head,'AW_HumanFace'):
    if v.co.z<1.49:v.co.x*=.96
for v in part(head,'AW_Nose'):
    if v.co.y<-.12:v.co.y=-.12+(v.co.y+.12)*.78
for v in part(head,'AW_Mouth'):v.co.z=1.493;v.co.y-=.001
for s in [-1,1]:
    for v in part(head,'AW_Eye_'+str(s)):
        v.co.z=1.559+(v.co.z-1.561)*.70
hair=bpy.data.objects['AW_Hair']
for v in part(hair,'AW_HairScalp'):
    # Fill the previously hidden crown now visible through the lowered rear cowl.
    if v.co.z>1.610:v.co.z+=.070*min(1,(v.co.z-1.610)/.040)
for v in part(hair,'AW_FrontLock_0'):
    if v.co.z<1.5:v.co.x-=.006;v.co.z-=.014
for v in part(hair,'AW_FrontLock_2'):
    if v.co.z<1.57:v.co.x-=.008;v.co.z-=.023

# Existing belt/pouches keep their skins, with richer fittings and a deeper side case.
belt=bpy.data.objects['AW_Belt']
for v in belt.data.vertices:
    v.co.x*=.94;v.co.z=1.013+(v.co.z-1.013)*1.12
for side,s in [('L',1),('R',-1)]:
    p=bpy.data.objects['AW_PouchAssembly_'+side]
    for v in p.data.vertices:
        if abs(v.co.x)<.14 and v.co.z>.95:v.co.x*=.94
        if side=='L' and v.co.z<.92:v.co.z=.90+(v.co.z-.90)*1.10;v.co.y-=.012
    start=mark()
    x=s*(.182 if side=='L' else .174);cy=-.176 if side=='L' else -.183;z=.758 if side=='L' else .832
    for dx in [-.042,.042]:
        B.ribbon('V3_PouchRetainingBand_'+side+str(dx),[(x+dx,cy,z+.076),(x+dx,cy-.007,z-.070)],.012,'leatherEdge','Pelvis',.004)
    B.ribbon('V3_CaseLowerBand_'+side,[(x-.051,cy-.004,z-.071),(x+.051,cy-.004,z-.071)],.013,'leatherEdge','Pelvis',.004)
    diamond('V3_CaseClasp_'+side,(x,cy-.013,z+.029),.023,.032,'bronze','Pelvis')
    remember('AW_PouchFittings_'+side,start)

# Existing boot topology refined with a tapered toe and a thin welt, no boxes.
start=mark()
for side,s in [('L',1),('R',-1)]:
    boot=bpy.data.objects['AW_Boot_'+side]
    for v in boot.data.vertices:
        if v.co.z<.12:
            center=s*.158
            if v.co.y<-.10:v.co.x=center+(v.co.x-center)*.89;v.co.y-=.013*(min(1,(-v.co.y-.10)/.09))
    # Assign graphite shaft, brown leather vamp, charcoal sole by anatomical surface.
    for m in [palette['charcoal'],palette['brown'],palette['sole'],palette['leatherEdge']]:
        if m not in list(boot.data.materials):boot.data.materials.append(m)
    for f in boot.data.polygons:
        z=sum(boot.data.vertices[i].co.z for i in f.vertices)/len(f.vertices)
        mat=palette['sole'] if z<.024 else palette['brown'] if z<.12 else palette['charcoal']
        f.material_index=list(boot.data.materials).index(mat)
    # Keep the existing integrated ankle topology; a generic added octagon
    # projects beyond the shaped foot and would look like a floating bar.

# Increase stature through a slightly longer upper torso/neck and crown only.
# Original legs, rest bones and all existing Action curves remain untouched.
for o in [o for o in rig.children if o.type=='MESH' and o.name not in ['AW_RunicFocus_Independent','AW_FocusHolster']]:
    for v in o.data.vertices:
        z=v.co.z
        if z>.30:
            shift=.015*max(0,min(1,(z-.30)/.66))
            shift+=.012*max(0,min(1,(z-1.05)/.40))
            v.co.z+=shift

# Consolidate only coherent garments, keeping islands/skins/materials intact.
joined=[]
for name,objs in groups.items():
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        g=o.vertex_groups.new(name='PART_'+o.name);g.add(list(range(len(o.data.vertices))),1,'REPLACE');o.select_set(True)
    bpy.context.view_layer.objects.active=objs[0]
    if len(objs)>1:bpy.ops.object.join()
    objs[0].name=name;joined.append({'name':name,'sources':len(objs),'welded':False})

meshes=[o for o in rig.children if o.type=='MESH' and o.name not in ['AW_RunicFocus_Independent','AW_FocusHolster']]
for o in meshes:
    bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()
    for p in o.data.polygons:p.use_smooth=False
qa=mesh_report(rig,meshes)
assert all(m['finite'] and not m['zero_area_faces'] and not m['subdivision'] for m in qa['meshes'])
assert all(not w['unweighted'] and not w['non_normalized'] and w['max_influences']<=4 for w in qa['weights'])
assert actions=={a.name:digest(action_payload(a)) for a in bpy.data.actions}
assert rest=={b.name:[list(r) for r in b.matrix_local] for b in rig.data.bones}
bpy.context.view_layer.update();after=evaluated_bounds(meshes)

# Save neutral editable asset with optional wand and demonstration collection hidden.
rig.data.pose_position='REST';scene.camera=bpy.data.objects['Camera_Perspective'];scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'AuraWeaver_Reconstructed.blend'),compress=True)
for name in ['Front','Side','Back','Perspective']:
    scene.camera=bpy.data.objects['Camera_'+name];scene.render.filepath=str(out/('AuraWeaver-'+name.lower()+'.png'));bpy.ops.render.render(write_still=True)

# Sample every retained Action; capture finite evaluated geometry and selected
# sleeve/body contact pairs. This is a diagnostic, not a no-clipping certificate.
rig.data.pose_position='POSE';samples=[]
pairs=[('AW_Sleeve_L','AW_Bodice'),('AW_Sleeve_R','AW_Bodice'),('AW_Sleeve_L','AW_OuterCoat'),('AW_Sleeve_R','AW_OuterCoat')]
for action in list(bpy.data.actions):
    set_action(rig,action);lo,hi=action.frame_range
    for t in [0,.5,1]:
        scene.frame_set(round(lo+(hi-lo)*t));bpy.context.view_layer.update()
        bounds=evaluated_bounds(meshes);assert all(math.isfinite(v) and abs(v)<8 for v in bounds['min']+bounds['max'])
        samples.append({'action':action.name,'frame':scene.frame_current,'bounds':bounds,'contacts':surface_contacts(meshes,pairs,max_pairs=2)})
    if action.name in ['AuraWeaver_Run','AuraWeaver_AuraChannel','AuraWeaver_Cast']:
        scene.frame_set(round(lo+(hi-lo)*.32));scene.camera=bpy.data.objects['Camera_Perspective']
        scene.render.filepath=str(out/('Pose-'+action.name+'.png'));bpy.ops.render.render(write_still=True)

# Transient extended-arm skin test, without adding or editing an Action.
rig.animation_data.action=None
for p in rig.pose.bones:p.matrix_basis=Matrix.Identity(4)
bpy.context.view_layer.update()
from animate_aura_weaver import solve_arm
solve_arm(rig,'L',(.535,-.045,1.30),(.32,-.12,1.20))
solve_arm(rig,'R',(-.535,-.045,1.30),(-.32,-.12,1.20))
for side in ['L','R']:
    hand=rig.pose.bones['Hand.'+side];fore=rig.pose.bones['Forearm.'+side]
    direction=(fore.tail-fore.head).normalized()
    q=(hand.bone.tail_local-hand.bone.head_local).rotation_difference(direction)@hand.bone.matrix_local.to_quaternion()
    hand.matrix=Matrix.LocRotScale(hand.head.copy(),q,Vector((1,1,1)))
bpy.context.view_layer.update()
samples.append({'action':'TransientExtendedArms_NoActionSaved','frame':0,'bounds':evaluated_bounds(meshes),
                'contacts':surface_contacts(meshes,pairs,max_pairs=2)})
scene.camera=bpy.data.objects['Camera_Perspective'];scene.render.filepath=str(out/'Pose-ExtendedArms.png');bpy.ops.render.render(write_still=True)
# Demo only: the existing two-hand channel Action, no changes to its curves.
set_action(rig,bpy.data.actions['AuraWeaver_AuraChannel']);scene.frame_set(22);bpy.context.view_layer.update()
demo=bpy.data.collections.new('DEMO ONLY / rune between hands');scene.collection.children.link(demo)
dm=material('DemoAmber','FFC067',.5)
node=next(n for n in dm.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
node.inputs['Emission Color'].default_value=node.inputs['Base Color'].default_value
node.inputs['Emission Strength'].default_value=2.0
def rune_path(name,points,r=.0024):
    verts=[];faces=[]
    for p in points:
        verts.extend([Vector(p)+Vector((math.cos(i*math.tau/4)*r,math.sin(i*math.tau/4)*r,0)) for i in range(4)])
    # Curve bevel gives consistent tiny, faceted bars even for vertical segments.
    data=bpy.data.curves.new(name,'CURVE');data.dimensions='3D';data.resolution_u=1;data.bevel_depth=r;data.bevel_resolution=0
    spline=data.splines.new('POLY');spline.points.add(len(points)-1)
    for a,p in zip(spline.points,points):a.co=(*p,1)
    o=bpy.data.objects.new(name,data);demo.objects.link(o);data.materials.append(dm)
    return o
# Average transformed wrists, then bias toward finger region, between palms.
left=rig.matrix_world@rig.pose.bones['Hand.L'].head;right=rig.matrix_world@rig.pose.bones['Hand.R'].head
c=(left+right)/2+Vector((0,-.018,.015))
def rp(x,z):return (c.x+x,c.y,c.z+z)
rune_path('Demo central diamond',[rp(0,.085),rp(.058,0),rp(0,-.085),rp(-.058,0),rp(0,.085)])
rune_path('Demo inner diamond',[rp(0,.040),rp(.025,0),rp(0,-.040),rp(-.025,0),rp(0,.040)],.0018)
for k in range(4):
    points=[rp(math.cos(a)*.108,math.sin(a)*.108) for a in [k*math.pi/2+.16+j*.13 for j in range(10)]]
    rune_path('Demo arc '+str(k),points,.0016)
for s in [-1,1]:
    for x,z,w,h in [(s*.122,.043,.010,.020),(s*.078,.120,.008,.016),(0,s*.134,.009,.018)]:
        rune_path('Demo floating diamond',[rp(x,z+h),rp(x+w,z),rp(x,z-h),rp(x-w,z),rp(x,z+h)],.0018)
scene.camera=bpy.data.objects['Camera_Perspective'];scene.render.filepath=str(out/'AuraWeaver-MagicDemo.png');bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'AuraWeaver_MagicDemo.blend'),compress=True)
report={'source':str(src),'source_sha256':source_hash,'source_unchanged':source_hash==hashlib.sha256(src.read_bytes()).hexdigest(),
 'rest_bones_unchanged':True,'original_actions_unchanged':True,'bone_count':len(rest),'actions':list(actions),
 'bounds_before':before,'bounds_after':after,'body_mesh_count_before':len(before_meshes),'body_mesh_count_after':len(meshes),
 'triangles':sum(len(p.vertices)-2 for o in meshes for p in o.data.polygons),'groups':joined,'qa':qa,'pose_samples':samples,
 'colors':colors,'height_method':'Legs unchanged; belt/upper robe +15mm, upper torso/neck additional 12mm, rebuilt crown. Existing uniform delta scale retained, no new whole-body scaling.',
 'remaining':['Illustration has perspective and stylized cloth; exact hidden geometry is inferred, not measured.',
 'Sampled Actions are not a full animation/gameplay validation; contact counts include intentional proximal garment overlaps.',
 'Unity assets and release not modified. No new FBX required for this art-only review.']}
(HERE/'validation.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('RECONSTRUCTION_OK',json.dumps({'meshes':len(meshes),'triangles':report['triangles'],'before':before,'after':after}))
