"""ERA character workshop. Run with the installed Blender --background --python.
Authored polygon surfaces, clothing layers, hardware and skin weights; no image projection.
The existing ERA bind skeleton is read from the prefab so its animation clips remain usable.
"""
import bpy, bmesh, math, json, re, sys, hashlib
from pathlib import Path
from mathutils import Vector, Quaternion, Matrix

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/Era/Art/BlenderCharacters'
SOURCE = Path(__file__).resolve().parent
PREVIEW = SOURCE / 'Previews'
for p in (OUT, PREVIEW): p.mkdir(parents=True, exist_ok=True)
TAU = math.pi*2

def V(p): return Vector(p)
def B(p): return Vector((p[0], -p[2], p[1]))
def U(p): return (p[0], p[2], -p[1])
def mix(a,b,t): return V(a).lerp(V(b),t)

def read_rig(model):
    folder = 'Creatures' if model in ('Stalker','Wisp') else 'Characters'
    text=(ROOT/f'Assets/Era/Resources/{folder}/{model}.prefab').read_text(encoding='utf-8-sig')
    chunks=re.split(r'--- !u!(\d+) &(-?\d+)\n',text.replace('\r\n','\n'))
    objects={}; transforms={}
    for at in range(1,len(chunks),3):
        typ,ident,body=chunks[at:at+3]
        if typ=='1': objects[ident]=re.search(r'  m_Name: (.*)',body).group(1)
        if typ=='4':
            def values(k):return [float(x.split(':')[1]) for x in re.search(k+r': \{([^}]+)\}',body).group(1).split(',')]
            q=values('m_LocalRotation')
            transforms[ident]={'go':re.search(r'm_GameObject: \{fileID: (-?\d+)',body).group(1),
                'parent':re.search(r'm_Father: \{fileID: (-?\d+)',body).group(1),
                'mat':Matrix.LocRotScale(V(values('m_LocalPosition')),Quaternion((q[3],*q[:3])),V(values('m_LocalScale')))}
    def world(i):
        t=transforms[i]
        return world(t['parent'])@t['mat'] if t['parent'] in transforms else t['mat']
    block=re.search(r'  m_Bones:\n((?:  - \{fileID: -?\d+\}\n)+)',text).group(1)
    ids=re.findall(r'fileID: (-?\d+)',block)
    rig={objects[transforms[i]['go']]:{'position':world(i).translation,
        'parent':objects.get(transforms[transforms[i]['parent']]['go'],'') if transforms[i]['parent'] in transforms else ''} for i in ids}
    old={n:j['position'].copy() for n,j in rig.items()}
    targets={'hips':(0,1.02,0),'spine':(0,1.18,0),'chest':(0,1.52,0),'head':(0,1.74,0)}
    for side in (-1,1):
        suffix='l' if side<0 else 'r'
        for name,x,y,z in [('upperarm',.27,1.64,0),('lowerarm',.54,1.64,-.012),('hand',.82,1.64,0),('upperleg',.14,.95,0),('lowerleg',.14,.53,.006),('foot',.14,.14,-.016)]:targets[name+'.'+suffix]=(side*x,y,z)
    solved={}
    def solve(n):
        if n in solved:return solved[n]
        parent=rig[n]['parent'];solved[n]=V(targets[n]) if n in targets else old[n]+(solve(parent)-old[parent] if parent in rig else Vector())
        return solved[n]
    for name in rig:rig[name]['position']=solve(name)
    return rig

PALETTE={
 'skin':(0.75,.51,.30), 'skin_light':(.94,.72,.43), 'skin_shadow':(.54,.29,.16),
 'linen':(.87,.84,.67), 'linen_shadow':(.65,.65,.49), 'cream':(.98,.94,.77),
 'leather':(.26,.13,.075), 'leather_light':(.43,.25,.13), 'leather_edge':(.59,.40,.20),
 'brown':(.38,.23,.13), 'brown_light':(.52,.34,.19), 'brown_dark':(.19,.105,.065),
 'green':(.16,.28,.18), 'green_light':(.30,.39,.21), 'green_dark':(.055,.16,.13),
 'teal':(.025,.19,.23), 'teal_light':(.07,.32,.36), 'teal_dark':(.013,.075,.11),
 'blue':(.21,.32,.40), 'blue_light':(.38,.57,.60), 'blue_dark':(.09,.15,.24),
 'red':(.78,.07,.018), 'red_light':(.98,.16,.03), 'red_dark':(.39,.018,.02),
 'gold':(.76,.48,.13), 'gold_light':(.98,.75,.27), 'gold_shadow':(.49,.27,.07),
 'steel':(.40,.48,.52), 'steel_light':(.73,.79,.77), 'black':(.018,.021,.031),
 'fur':(.18,.21,.25), 'fur_light':(.32,.38,.40), 'fur_dark':(.075,.09,.14),
 'hair':(.58,.58,.49), 'hair_light':(.78,.79,.66), 'blond':(.81,.65,.33),
 'eye':(.12,.58,.40), 'blood_eye':(.81,.035,.025), 'paper':(.91,.85,.62),
 'ink':(.21,.18,.12), 'purple':(.20,.09,.24)
}
MATS={}
def srgb_channel(v):return v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4
for name,color in PALETTE.items():
    color=tuple(srgb_channel(v) for v in color)
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Roughness'].default_value=.88
    MATS[name]=m

