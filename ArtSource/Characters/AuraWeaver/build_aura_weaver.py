"""Explicit reference-led Aura Weaver model. Never writes the archer or game prefabs."""
import bpy, math, sys, json, hashlib, argparse
from pathlib import Path
from mathutils import Vector, Matrix

HERE=Path(__file__).resolve().parent;ERA=HERE.parents[2];FACTORY=ERA/'ArtSource/CharacterFactory'
sys.path[:0]=[str(HERE),str(FACTORY)]
from era_character_factory.components import append_template
from era_character_factory.rig import load_actions
from era_character_factory.audit import skeleton_payload
from weaver_geometry import Builder,material,octagon,arm_ring,blend_z

args=argparse.ArgumentParser();args.add_argument('--blockout',action='store_true');args.add_argument('--no-render',action='store_true')
args=args.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.render.fps=30
collection,rig,old=append_template(FACTORY/'ERA_CharacterLibrary.blend');collection.name='AURA WEAVER / editable character'
rig.animation_data_clear();rig.name='Archer_Rig';rig.data.name='ERA_Humanoid54_AuraWeaver_Extended'
for p in rig.pose.bones:p.matrix_basis=Matrix.Identity(4)
original={b.name:tuple(tuple(row) for row in b.matrix_local) for b in rig.data.bones}
keep=[o for o in old if o.name.startswith(('CF_Legs_Trousers_','CF_Boots_'))]
for o in old:
    if o not in keep:bpy.data.objects.remove(o,do_unlink=True)
palette={
 'ivory':'D8CEAF','sand':'C4B38D','fold':'B29C73','light':'E6DCC3',
 'honey':'C5A063','amber':'B57D3F','ochre':'967044','bronze':'A47B48',
 'leather':'6F4D34','leatherEdge':'8D6540','brown':'4C352A','charcoal':'24272A','inner':'30302C',
 'hair':'292927','hairPlane':'3B3731','skin':'D1A080','skinShade':'B68062','eye':'362C27','sole':'272725'}
M={n:material(n,c,.9,.12 if n=='bronze' else 0) for n,c in palette.items()};B=Builder(collection,M,rig)
for o in keep:
    isboot='Boots' in o.name;o.name='AW_Boot_'+o.name[-1] if isboot else 'AW_Leg_'+o.name[-1]
    o.data.materials.clear();o.data.materials.append(M['brown' if isboot else 'charcoal'])
    for p in o.data.polygons:p.material_index=0;p.use_smooth=False
    if isboot:
        center=sum((v.co for v in o.data.vertices),Vector())/len(o.data.vertices)
        for v in o.data.vertices:v.co.x=center.x+(v.co.x-center.x)*1.13;v.co.y=center.y+(v.co.y-center.y)*1.08
    B.meshes.append(o)

# Body volumes are deliberately independent of the elf head and costume.
body=B.loft('AW_Bodice', [octagon(.131,.098,.106,.91),octagon(.116,.091,.100,1.03),octagon(.162,.105,.111,1.22),octagon(.171,.091,.103,1.30),octagon(.08,.065,.065,1.35)],'inner',sections=['inner','charcoal','inner','charcoal'])
B.weight(body,lambda p:blend_z(p,1.00,1.23,'Pelvis','Chest'))
B.rigid(B.loft('AW_Neck',[octagon(.055,.052,.049,1.32),octagon(.057,.054,.05,1.47)],'skin'),'Neck')
head=B.loft('AW_HumanFace',[
 octagon(.027,.094,.010,1.446,cy=-.010),octagon(.061,.103,.047,1.475,cy=-.010),
 octagon(.089,.106,.079,1.535,cy=-.006),octagon(.096,.102,.089,1.595,cy=.001),
 octagon(.086,.088,.087,1.659,cy=.005),octagon(.056,.055,.056,1.694,cy=.008)],'skin','Head',facets={(1,2):('skin','skinShade'),(1,6):('skinShade','skin')})
for s in [-1,1]:
    B.rigid(B.loft('AW_HumanEar_'+str(s),[octagon(.012,.016,.013,1.526,cx=s*.09,cy=.007),octagon(.014,.017,.015,1.567,cx=s*.092,cy=.008)],'skinShade'),'Head')
if not args.blockout:
    B.panel('AW_Nose',[(-.012,-.108,1.572),(.011,-.108,1.572),(.014,-.125,1.533),(0,-.139,1.531),(-.014,-.124,1.532)],.006,'skin','Head')
    for s in [-1,1]:
        B.panel('AW_Eye_'+str(s),[(s*.018,-.111,1.562),(s*.062,-.108,1.566),(s*.058,-.111,1.557),(s*.024,-.114,1.555)],.0015,'eye','Head')
        B.ribbon('AW_Brow_'+str(s),[(s*.018,-.110,1.579),(s*.048,-.108,1.581),(s*.066,-.102,1.576)],.007,'hair','Head',.002)
    B.ribbon('AW_Mouth',[(-.019,-.116,1.495),(0,-.119,1.492),(.019,-.116,1.496)],.003,'skinShade','Head',.001)

# Rooted dark scalp, chunky independent locks and asymmetric fringe.
B.loft('AW_HairScalp',[octagon(.088,.085,.093,1.65),octagon(.098,.101,.103,1.61),octagon(.102,.084,.107,1.50),octagon(.090,.063,.109,1.40)],'hair','Head')
def lock(name,root,wide,tip,mat='hair'):
    x,y,z=root;tx,ty,tz=tip
    return B.rigid(B.mesh(name,[(x-wide,y,z),(x+wide,y+.006,z),(x+wide*.7,y-.025,z-.022),(tx,ty,tz),(x-wide*.65,y-.027,z-.026)],[(0,1,2),(0,2,4),(4,2,3),(0,4,3),(1,3,2),(0,3,1)],mat),'Head')
for i,(x,w,tip) in enumerate([(-.060,.032,(-.082,-.139,1.385)),(-.026,.029,(-.045,-.140,1.493)),(.018,.034,(.054,-.139,1.527)),(.061,.027,(.096,-.111,1.410))]):lock('AW_FrontLock_'+str(i),(x,-.105,1.665),w,tip,'hairPlane' if i==2 else 'hair')
for s in [-1,1]:
    lock('AW_SideLock_'+str(s),(s*.098,.005,1.613),.019,(s*.110,-.034,1.344))

# Deep cream hood: open front, thin turned rim, rear hood volume under the hat.
hood_front=[(-.060,-.059,1.365),(-.150,-.078,1.385),(-.198,-.041,1.451),(-.174,-.032,1.617),(-.112,-.023,1.704),(.099,-.024,1.706),(.164,-.030,1.630),(.191,-.041,1.465),(.148,-.077,1.380),(.058,-.070,1.361)]
hood_mid=[(x*.91,.122,z-.010) for x,y,z in hood_front]
hood_back=[(x,.184,z) for x,z in [(-.005,1.325),(-.09,1.365),(-.14,1.455),(-.13,1.60),(-.08,1.70),(.07,1.70),(.13,1.61),(.14,1.46),(.09,1.36),(.005,1.32)]]
hood=B.loft('AW_Hood', [hood_front,hood_mid,hood_back],'ivory','Head',cap=False,facets={(0,2):('ivory','sand'),(0,6):('sand','fold'),(1,4):('sand','light')})
# A shell with a central face opening; never a filled pentagonal front cap.
inner=[(x*.92,y+.012,z+.006 if z<1.5 else z-.006) for x,y,z in hood_front]
rim=B.loft('AW_HoodRim',[hood_front,inner],'honey','Head',cap=False)
lining=B.loft('AW_HoodLining',[inner,[(x*.96,.099,z-.008) for x,y,z in inner]],'charcoal','Head',cap=False)
backcap=B.panel('AW_HoodRear',hood_back,.010,'sand','Head',normal=(0,-1,0))

# Broad, irregular 12-sided brim and a genuinely bent faceted hat crown.
n=12;outer=[];hole=[]
for i in range(n):
    a=math.tau*i/n;x=math.sin(a)*.301*(1+.035*math.cos(a*3));y=-math.cos(a)*.282
    z=1.708+.028*math.sin(a)-.022*math.cos(a)+(.014 if i in [2,8] else 0)
    outer.append((x,y,z));hole.append((math.sin(a)*.147,-math.cos(a)*.130,1.735))