class Character:
    def __init__(self,key,label,rig_source):
        self.key,self.label=key,label;self.rig=read_rig(rig_source);self.parts=[]
        self.collection=bpy.data.collections.new(label);bpy.context.scene.collection.children.link(self.collection)
        self.root=bpy.data.objects.new(label+' / origin',None);self.collection.objects.link(self.root)
        self.arm=self.make_armature()
    def p(self,name):return V(self.rig[name]['position'])
    def make_armature(self):
        data=bpy.data.armatures.new(self.key+' / editable skeleton');obj=bpy.data.objects.new(self.key+' / Rig',data)
        self.collection.objects.link(obj);obj.parent=self.root;bpy.context.view_layer.objects.active=obj;obj.select_set(True)
        bpy.ops.object.mode_set(mode='EDIT')
        for name,joint in self.rig.items():
            bone=data.edit_bones.new(name);bone.head=B(joint['position'])
            children=[k for k,v in self.rig.items() if v['parent']==name and (V(v['position'])-joint['position']).length>.03]
            chosen=next((c for c in children if not any(x in c.lower() for x in ('slot','ik'))),children[0] if children else None)
            bone.tail=B(self.rig[chosen]['position']) if chosen else bone.head+Vector((0,0,.07))
            if (bone.tail-bone.head).length<.01:bone.tail=bone.head+Vector((0,0,.07))
        for name,joint in self.rig.items():
            if joint['parent'] in data.edit_bones:data.edit_bones[name].parent=data.edit_bones[joint['parent']]
        bpy.ops.object.mode_set(mode='OBJECT');obj.select_set(False);obj.show_in_front=True;return obj
    def mesh(self,name,vertices,faces,mat,bone='spine',weight=None,accents=None):
        mesh=bpy.data.meshes.new(name);mesh.from_pydata([B(v) for v in vertices],[],faces);mesh.update()
        bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(mesh);bm.free()
        obj=bpy.data.objects.new(name,mesh);self.collection.objects.link(obj);obj.parent=self.root
        names=[mat]+list(accents or []);[mesh.materials.append(MATS[m]) for m in names]
        for f in mesh.polygons:
            f.use_smooth=False
            # Facet values come from normals, rather than random light-coloured patches.
            f.material_index=0
        groups={}
        for i,v in enumerate(vertices):
            weights=weight(V(v)) if weight else {bone:1}
            for joint,value in weights.items():
                if value<=0:continue
                if joint not in groups:groups[joint]=obj.vertex_groups.new(name=joint)
                groups[joint].add([i],value,'REPLACE')
        mod=obj.modifiers.new('ERA skeleton','ARMATURE');mod.object=self.arm
        obj['detail']=name;self.parts.append(obj);return obj
    def box(self,name,p,size,mat,bone='spine',rotation=None):
        verts=[V(p)+V((sx*size[0]/2,sy*size[1]/2,sz*size[2]/2)) for sx,sy,sz in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
        if rotation:verts=[V(p)+rotation@(v-V(p)) for v in verts]
        return self.mesh(name,verts,[(0,1,2,3),(4,7,6,5),(0,4,5,1),(1,5,6,2),(2,6,7,3),(4,0,3,7)],mat,bone)
    def loft(self,name,rings,mat,bone='spine',n=8,phase=math.pi/8,weight=None,accents=None,split=None):
        verts=[]
        for p,rx,rz in rings:
            for i in range(n):
                a=TAU*i/n+phase;verts.append(V(p)+V((math.cos(a)*rx,0,math.sin(a)*rz)))
        faces=[]
        for r in range(len(rings)-1):
            for i in range(n):
                if split and i in split:continue
                a=r*n+i;b=r*n+(i+1)%n;c=b+n;d=a+n
                if (r+i)%2:faces.extend([(a,b,d),(b,c,d)])
                else:faces.extend([(a,b,c),(a,c,d)])
        faces += [tuple(range(n-1,-1,-1)),tuple((len(rings)-1)*n+i for i in range(n))]
        return self.mesh(name,verts,faces,mat,bone,weight,accents)
    def tube(self,name,a,b,ra,rb,mat,bone='spine',n=8,depth=1):
        a,b=V(a),V(b);axis=(b-a).normalized();u=axis.cross(V((0,0,1))).normalized()
        if u.length<.01:u=V((1,0,0))
        v=axis.cross(u);verts=[]
        for center,radius in ((a,ra),(a.lerp(b,.5),(ra+rb)*.52),(b,rb)):
            for i in range(n):
                t=TAU*i/n;verts.append(center+(u*math.cos(t)+v*math.sin(t)*depth)*radius)
        faces=[tuple(range(n-1,-1,-1)),tuple(2*n+i for i in range(n))]
        for r in range(2):
            for i in range(n):faces.append((r*n+i,r*n+(i+1)%n,(r+1)*n+(i+1)%n,(r+1)*n+i))
        return self.mesh(name,verts,faces,mat,bone)
    def jewel(self,name,p,size,mat,bone='spine'):
        p=V(p);x,y,z=size
        return self.mesh(name,[p+V(t) for t in [(-x,0,0),(x,0,0),(0,-y,0),(0,y,0),(0,0,-z),(0,0,z)]],[(0,2,4),(1,4,2),(0,5,2),(1,2,5),(0,4,3),(1,3,4),(0,3,5),(1,5,3)],mat,bone)
    def panel(self,name,points,mat,bone='spine',thickness=.014):
        pts=[V(p) for p in points];front=list(range(len(pts)));back=list(range(len(pts),len(pts)*2))
        verts=pts+[p+V((0,0,-thickness)) for p in pts]
        faces=[tuple(front),tuple(reversed(back))]+[(i,(i+1)%len(pts),(i+1)%len(pts)+len(pts),i+len(pts)) for i in range(len(pts))]
        return self.mesh(name,verts,faces,mat,bone)
    def ribbon(self,name,a,b,width,mat,bone='spine'):
        a,b=V(a),V(b);d=b-a;side=V((-d.y,d.x,0)).normalized()*width/2
        return self.panel(name,[a-side,a+side,b+side,b-side],mat,bone)
    def ring(self,name,p,r,width,mat,bone='hips',depth=.7,n=12):
        return self.loft(name,[(V(p)-V((0,width/2,0)),r,r*depth),(V(p)+V((0,width/2,0)),r,r*depth)],mat,bone,n)
    def buckle(self,p,bone='hips',size=.1):
        p=V(p);self.box('Buckle / forged rim',p,(size,size*.77,.026),'gold',bone)
        self.box('Buckle / inset opening',p+V((0,0,.017)),(size*.64,size*.46,.012),'brown_dark',bone)
        self.box('Buckle / prong',p+V((0,0,.025)),(size*.12,size*.56,.009),'gold_light',bone)
    def limb(self,side,cloth='green',bare=False,puffy=False,wolf=False):
        s='l' if side<0 else 'r';arm='upperarm.'+s;fore='lowerarm.'+s;hand='hand.'+s
        a,b,c=self.p(arm),self.p(fore),self.p(hand)
        self.tube('Sleeve / upper '+s,a,b,.145 if not wolf else .19,.12,cloth,arm,8)
        self.tube('Sleeve / lower '+s,b,c,.13 if not puffy else .20,.073,'skin' if bare else cloth,fore,8)
        self.tube('Cuff / leather band '+s,mix(b,c,.82),mix(b,c,.97),.086,.083,'fur' if wolf else 'leather',fore,8)
        if not wolf:
            for t in (.84,.93):self.tube('Cuff / raised trim '+s,mix(b,c,t),mix(b,c,t+.025),.089,.089,'gold' if puffy else 'leather_edge',fore,8)
        self.box('Palm / articulated '+s,c+V((side*.035,-.015,.005)),(.13,.13,.10),'fur_dark' if wolf else 'skin' if bare else 'leather',hand)
        for i in range(4):
            start=c+V((side*.07,-.05+i*.032,.037));end=start+V((side*(.065 if not wolf else .09),-.01,.015))
            self.tube('Finger '+s+str(i),start,end,.018,.013,'fur' if wolf else 'skin' if bare else 'leather',hand,5)
            if wolf:self.tube('Ivory claw '+s+str(i),end,end+V((side*.05,-.017,.018)),.014,.001,'linen',hand,4)
        self.tube('Thumb '+s,c+V((side*.006,-.07,.035)),c+V((side*.07,-.10,.048)),.025,.014,'fur' if wolf else 'skin' if bare else 'leather',hand,5)
        thigh,knee,foot=(self.p(k+'.'+s) for k in ('upperleg','lowerleg','foot'))
        self.tube('Leg / trousers '+s,thigh,knee,.145 if not wolf else .17,.11,cloth,'upperleg.'+s,8)
        self.tube('Boot / tall shaft '+s,knee,foot+V((0,.01,0)),.112,.078,'fur_dark' if wolf else 'leather','lowerleg.'+s,8)
        p=foot+V((0,-.07,.035))
        self.loft('Boot / shaped foot '+s,[(p+V((0,-.04,0)),.105,.17),(p,.12,.19),(p+V((0,.09,-.045)),.085,.11)],'fur_dark' if wolf else 'leather','foot.'+s,8)
        self.box('Boot / sole '+s,p+V((0,-.052,.02)),(.215,.036,.34),'black','foot.'+s)
        if not wolf:
            for y in (.04,.15):self.ring('Boot / ankle strap '+s,foot+V((0,y,0)),.091,.035,'leather_light','lowerleg.'+s,.8,8)
        else:
            for toe in range(3):
                start=foot+V(((toe-1)*.06,-.085,.16))
                self.tube('Paw / toe '+s+str(toe),start,start+V((0,-.015,.08)),.035,.022,'fur_dark','foot.'+s,5)
                self.tube('Paw / black claw '+s+str(toe),start+V((0,-.014,.07)),start+V((0,-.025,.12)),.023,.001,'black','foot.'+s,4)
        if not wolf:
            self.panel('Boot / pointed cuff '+s,[knee+V((-.13,.04,.09)),knee+V((.13,.04,.09)),knee+V((.08,-.10,.13)),knee+V((0,-.13,.145)),knee+V((-.08,-.10,.13))],'linen_shadow' if cloth=='green' else 'brown_light','lowerleg.'+s)
    def body(self,cloth='green',wolf=False):
        self.loft('Torso / tailored silhouette', [((0,.96,0),.23,.14),((0,1.13,0),.25,.155),((0,1.47,0),.32 if not wolf else .39,.18),((0,1.70,0),.28 if not wolf else .36,.15)],cloth,'spine',10,accents=[cloth if wolf else cloth])
        def neck_w(p):
            t=max(0,min(1,(p.y-1.67)/.16));return {'chest':1-t,'head':t}
        self.loft('Neck / weighted continuous bridge',[((0,1.64,0),.105,.085),((0,1.75,0),.09,.078),((0,1.83,0),.105,.085)],'fur' if wolf else 'skin','chest',8,weight=neck_w)
        for side in (-1,1):self.limb(side,cloth,bare=cloth=='brown',puffy=cloth=='teal',wolf=wolf)
        self.ring('Waist / leather belt',(0,1.05,0),.267,.092,'leather','hips',.7)
        self.buckle((0,1.052,.197))
    def face(self,kind='human',beard=False,elf=False):
        h=self.p('head');skin='blue_dark' if kind=='shade' else 'linen' if kind=='mask' else 'skin_light' if elf else 'skin'
        self.loft('Face / planar jaw and cheekbones',[(h+V((0,.015,.025)),.085,.09),(h+V((0,.10,.015)),.16,.135),(h+V((0,.27,0)),.175,.14),(h+V((0,.36,-.01)),.12,.11),(h+V((0,.39,-.01)),.05,.055)],skin,'head',8)
        self.jewel('Face / nose bridge',h+V((0,.19,.15)),(.036,.074,.067),skin,'head')
        self.panel('Face / mouth',[h+V((-.06,.08,.145)),h+V((.06,.08,.145)),h+V((.035,.062,.15)),h+V((-.025,.062,.15))],'skin_shadow' if kind!='mask' else 'black','head')
        for side in (-1,1):
            self.panel('Eye / inset socket',[h+V((side*.027,.243,.147)),h+V((side*.132,.239,.115)),h+V((side*.122,.219,.135)),h+V((side*.035,.222,.16))],'black','head')
            if kind!='mask':
                self.jewel('Eye / iris',h+V((side*.080,.231,.149)),(.016,.012,.009),'eye' if elf else 'steel_light','head')
                self.ribbon('Brow / angled',h+V((side*.024,.265,.147)),h+V((side*.14,.273,.10)),.021,'blue_dark' if kind=='shade' else 'blond' if elf else 'hair' if beard else 'brown_dark','head')
            if elf:
                self.mesh('Ear / pointed elven pinna',[h+V((side*.145,.24,0)),h+V((side*.38,.28,-.005)),h+V((side*.17,.12,.025)),h+V((side*.18,.22,.075))],[(0,1,3),(1,2,3),(2,0,3),(0,2,1)],'skin_light','head',accents=['skin_shadow'])
            else:self.jewel('Ear / rim',h+V((side*.18,.19,-.005)),(.035,.075,.05),skin,'head')
        if beard:self.beard(h)
    def beard(self,h):
        self.loft('Beard / angular jaw mass',[(h+V((0,.04,.11)),.135,.08),(h+V((0,-.16,.115)),.07,.04),(h+V((0,-.24,.12)),.018,.018)],'hair_light','head',8,accents=['hair'])
        for side in (-1,1):
            self.ribbon('Moustache / parted',h+V((side*.008,.125,.188)),h+V((side*.12,.06,.165)),.055,'hair_light','head')
            self.panel('Beard / side plane',[h+V((side*.15,.12,.08)),h+V((side*.145,-.07,.11)),h+V((side*.04,-.22,.16)),h+V((side*.065,.02,.195))],'hair','head')
        for n in range(5):self.ribbon('Beard / carved lock',h+V(((n-2)*.033,.03,.204)),h+V(((n-2)*.017,-.185+abs(n-2)*.02,.157)),.012,'hair' if n%2 else 'cream','head')
    def hair(self,elf=False,short=False):
        h=self.p('head');mat='brown_dark' if short else 'blond' if elf else 'hair'
        light='brown' if short else 'gold' if elf else 'hair_light'
        # A continuous scalp covers the entire skull; locks add volume to this surface.
        n=16;vertices=[]
        for row,(y,rx,rz) in enumerate([(0,.178,.153),(.34,.184,.154),(.42,.122,.108),(.447,.012,.012)]):
            for i in range(n):
                a=TAU*i/n;front=max(0,math.sin(a));back=max(0,-math.sin(a))
                yy=(.20+.095*front-(.055 if short else .10)*back) if row==0 else y
                vertices.append(h+V((math.cos(a)*rx,yy,math.sin(a)*rz-.014)))
        faces=[]
        for row in range(3):
            for i in range(n):a=row*n+i;b=row*n+(i+1)%n;faces.append((a,b,b+n,a+n))
        faces.append(tuple(range(3*n,4*n)))
        self.mesh('Hair / continuous fitted scalp',vertices,faces,mat,'head')
        for i in range(n):
            a=TAU*i/n;tangent=V((-math.sin(a),0,math.cos(a)))*(.027 if short else .035)
            start=h+V((math.cos(a)*.15,.36,math.sin(a)*.13-.014))
            y=.29 if math.sin(a)>.35 else (.18 if short else .07)
            end=h+V((math.cos(a)*.19,y,math.sin(a)*.16-.014))
            ridge=mix(start,end,.45)+V((math.cos(a)*.025,.007,math.sin(a)*.025))
            self.mesh('Hair / layered swept lock '+str(i),[start-tangent,start+tangent,end-tangent*.4,end+tangent*.4,ridge],[(0,1,4),(1,3,4),(3,2,4),(2,0,4),(0,2,3,1)],light if i%5==1 else mat,'head')
        for i in range(4):
            x=(i-1.5)*.067
            self.panel('Hair / side swept fringe '+str(i),[h+V((x-.04,.355,.142)),h+V((x+.025,.37,.149)),h+V((x+.057,.277,.15)),h+V((x+.019,.285,.167))],mat,'head',.018)
    def skirt(self,mat='green',bottom=.3,split=True):
        n=12;verts=[]
        for row,(y,rx,rz) in enumerate([(1.04,.25,.17),(.67,.32,.22),(bottom,.40,.29)]):
            for i in range(n):
                t=TAU*i/n+math.pi/12;yy=y+(0 if row<2 else .06*(i%2))
                verts.append((math.cos(t)*rx,yy,math.sin(t)*rz))
        faces=[]
        for row in range(2):
            for i in range(n):
                if split and i in (2,8):continue
                a=row*n+i;b=row*n+(i+1)%n;c=b+n;d=a+n;faces.extend([(a,b,c),(a,c,d)])
        def weights(p):
            t=max(0,min(.65,(1.0-p.y)*.75));return {'hips':1-t,('upperleg.l' if p.x<0 else 'upperleg.r'):t}
        return self.mesh('Coat / cut panels and split hem',verts,faces,mat,'hips',weights,[mat+'_light'] if mat+'_light' in MATS else ['linen_shadow'])
    def cape(self,mat='green',bottom=.25):
        verts=[]
        for row,(y,w,z) in enumerate([(1.68,.26,-.15),(1.50 if bottom>1 else 1.38,.30,-.22),((1.50+bottom)/2 if bottom>1 else .82,.38,-.25),(bottom,.48,-.30)]):
            for col in range(5):
                x=(col-2)*w/2;verts.append((x,y+(0 if row<3 else .12*(col%2)),z+(.035 if col%2 else -.025)))
        faces=[]
        for r in range(3):
            for c in range(4):a=r*5+c;faces.extend([(a,a+1,a+6),(a,a+6,a+5)])
        obj=self.mesh('Cape / broad folded back silhouette',verts,faces,mat,'spine',lambda p:{'chest':max(.5,min(1,(p.y-.3)/1.3)),'hips':1-max(.5,min(1,(p.y-.3)/1.3))},[mat+'_dark'] if mat+'_dark' in MATS else ['brown_dark'])
        sol=obj.modifiers.new('Cloth edge thickness','SOLIDIFY');sol.thickness=.018;return obj
    def hood(self,mat='green',point=False):
        h=self.p('head');rings=[(h+V((0,.0,-.035)),.25,.21),(h+V((0,.25,-.05)),.27,.23),(h+V((0,.46,-.08)),.21,.18),(h+V((0,.53,-.12)),.08,.09)]
        self.loft('Hood / polygon crown and back',rings,mat,'head',10,split={1,2,3},accents=[mat+'_light'] if mat+'_light' in MATS else ['blue_light'])
        for side in (-1,1):
            self.panel('Hood / thick face opening '+str(side),[h+V((0,.48,.12)),h+V((side*.19,.34,.20)),h+V((side*.20,.03,.15)),h+V((side*.27,-.04,.11)),h+V((side*.27,.30,.12)),h+V((side*.18,.48,.055))],mat,'head')
            self.ribbon('Hood / bound face edge',h+V((0,.49,.125)),h+V((side*.20,.31,.207)),.024,mat+'_light' if mat+'_light' in MATS else 'linen_shadow','head')
        if point:
            self.tube('Hood / long swept crown',h+V((0,.42,-.12)),h+V((0,.52,-.39)),.16,.08,mat,'head',6)
            self.tube('Hood / falling point',h+V((0,.52,-.39)),h+V((0,.37,-.60)),.08,.004,mat,'head',5)
    def pouch(self,p,bone='hips'):
        p=V(p);self.box('Pouch / stitched leather body',p,(.15,.18,.075),'leather_light',bone)
        self.panel('Pouch / envelope flap',[p+V((-.08,.09,.044)),p+V((.08,.09,.044)),p+V((.07,.01,.05)),p+V((0,-.025,.054)),p+V((-.07,.01,.05))],'leather',bone)
        self.jewel('Pouch / clasp',p+V((0,.015,.06)),(.015,.017,.008),'gold',bone)
    def book(self,p,scale=1,bone='hips',open_book=False):
        p=V(p);w=.18*scale;y=.25*scale;d=.07*scale
        for z in (-d,d):self.box('Book / embossed cover',p+V((0,0,z)),(w*2+.025,y*2+.025,.019),'green_dark',bone)
        self.box('Book / parchment block',p,(w*2,y*2,d*2-.02),'paper',bone)
        self.box('Book / rounded spine',p+V((-w,0,0)),(.035,y*2+.02,d*2+.024),'leather',bone)
        for side in (-1,1):self.box('Book / brass corner',p+V((side*(w-.018),y-.018,d+.012)),(.032,.035,.015),'gold',bone)
        self.box('Book / strap clasp',p+V((w,.0,d+.012)),(.075,.037,.021),'gold',bone)
        if open_book:
            for row in range(15):self.box('Book / engraved text line '+str(row),p+V((.015,y*.78-row*y*.105,d+.023)),(w*1.45-(row%4)*.012,.005,.003),'ink',bone)
    def cross(self,p,scale=1,bone='chest'):
        p=V(p);self.box('Cross / long stem',p,(.032*scale,.19*scale,.022),'gold',bone)
        self.box('Cross / arms',p+V((0,.031*scale,.008)),(.13*scale,.032*scale,.026),'gold_light',bone)
        for dx,dy in [(0,.106),(0,-.105),(-.078,.031),(.078,.031)]:self.jewel('Cross / bevelled end',p+V((dx*scale,dy*scale,.012)),(.024*scale,.025*scale,.015),'gold_light',bone)

def ranger(c):
    c.body('green');c.face(elf=True);c.hair(True);c.hood('green');c.cape('green',.30);c.skirt('green',.42)
    for side in (-1,1):
        p=c.p('upperarm.'+('l' if side<0 else 'r'))
        for layer in range(2):c.panel('Shoulder / layered leather scale',[p+V((side*.00,.09-layer*.055,-.09)),p+V((side*.17,.015-layer*.06,-.09)),p+V((side*.23,-.01-layer*.06,.13)),p+V((side*.04,.08-layer*.055,.17))],'linen_shadow','upperarm.'+('l' if side<0 else 'r'))
    c.ribbon('Harness / chest diagonal',(-.28,1.60,.193),(.25,1.05,.204),.074,'leather_light')
    c.buckle((-.07,1.36,.222),'spine',.085)
    for x in (-.19,.19):c.pouch((x,1.00,.16))
    c.tube('Quiver / leather case',(.25,1.10,-.26),(.25,1.80,-.26),.12,.14,'leather_light','chest',8)
    c.ring('Quiver / top rim',(.25,1.78,-.26),.15,.04,'leather_edge','chest',1)
    for i in range(7):
        p=V((.19+(i%3)*.055,1.83+(i%2)*.06,-.29+(i//3)*.035));c.tube('Arrow / shaft '+str(i),p-V((0,.38,0)),p+V((0,.16,0)),.007,.007,'leather_edge','chest',4)
        for s in (-1,1):c.panel('Arrow / split fletching', [p+V((0,.13,0)),p+V((s*.028,.09,0)),p+V((s*.027,.015,0)),p-V((0,.015,0))],'black','chest',.004)
    # The gameplay weapon remains attached to its existing hand slot. A separate prop is editable in Blender.
    c.ribbon('Tunic / woven centre trim',(0,1.025,.207),(0,.46,.275),.036,'linen_shadow','hips')
    start=len(c.parts)
    arc=[(-.40,1.60,.10),(-.52,1.51,.10),(-.56,1.30,.10),(-.44,1.12,.10),(-.56,.94,.10),(-.52,.73,.10),(-.40,.64,.10)]
    for i in range(len(arc)-1):c.tube('Display / recurve bow limb',arc[i],arc[i+1],.023,.021,'leather_edge','hips',6)
    c.tube('Display / bow string',arc[0],arc[-1],.002,.002,'linen','hips',4)
    c.tube('Display / wrapped bow grip',(-.44,1.07,.10),(-.44,1.17,.10),.028,.028,'leather','hips',6)
    for obj in c.parts[start:]:obj['display_only']=True

def monk(c):
    c.body('brown');c.face();c.hair(short=True);c.skirt('brown',.31)
    c.panel('Robe / diagonal ivory lapel',[(-.24,1.63,.17),(-.15,1.66,.20),(.25,1.14,.20),(.17,1.09,.218)],'linen')
    c.panel('Robe / broad overlapping wrap',[(.19,1.61,.17),(.29,1.56,.16),(-.19,1.04,.19),(-.25,1.08,.18)],'brown_light')
    c.ribbon('Harness / back left',(-.27,1.59,-.18),(.21,1.04,-.21),.07,'brown_dark')
    c.ribbon('Harness / back right',(.27,1.59,-.18),(-.21,1.04,-.21),.07,'leather')
    c.ring('Waist / layered cloth sash',(0,1.10,0),.275,.048,'brown_dark','hips',.7)
    for x in (-.15,0,.15):c.ribbon('Sash / hanging end',(x,1.01,.218),(x*.9,.32,.29),.075,'leather_edge' if x==0 else 'brown_light','hips')
    c.pouch((.28,.95,-.05));c.pouch((-.27,1.00,.02))
    for side in (-1,1):
        s='l' if side<0 else 'r';a,b=c.p('lowerarm.'+s),c.p('hand.'+s)
        for j in range(5):c.tube('Hand wraps / band '+s+str(j),mix(a,b,.50+j*.065),mix(a,b,.54+j*.065),.099,.095,'linen_shadow' if j%2 else 'brown_dark','lowerarm.'+s,8)

def prelate(c):
    c.body('linen');c.face(beard=True);c.hair();c.skirt('linen',.10,False);c.cape('brown_dark',1.22)
    for side in (-1,1):
        x=side*.20
        c.panel('Vestment / long embroidered stole',[(x-side*.055,1.61,.20),(x+side*.055,1.58,.20),(x+side*.10,.16,.30),(x-side*.025,.16,.31)],'brown','chest')
        c.ribbon('Vestment / gold edging',(x+side*.041,1.57,.223),(x+side*.078,.18,.322),.016,'leather_edge','chest')
        for y in (.55,.91,1.3):c.cross((x+side*.025,y,.255 if y>.9 else .31),.37,'chest')
    h=c.p('head')
    c.panel('Mitre / five sided crown',[h+V((-.20,.35,.14)),h+V((.20,.35,.14)),h+V((.23,.56,.14)),h+V((0,.80,.14)),h+V((-.23,.56,.14))],'brown_dark','head',.29)
    c.cross(h+V((0,.56,.145)),.72,'head');c.cross((0,1.40,.237),1.05)
    for side in (-1,1):c.ribbon('Pendant / chain',(side*.13,1.64,.155),(0,1.43,.229),.013,'gold','chest')
    c.book((.29,1.02,.24),.80,'hips',True);c.pouch((-.30,.96,.03))
    c.cross((0,1.44,-.255),1.1,'chest')

def scholar(c):
    c.body('brown');c.face(beard=True);c.hair();c.skirt('brown',.14,True);c.cape('brown_dark',.28)
    c.panel('Coat / inset linen placket',[(-.14,1.63,.195),(.14,1.63,.195),(.14,.22,.30),(-.14,.22,.30)],'linen')
    for side in (-1,1):
        c.panel('Coat / angular lapel',[(side*.01,1.65,.21),(side*.22,1.71,.10),(side*.30,1.51,.17),(side*.16,1.38,.226)],'brown_dark','chest')
        s='l' if side<0 else 'r';a,b=c.p('upperarm.'+s),c.p('lowerarm.'+s)
        c.tube('Coat / layered shoulder capelet '+s,a,mix(a,b,.65),.19,.17,'brown','upperarm.'+s,8)
    for i in range(6):
        y=1.52-i*.055;c.ribbon('Tunic / tied lacing',(-.045,y,.213),(.045,y-.03,.214),.012,'leather')
        c.ribbon('Tunic / tied lacing',(.045,y,.215),(-.045,y-.03,.214),.012,'leather')
    c.book((.25,.97,.16),.65);c.pouch((-.28,.98,.10))
    for side in (-1,1):c.ribbon('Coat / teal hem piping',(side*.30,.19,.24),(side*.30,.28,.25),.10,'teal_dark','hips')

def dusk(c):
    c.body('blue_dark');c.face(kind='shade');c.hair(short=True);c.hood('blue',True);c.cape('blue',.12);c.skirt('blue',.22,True)
    c.loft('Scarf / high shadow collar',[((0,1.62,.0),.21,.17),((0,1.75,.0),.23,.17)],'blue_dark','chest',8)
    for side in (-1,1):
        for layer in range(3):
            p=V((side*(.20+layer*.035),1.62-layer*.055,0))
            c.panel('Mantle / layered angular pauldron',[p+V((-side*.15,.08,-.13)),p+V((side*.18,-.02,-.13)),p+V((side*.18,-.04,.13)),p+V((0,.04,.20)),p+V((-side*.13,.09,.13))],'blue_light' if layer==0 else 'blue','chest')
    c.ribbon('Belting / slanted waist',(-.28,1.03,.16),(.25,1.14,.18),.065,'leather_light','hips')
    for side in (-1,1):c.pouch((side*.25,1.02,.15))
    c.jewel('Mask / pale central rune',c.p('head')+V((0,.18,.216)),(.015,.06,.009),'blue_light','head')
    for x in (-.17,0,.17):c.panel('Tabard / torn front strip',[(x-.05,1.04,.21),(x+.05,1.04,.21),(x+.06,.45,.29),(x,.28,.30),(x-.04,.46,.29)],'blue_light' if x==0 else 'blue','hips')
    c.tube('Dagger / leather grip',(.31,.95,.06),(.36,.77,.10),.026,.025,'leather','hips',6)
    c.panel('Dagger / sheath',[(.33,.80,.12),(.39,.79,.12),(.47,.35,.16),(.41,.40,.16)],'steel','hips',.025)

def jester(c):
    c.body('teal');c.face('mask');c.skirt('linen',.38,True)
    h=c.p('head')
    c.loft('Cap / fitted cloth crown',[(h+V((0,.12,-.04)),.19,.16),(h+V((0,.34,-.04)),.22,.18),(h+V((0,.45,-.04)),.10,.13)],'teal','head',8,accents=['red'])
    for side in (-1,1):
        mat='teal' if side<0 else 'red'
        points=[h+V((side*.10,.36,-.02)),h+V((side*.36,.51,-.02)),h+V((side*.62,.36,-.02)),h+V((side*.76,.03,0))]
        for i in range(3):c.tube('Cap / bent horn '+str(side)+str(i),points[i],points[i+1],[.19,.15,.085][i],[.15,.085,.006][i],mat,'head',5)
        for drop in range(2):c.jewel('Cap / hanging gold bells',points[-1]-V((0,.055+drop*.073,0)),(.04,.045,.035),'gold_light','head')
        s='l' if side<0 else 'r';a,b=c.p('upperarm.'+s),c.p('lowerarm.'+s)
        c.tube('Sleeve / striped upper puff',a,b,.18,.17,mat,'upperarm.'+s,8)
        puff=c.tube('Sleeve / large lower puff',b,mix(b,c.p('hand.'+s),.8),.18,.105,'linen','lowerarm.'+s,8)
        puff.data.materials.append(MATS[mat])
        for poly in puff.data.polygons:poly.material_index=1 if (poly.index-2)%8 in (0,1,4,5) else 0
        c.panel('Mask / long diamond eye',[h+V((side*.035,.26,.152)),h+V((side*.11,.30,.13)),h+V((side*.14,.22,.14)),h+V((side*.075,.12,.171))],'black','head')
        c.panel('Tabard / harlequin colour',[ (side*.02,1.02,.21),(side*.18,1.02,.19),(side*.35,.43,.26),(side*.24,.29,.30),(side*.12,.47,.30)],mat,'hips')
        for y in (.55,.77,1.0):c.jewel('Tabard / brass bobble',(side*.18,y,.29 if y<.8 else .21),(.03,.04,.025),'gold','hips')
        foot=c.p('foot.'+s);c.tube('Shoe / curled toe',foot+V((0,-.07,.16)),foot+V((0,-.01,.34)),.063,.012,'black','foot.'+s,5)
    for i in range(12):
        a=TAU*i/12;b=TAU*(i+1)/12
        c.mesh('Ruff / pleated ivory fan '+str(i),[(math.cos(a)*.12,1.77,math.sin(a)*.10),(math.cos(a)*.38,1.56,math.sin(a)*.29),(math.cos(b)*.38,1.56,math.sin(b)*.29),(math.cos(b)*.12,1.77,math.sin(b)*.10)],[(0,1,2),(0,2,3)],'cream' if i%2 else 'linen_shadow','chest')
    for y in (1.23,1.36,1.49):c.jewel('Bodice / gold button',(0,y,.20),(.022,.025,.015),'gold','spine')

def wolf(c):
    c.body('fur',True);h=c.p('head')
    c.loft('Wolf / cranial planes',[(h+V((0,-.015,0)),.14,.13),(h+V((0,.15,0)),.25,.22),(h+V((0,.35,-.02)),.19,.18),(h+V((0,.44,-.03)),.10,.10)],'fur','head',8,accents=['fur_light'])
    c.loft('Wolf / long powerful muzzle',[(h+V((0,.08,.20)),.13,.14),(h+V((0,.19,.22)),.15,.17),(h+V((0,.23,.24)),.115,.12)],'linen_shadow','head',6)
    c.jewel('Wolf / black leather nose',h+V((0,.18,.378)),(.095,.052,.033),'black','head')
    c.panel('Wolf / open jaw line',[h+V((-.13,.09,.29)),h+V((.13,.09,.29)),h+V((.08,.035,.32)),h+V((-.08,.035,.32))],'black','head')
    for side in (-1,1):
        c.mesh('Wolf / pointed ear',[h+V((side*.13,.30,-.06)),h+V((side*.25,.64,-.05)),h+V((side*.29,.31,.045)),h+V((side*.22,.37,-.14))],[(0,1,2),(0,3,1),(1,3,2),(0,2,3)],'fur_dark','head',accents=['fur_light'])
        c.panel('Wolf / inner ear',[h+V((side*.18,.35,.015)),h+V((side*.245,.55,-.015)),h+V((side*.25,.35,.045))],'black','head')
        c.ribbon('Wolf / brow ridge',h+V((side*.04,.295,.20)),h+V((side*.20,.25,.15)),.05,'fur_dark','head')
        c.jewel('Wolf / crimson eye',h+V((side*.12,.23,.205)),(.031,.024,.015),'blood_eye','head')
        c.tube('Wolf / canine',h+V((side*.095,.115,.315)),h+V((side*.075,.04,.33)),.022,.002,'cream','head',5)
    # Individual pointed fur locks form a directional silhouette rather than round lumps.
    for row in range(5):
        y=1.79-row*.105;span=.20+row*.035
        for col in range(7):
            x=(col-3)*span/3;z=-.15-abs(x)*.17
            c.mesh('Mane / layered back lock '+str(row)+'-'+str(col),[(x-.075,y+.08,z),(x+.075,y+.08,z),(x+(col-3)*.025,y-.13,z-.055),(x,y+.02,z-.08)],[(0,1,3),(0,3,2),(1,2,3),(0,2,1)],'fur_light' if (row+col)%3==0 else 'fur','chest')
    for side in (-1,1):
        for row in range(4):
            x=side*(.20+row*.045);y=1.77-row*.085
            c.mesh('Mane / cheek and shoulder blade',[(x-side*.09,y+.08,.03),(x+side*.05,y+.04,-.04),(x+side*.17,y-.12,.01),(x,y-.015,.12)],[(0,1,3),(1,2,3),(2,0,3),(0,2,1)],'fur_light' if row%2==0 else 'fur','chest')
        chest=[(side*.015,1.66,.20),(side*.15,1.70,.20),(side*.33,1.61,.16),(side*.29,1.47,.19),(side*.08,1.45,.23),(side*.018,1.51,.24),(side*.16,1.57,.265)]
        c.mesh('Chest / anatomical pectoral planes',chest,[(i,(i+1)%6,6) for i in range(6)],'linen_shadow','chest',accents=['linen'])
        for row in range(3):
            y=1.39-row*.105;w=.19-row*.02
            c.mesh('Abdomen / integrated muscle planes',[(side*.015,y+.04,.205),(side*w,y+.03,.16),(side*w*.9,y-.045,.18),(side*.016,y-.05,.22),(side*.095,y,.235)],[(0,1,4),(1,2,4),(2,3,4),(3,0,4)],'linen_shadow','spine',accents=['linen'])
        s='l' if side<0 else 'r'
        for row in range(3):
            p=mix(c.p('lowerarm.'+s),c.p('hand.'+s),.2+row*.22)
            c.jewel('Forearm / fur blade',p+V((0,0,-.07)),(.085,.08,.10),'fur_light','lowerarm.'+s)
    for i in range(10):
        a=TAU*i/10;b=TAU*(i+1)/10
        c.panel('Loincloth / torn leather panel',[(math.cos(a)*.27,1.04,math.sin(a)*.19),(math.cos(b)*.27,1.04,math.sin(b)*.19),(math.cos(b)*.35,.67+(i%2)*.07,math.sin(b)*.26),(math.cos((a+b)/2)*.37,.54+(i%3)*.04,math.sin((a+b)/2)*.27),(math.cos(a)*.35,.68,math.sin(a)*.26)],'brown' if i%2 else 'brown_light','hips')
    tail=[(0,.90,-.16),(.05,.65,-.42),(.22,.43,-.68),(.48,.35,-.78),(.68,.42,-.82)]
    for i in range(4):c.tube('Tail / angular brush '+str(i),tail[i],tail[i+1],[.14,.17,.16,.10][i],[.17,.16,.10,.002][i],'fur_dark' if i==3 else 'fur','hips',7)

BUILDERS=[('Wayfarer','01 / Grey Wayfarer','Lumen',scholar),('DuskMage','02 / Dusk Mage','Echo',dusk),('Stalker','03 / Ashfang Werewolf','Stalker',wolf),('Wisp','04 / Crimson Harlequin','Wisp',jester),('Ember','05 / Woodland Ranger','Ember',ranger),('Lumen','06 / Ivory Prelate','Lumen',prelate),('Echo','07 / Earthbound Monk','Echo',monk)]

def export_game(c):
    joints=list(c.rig);positions=[];colors=[];indices=[];weights=[];parts=[]
    deps=bpy.context.evaluated_depsgraph_get()
    # Evaluate cloth thickness, keeping the armature in bind pose during the export.
    for obj in c.parts:
        if obj.get('display_only'):continue
        evaluated=obj.evaluated_get(deps);mesh=evaluated.to_mesh();mesh.calc_loop_triangles()
        first=len(positions)//3
        for tri in mesh.loop_triangles:
            color=obj.data.materials[mesh.polygons[tri.polygon_index].material_index].diffuse_color
            for vi in tri.vertices:
                v=mesh.vertices[vi];positions.extend(round(n,6) for n in U(v.co));colors.extend(round(n,5) for n in color)
                pairs=sorted([(joints.index(obj.vertex_groups[g.group].name),g.weight) for g in v.groups if obj.vertex_groups[g.group].name in joints],key=lambda p:-p[1])[:2]
                if not pairs:pairs=[(joints.index('spine'),1)]
                if len(pairs)==1:pairs.append((pairs[0][0],0))
                total=sum(p[1] for p in pairs);indices.extend(p[0] for p in pairs);weights.extend(round(p[1]/total,6) for p in pairs)
        parts.append({'name':obj.name,'first':first,'count':len(positions)//3-first});evaluated.to_mesh_clear()
    payload={'name':c.key,'bones':joints,'bindPositions':[round(v,6) for joint in joints for v in c.rig[joint]['position']],'positions':positions,'colors':colors,'joints':indices,'weights':weights,'parts':parts}
    path=OUT/(c.key+'.era-mesh.json');path.write_text(json.dumps(payload,separators=(',',':')),encoding='utf-8')
    return {'name':c.key,'triangles':len(positions)//9,'vertices':len(positions)//3,'details':len(c.parts),'sha256':hashlib.sha256(path.read_bytes()).hexdigest()}

def relaxed_pose(c):
    for side in (-1,1):
        s='l' if side<0 else 'r';pb=c.arm.pose.bones['upperarm.'+s]
        axis=pb.bone.matrix_local.inverted().to_3x3()@Vector((0,1,0));pb.rotation_mode='QUATERNION';pb.rotation_quaternion=Quaternion(axis,math.radians(side*72))
    bpy.context.view_layer.update()

def render_turnaround(c):
    scene=bpy.context.scene
    for char in CHARACTERS:char.collection.hide_render=char!=c
    relaxed_pose(c);deps=bpy.context.evaluated_depsgraph_get();temp=bpy.data.collections.new('Render / turnaround');scene.collection.children.link(temp)
    for view,angle in enumerate((0,math.pi/2,math.pi)):
        for obj in c.parts:
            eval_obj=obj.evaluated_get(deps);mesh=bpy.data.meshes.new_from_object(eval_obj,depsgraph=deps);copy=bpy.data.objects.new(obj.name+' / view',mesh);temp.objects.link(copy)
            copy.rotation_euler.z=angle;copy.location.x=(view-1)*1.65
    c.collection.hide_render=True
    scene.camera.location=(0,-10,2.9);aim(scene.camera,(0,0,1.25));scene.camera.data.type='ORTHO';scene.camera.data.ortho_scale=5.2
    scene.render.resolution_x=1500;scene.render.resolution_y=900;scene.render.filepath=str(PREVIEW/(c.key+'-turnaround.png'))
    bpy.ops.render.render(write_still=True)
    for obj in list(temp.objects):
        mesh=obj.data;bpy.data.objects.remove(obj,do_unlink=True);bpy.data.meshes.remove(mesh)
    bpy.data.collections.remove(temp)
    for pb in c.arm.pose.bones:pb.rotation_quaternion=Quaternion((1,0,0,0))

def aim(obj,target):obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()

# A fresh background process owns this scene; existing interactive Blender work is untouched.
for obj in list(bpy.data.objects):bpy.data.objects.remove(obj,do_unlink=True)
scene=bpy.context.scene;scene.render.engine='BLENDER_EEVEE';scene.render.image_settings.file_format='PNG';scene.render.film_transparent=False
scene.world.use_nodes=True;nodes=scene.world.node_tree.nodes;links=scene.world.node_tree.links
ambient=nodes['Background'];ambient.inputs['Color'].default_value=(.82,.85,.88,1);ambient.inputs['Strength'].default_value=.35
paper=nodes.new('ShaderNodeBackground');paper.inputs['Color'].default_value=(.96,.97,.95,1)
path=nodes.new('ShaderNodeLightPath');blend=nodes.new('ShaderNodeMixShader');links.new(path.outputs['Is Camera Ray'],blend.inputs[0]);links.new(ambient.outputs[0],blend.inputs[1]);links.new(paper.outputs[0],blend.inputs[2]);links.new(blend.outputs[0],nodes['World Output'].inputs['Surface'])
for name,position,power,size in [('Key',(-3,-4,6),450,5),('Fill',(4,-2,4),200,4),('Rim',(1,4,5),300,3)]:
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=size
    obj=bpy.data.objects.new(name,data);scene.collection.objects.link(obj);obj.location=position;aim(obj,(0,0,1.2))
scene.display.shading.light='STUDIO';scene.display.shading.studiolight_rotate_z=.35;scene.display.shading.color_type='MATERIAL'
scene.display.shading.show_shadows=True;scene.display.shading.show_cavity=False
scene.display.shading.background_type='WORLD';scene.view_settings.view_transform='Standard'
camera_data=bpy.data.cameras.new('Workshop / Camera');camera=bpy.data.objects.new('Workshop / Camera',camera_data);scene.collection.objects.link(camera);scene.camera=camera
CHARACTERS=[];report=[]
for key,label,rig_source,builder in BUILDERS:
    c=Character(key,label,rig_source);CHARACTERS.append(c);builder(c);bpy.context.view_layer.update()
    report.append(export_game(c));print('ERA_CHARACTER',json.dumps(report[-1]),flush=True)
for c in CHARACTERS:
    relaxed_pose(c)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in list(c.collection.objects):obj.select_set(True)
    # Native export has editable named parts, armature and skinning. Runtime uses the bind-pose JSON.
    for pb in c.arm.pose.bones:pb.rotation_quaternion=Quaternion((1,0,0,0))
    bpy.ops.export_scene.fbx(filepath=str(SOURCE/(c.key+'.fbx')),use_selection=True,add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',mesh_smooth_type='FACE')
    render_turnaround(c)
for index,c in enumerate(CHARACTERS):
    c.collection.hide_render=False;c.root.location=((index%4-1.5)*2.2,(index//4)*3.5,0);relaxed_pose(c)
camera.location=(7,-13,10);aim(camera,(0,1.6,1.1));camera.data.ortho_scale=11.7
scene.render.resolution_x=1920;scene.render.resolution_y=1300;scene.render.filepath=str(PREVIEW/'ERA-seven-characters.png');bpy.ops.render.render(write_still=True)
scene['ERA Workshop']='Seven editable flat shaded characters. Runtime bind meshes in Assets/Era/Art/BlenderCharacters. Existing KayKit animation rig retained in game.'
for area in bpy.context.screen.areas if bpy.context.screen else []:
    if area.type=='VIEW_3D':
        area.spaces.active.shading.color_type='MATERIAL';area.spaces.active.overlay.show_overlays=False
        area.spaces.active.region_3d.view_distance=12;area.spaces.active.region_3d.view_location=(0,1.5,1)
bpy.ops.object.select_all(action='DESELECT');CHARACTERS[4].arm.select_set(True);bpy.context.view_layer.objects.active=CHARACTERS[4].arm
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'ERA_Character_Workshop-v17.blend'))
(SOURCE/'character-manifest.json').write_text(json.dumps({'blender':bpy.app.version_string,'characters':report},indent=2),encoding='utf-8')
print('ERA_WORKSHOP_OK',flush=True)