v=outer+hole+[(x,y,z-.009) for x,y,z in outer]+[(x,y,z-.010) for x,y,z in hole];f=[];fm=[]
for i in range(n):
    j=(i+1)%n;f += [(i,j,n+j),(i,n+j,n+i),(2*n+j,2*n+i,3*n+i,3*n+j),(i,2*n+i,2*n+j,j),(n+j,3*n+j,3*n+i,n+i)]
    fm += [('light' if i in [0,1,10,11] else 'ivory'),'sand' if i in [3,4,8] else 'ivory','charcoal','sand','charcoal']
B.rigid(B.mesh('AW_WideHatBrim',v,f,'ivory',fm),'Head')
def hat_ring(x,y,z,rx,ry):return [(x+math.sin(i*math.tau/8)*rx,y-math.cos(i*math.tau/8)*ry,z+(.012 if i in [3,5] else 0)) for i in range(8)]
hat_rings=[hat_ring(*r) for r in [(0,.015,1.73,.150,.139),(0,.015,1.81,.135,.123),(.012,.032,1.92,.070,.063),(.060,.045,1.958,.035,.032),(.138,.065,1.89,.024,.019),(.230,.097,1.748,.002,.002)]]
B.loft('AW_BentHatCrown',hat_rings,'ivory','Head',sections=['ivory','ivory','ivory','brown','charcoal'],facets={(1,3):('sand','fold'),(2,5):('fold','sand')})
B.loft('AW_HatLeatherBand',[hat_ring(0,.015,1.745,.154,.143),hat_ring(.004,.020,1.799,.144,.134)],'leather','Head',facets={(0,0):('amber','leather'),(0,7):('leatherEdge','leather')})

# Scarf layers remain distinct from the hood and short shoulder mantle.
for name,outline,mat in [
 ('AW_ScarfUnder',[(-.117,-.085,1.416),(.105,-.09,1.420),(.146,-.099,1.343),(.016,-.162,1.260),(-.140,-.110,1.345)],'charcoal'),
 ('AW_ScarfUpper',[(-.134,-.108,1.373),(.103,-.119,1.409),(.129,-.127,1.363),(-.016,-.166,1.314),(-.111,-.135,1.338)],'brown'),
 ('AW_ScarfLower',[(-.109,-.134,1.338),(.128,-.128,1.361),(.089,-.151,1.304),(-.008,-.172,1.262),(-.112,-.141,1.300)],'inner')]:B.panel(name,outline,.011,mat,'Chest')

for side,s in [('L',1),('R',-1)]:
    upper=rig.data.bones['UpperArm.'+side];fore=rig.data.bones['Forearm.'+side]
    a=upper.head_local.copy();elbow=fore.head_local.copy();wrist=fore.tail_local.copy();axis=(wrist-elbow).normalized()
    # Loose bell sleeve with actual inner cuff, rather than a capped cylinder.
    centers=[a, a.lerp(elbow,.65), elbow,elbow.lerp(wrist,.72),wrist+axis*.025]
    rings=[arm_ring(c,axis,r,d) for c,r,d in zip(centers,[.075,.086,.080,.097,.114],[.072,.083,.082,.104,.121])]
    sleeve=B.loft('AW_BellSleeve_'+side,rings,'ivory',sections=['ivory','sand','ivory','honey'],cap=False,facets={(0,4):('ivory','light'),(2,1):('light','ivory'),(3,6):('amber','honey')})
    def sleeve_weights(p,side=side,elbow=elbow,axis=axis):
        t=max(0,min(1,((p-elbow).dot(axis)+.065)/.11));return {'UpperArm.'+side:1-t,'Forearm.'+side:t}
    B.weight(sleeve,sleeve_weights)
    cuff=B.loft('AW_Cuff_'+side,[rings[-1],arm_ring(centers[-1]+axis*.008,axis,.113,.120),arm_ring(centers[-1]+axis*.008,axis,.098,.105),arm_ring(centers[-1]-axis*.06,axis,.085,.091)],'brown',sections=['brown','leather','inner'],cap=False)
    B.rigid(cuff,'Forearm.'+side)
    # Short overlapping shoulder cap with two-dimensional folds, not big armor blocks.
    shoulder=B.loft('AW_ShoulderCape_'+side,[octagon(.076,.081,.073,1.365,cx=s*.171),octagon(.085,.107,.098,1.294,cx=s*.176),octagon(.085,.109,.102,1.224,cx=s*.180)],'ivory',sections=['ivory','honey'],facets={(0,0):('light','ivory'),(1,4):('ochre','honey')})
    B.weight(shoulder,lambda p,side=side:{'Chest':.42,'UpperArm.'+side:.58})
    B.ribbon('AW_ShoulderLeather_'+side,[(s*.10,-.100,1.360),(s*.178,-.106,1.338),(s*.244,-.085,1.306)],.036,'leather','Chest',.007)

# Back mantle has a V hem and large coherent facets.
backrows=[[(x,.125,z) for x,z in [(-.105,1.407),(0,1.375),(.103,1.405)]],
          [(-.250,.110,1.314),(0,.181,1.265),(.250,.112,1.320)],
          [(-.256,.124,1.190),(0,.184,1.095),(.256,.121,1.196)]]
back=B.folded_panel('AW_BackMantle',backrows,['ivory','honey'],.011,True);B.rigid(back,'Chest')
B.panel('AW_BackCollar',[(-.121,.132,1.421),(0,.178,1.397),(.119,.133,1.420),(.149,.145,1.374),(0,.192,1.284),(-.148,.141,1.373)],.012,'ivory','Chest',normal=(0,-1,0))

# Extra coat chains follow the original pelvis while preserving every v1 rest matrix.
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
coat_specs=[]
for label,x,y,backside in [('Front.L',.135,-.142,False),('Front.R',-.135,-.142,False),('Side.L',.243,.005,False),('Side.R',-.243,.005,False),('Back.L',.15,.145,True),('Back.R',-.15,.145,True)]:
    parent=rig.data.edit_bones['Pelvis']
    for k,(top,bottom) in enumerate([(1.005,.57),(.57,.12)],1):
        b=rig.data.edit_bones.new('Coat'+str(k)+'.'+label);b.head=(x,y,top);b.tail=(x,y,bottom);b.parent=parent;b.use_deform=True;parent=b
    coat_specs.append({'label':label,'x':x,'y':y,'back':backside})
bpy.ops.object.mode_set(mode='OBJECT')

for spec in coat_specs:
    label=spec['label'];s=1 if spec['x']>0 else -1;rear=spec['back'];side=label.startswith('Side')
    rows=[]
    for i,(z,spread) in enumerate([(1.015,1),(.82,1.12),(.56,1.36),(.34,1.55),(.235,1.66),(.125,1.73)]):
        if side:
            x=s*.150*spread;y=.140*(1+.48*(1.015-z))*.89
            row=[(x,-y,z+.012),(x+s*.040,0,z+.025),(x,y,z+.015)]
        else:
            inner=.031*spread;outer=.150*spread;x1=s*inner;x2=s*outer;y=(.143 if rear else -.140)*(1+.48*(1.015-z));ridge=y+(.027 if rear else -.027)
            row=[(x1,y,z),(s*(inner+outer)/2,ridge,z+.018),(x2,y*.89,z+(.018 if label=='Front.L' else 0))]
        rows.append(row)
    coat=B.folded_panel('AW_Coat_'+label,rows,['ivory','ivory','sand','amber','brown'],.010,rear,normal=(s,0,0) if side else None)
    # Deliberate broad triangular cloth-color regions, following the constructed fold.
    for key in ['light','honey','ivory','sand']:
        if M[key] not in list(coat.data.materials):coat.data.materials.append(M[key])
    for k,j,tri,key in [(1,0,0,'light'),(1,1,1,'honey' if label.endswith('L') else 'sand'),(2,0,1,'honey'),(3,1,0,'amber')]:
        index=k*8+j*4+tri*2
        coat.data.polygons[index].material_index=list(coat.data.materials).index(M[key]);coat.data.polygons[index+1].material_index=list(coat.data.materials).index(M[key])
    def coat_weights(p,label=label):
        if p.z>.93:return {'Pelvis':(p.z-.93)/.085,'Coat1.'+label:1-(p.z-.93)/.085} if p.z<1.015 else {'Pelvis':1}
        return blend_z(p,.35,.75,'Coat2.'+label,'Coat1.'+label)
    B.weight(coat,coat_weights)
    if not side:
        # Broad ochre placket follows the inner opening; back has a central motif.
        edge=[row[0] for row in rows];edge=[(x,y+(.002 if rear else -.002),z) for x,y,z in edge]
        trim=B.ribbon('AW_CoatEdge_'+label,edge,.017,'honey','Pelvis',.003);B.weight(trim,coat_weights)

# Belt is a closed ring, with a second narrow retaining strap and leather pouches.
for name,z1,z2,mat in [('AW_WaistBelt',.984,1.044,'leather'),('AW_BeltLower',.962,.983,'leatherEdge')]:
    B.loft(name,[octagon(.145,.119,.118,z1),octagon(.145,.119,.118,z2)],mat,'Pelvis')

def diamond(name,c,w,h,mat,bone,back=False):
    x,y,z=c;return B.panel(name,[(x,y,z+h/2),(x+w/2,y,z),(x,y,z-h/2),(x-w/2,y,z)],.006,mat,bone,normal=(0,-1 if back else 1,0))

if not args.blockout:
    # Secular diamond ornament and functional rectangular buckle frames.
    diamond('AW_HatBuckle',(.058,-.135,1.780),.048,.055,'bronze','Head')
    diamond('AW_HatBuckleInset',(.058,-.140,1.780),.022,.029,'charcoal','Head')
    diamond('AW_BackDiamond',(0,.199,1.263),.043,.056,'amber','Chest',True)
    for x in [-.097,.005,.097]:
        B.panel('AW_BeltKeeper_'+str(x),[(x-.009,-.128,.976),(x+.009,-.128,.976),(x+.009,-.128,1.051),(x-.009,-.128,1.051)],.009,'bronze','Pelvis')
    B.panel('AW_BuckleFrame',[(-.033,-.132,.996),(.031,-.132,.996),(.031,-.132,1.035),(-.033,-.132,1.035)],.008,'bronze','Pelvis')
    B.panel('AW_BuckleInset',[(-.021,-.141,1.006),(.020,-.141,1.006),(.020,-.141,1.026),(-.021,-.141,1.026)],.002,'leather','Pelvis')
    for side,s in [('L',1),('R',-1)]:
        x=s*(.182 if side=='L' else .174);z=.770 if side=='L' else .827;cy=-.096 if side=='L' else -.118
        B.loft('AW_Pouch_'+side,[octagon(.054,.050,.039,z-.106,cx=x,cy=cy),octagon(.061,.055,.044,z+.066,cx=x,cy=cy)],'leather','Pelvis')
        B.panel('AW_PouchFlap_'+side,[(x-.066,cy-.059,z+.075),(x+.066,cy-.059,z+.075),(x+.057,cy-.064,z+.028),(x,cy-.070,z+.008),(x-.059,cy-.063,z+.028)],.008,'brown','Pelvis')
        B.ribbon('AW_PouchStrap_'+side,[(x-.016,-.127,1.038),(x,-.156,.933),(x,cy-.071,z+.028)],.019,'leatherEdge','Pelvis')
        diamond('AW_PouchClasp_'+side,(x,cy-.079,z+.032),.025,.037,'bronze','Pelvis')
        strap=B.ribbon('AW_HangingStrap_'+side,[(s*.08,-.133,.976),(s*.10,-.165,.86),(s*.13,-.183,.738)],.020,'leather','Pelvis')
        diamond('AW_StrapTip_'+side,(s*.13,-.190,.721),.032,.046,'honey','Pelvis')

# Hand geometry and new finger controls live in a separate small source module.
from weaver_hands import build_hands
finger_specs=build_hands(B,rig)
from refine_weaver_forms import refine_large_forms
refine_large_forms(B,rig)

# Store the contract explicitly: all 54 base matrices and parents are preserved;
# new child bones add individual fingers and independent long-coat chains.
schema=json.loads((FACTORY/'rig-schema.json').read_text())
core_error=max(abs(rig.data.bones[n].matrix_local[i][j]-original[n][i][j]) for n in original for i in range(4) for j in range(4))
assert core_error<1e-7
for a in list(bpy.data.actions):bpy.data.actions.remove(a)
selected=['Idle','Walk','Run','JumpStart','Airborne','Fall','Land','Cast','Hit','Death']
actions=load_actions(ERA/'ArtSource/Characters/Archer/Archer_Animated.blend',['Archer_'+n for n in selected])
rig['era_editable_copy']=True;rig['era_reference']=False;rig['era_core_rest_error']=core_error
rig['era_rig_contract']='ERA_Humanoid54_v1 plus 20 finger bones and 12 long-coat bones; Generic'
metadata={'reference':'Reference.png','source_archer_sha256':hashlib.sha256((ERA/'ArtSource/Characters/Archer/Archer_Animated.blend').read_bytes()).hexdigest(),
 'core_rest_matrix_max_error':core_error,'base_bones':54,'total_bones':len(rig.data.bones),'palette_srgb':palette,'coat':coat_specs,'fingers':finger_specs,
 'designed_height_m':1.98,'reference_ratios_approx':{'hat_width_to_height':.30,'shoulder_cloth_to_height':.26,'hem_to_height':.33,'waist_level_from_sole':.57},
 'measurement_note':'Approximate image-space silhouette guides; drawing has perspective and the front hands are posed. Under-clothing anatomy is not measurable from the illustration.'}
(HERE/'construction.json').write_text(json.dumps(metadata,indent=2),encoding='utf8')

if not args.blockout:
    from animate_aura_weaver import prepare_actions
    prepare_actions(rig,actions,metadata)
else:rig.animation_data_clear()
(HERE/'construction.json').write_text(json.dumps(metadata,indent=2),encoding='utf8')

# A consistent neutral studio, kept out of the FBX selection.
studio=bpy.data.collections.new('STUDIO / not exported');scene.collection.children.link(studio)
floor=B.mesh('Studio ground',[(-100,-100,-.012),(100,-100,-.012),(100,100,-.012),(-100,100,-.012)],[(0,1,2,3)],'sand')
floor.modifiers.clear();floor.parent=None;B.meshes.remove(floor);collection.objects.unlink(floor);studio.objects.link(floor)
floor.data.materials.clear();fm=material('Studio','DADDD8');floor.data.materials.append(fm)
world=bpy.data.worlds.new('Neutral studio');world.use_nodes=True
next(n for n in world.node_tree.nodes if n.type=='BACKGROUND').inputs['Color'].default_value=(.42,.46,.48,1)
next(n for n in world.node_tree.nodes if n.type=='BACKGROUND').inputs['Strength'].default_value=.6;scene.world=world
for name,pos,energy,size in [('Key',(-3,-4,5),450,4),('Fill',(3,-2,3),240,3),('Rim',(1,3,4),350,3)]:
    data=bpy.data.lights.new(name,'AREA');data.energy=energy;data.shape='DISK';data.size=size
    o=bpy.data.objects.new(name,data);studio.objects.link(o);o.location=pos;o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler()
for name,d in [('Front',(0,-6,1.03)),('Side',(6,0,1.03)),('Back',(0,6,1.03)),('Perspective',(2,-3.2,1.9))]:
    data=bpy.data.cameras.new('Camera_'+name);data.type='ORTHO' if name!='Perspective' else 'PERSP';data.ortho_scale=2.22;data.lens=58
    o=bpy.data.objects.new('Camera_'+name,data);studio.objects.link(o);o.location=d;o.rotation_euler=(Vector((0,0,1.00))-o.location).to_track_quat('-Z','Y').to_euler()
scene.camera=bpy.data.objects['Camera_Perspective']
scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True;scene.render.resolution_x=850;scene.render.resolution_y=1050;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG'
scene.view_settings.view_transform='AgX'
rig.data.pose_position='REST';scene.frame_set(1)
filename='AuraWeaver_Blockout.blend' if args.blockout else 'AuraWeaver.blend'
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/filename),compress=True)
if not args.no_render:
    preview=HERE/'Previews';preview.mkdir(exist_ok=True)
    for name in ['Front','Side','Back','Perspective']:
        scene.camera=bpy.data.objects['Camera_'+name];scene.render.filepath=str(preview/(('Blockout-' if args.blockout else 'AuraWeaver-')+name.lower()+'.png'));bpy.ops.render.render(write_still=True)
print('AURA_WEAVER_BUILT',json.dumps({'file':str(HERE/filename),'meshes':len(B.meshes),'bones':len(rig.data.bones),'triangles':sum(len(p.vertices)-2 for o in B.meshes for p in o.data.polygons)}))
