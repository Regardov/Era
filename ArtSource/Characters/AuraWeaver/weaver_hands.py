"""Connected low-poly palms/fingers, with independent two-segment digit bones."""
import bpy
from mathutils import Vector


def build_hands(B,rig):
    results=[]
    for side,sign in [('L',1),('R',-1)]:
        hand=rig.data.bones['Hand.'+side];origin=hand.head_local.copy();d=(hand.tail_local-origin).normalized()
        u=Vector((sign,0,0));u=(u-d*u.dot(d)).normalized();normal=d.cross(u).normalized()
        xs=[-.028,-.026,-.015,-.013,-.002,.001,.012,.014,.025,.028]
        v=[];weights=[];faces=[];cols=len(xs)
        def add(p,w):v.append(p);weights.append(w);return len(v)-1
        def point(x,t,n):return origin+u*x+d*t+normal*n
        for layer in range(2):
            for row,(distance,scale,thickness) in enumerate([(0,.55,.008),(.026,.91,.009),(.060,1,.008)]):
                for x in xs:add(point(x*scale,distance,thickness*(1 if layer==0 else -1)),{'Hand.'+side:1})
        def ix(layer,row,col):return layer*3*cols+row*cols+col
        for layer in range(2):
            for r in range(2):
                for j in range(cols-1):
                    q=(ix(layer,r,j),ix(layer,r,j+1),ix(layer,r+1,j+1),ix(layer,r+1,j));faces.append(q if layer==0 else tuple(reversed(q)))
        # Wrist cap and palm side walls. One side quad is extruded into the thumb.
        for j in range(cols-1):faces.append((ix(0,0,j+1),ix(0,0,j),ix(1,0,j),ix(1,0,j+1)))
        for r in range(2):
            if r!=1:faces.append((ix(0,r,0),ix(0,r+1,0),ix(1,r+1,0),ix(1,r,0)))
            faces.append((ix(0,r+1,cols-1),ix(0,r,cols-1),ix(1,r,cols-1),ix(1,r+1,cols-1)))
        roots=[(1,2,'Index',.060),(3,4,'Middle',.066),(5,6,'Ring',.060),(7,8,'Little',.046)]
        for j in range(cols-1):
            if not any(j==a for a,b,n,l in roots):faces.append((ix(0,2,j),ix(0,2,j+1),ix(1,2,j+1),ix(1,2,j)))
        bone_specs=[]
        for a,b,name,length in roots:
            root=[ix(0,2,a),ix(0,2,b),ix(1,2,b),ix(1,2,a)];center=(xs[a]+xs[b])/2
            joint=point(center,.060+length*.52,0);tip=point(center,.060+length,0)
            names=('Digit_'+name+'1.'+side,'Digit_'+name+'2.'+side)
            bone_specs.extend([(names[0],point(center,.060,0),joint,'Hand.'+side),(names[1],joint,tip,names[0])])
            prev=root
            for k,t in enumerate([.060+length*.48,.060+length*.83,.060+length]):
                width=(xs[b]-xs[a])*.5*(.92 if k<2 else .66);depth=.007*(1 if k==0 else .83)
                w={names[0]:.6,names[1]:.4} if k==0 else {names[1]:1}
                ring=[add(point(center-width,t,depth),w),add(point(center+width,t,depth),w),add(point(center+width,t,-depth),w),add(point(center-width,t,-depth),w)]
                for i in range(4):faces.append((prev[i],prev[(i+1)%4],ring[(i+1)%4],ring[i]))
                prev=ring
            faces.append(tuple(prev))
        thumb=[ix(0,1,0),ix(0,2,0),ix(1,2,0),ix(1,1,0)]
        base=sum((v[i] for i in thumb),Vector())/4;joint=base-u*.022+d*.014;tip=base-u*.041+d*.034
        names=('Digit_Thumb1.'+side,'Digit_Thumb2.'+side)
        bone_specs.extend([(names[0],base,joint,'Hand.'+side),(names[1],joint,tip,names[0])])
        prev=thumb
        for k,c in enumerate([joint,tip]):
            w={names[0]:.5,names[1]:.5} if k==0 else {names[1]:1};r=.008 if k==0 else .006
            ring=[add(c+d*(-r if i in [0,3] else r)+normal*(r if i<2 else -r),w) for i in range(4)]
            for i in range(4):faces.append((prev[i],prev[(i+1)%4],ring[(i+1)%4],ring[i]))
            prev=ring
        faces.append(tuple(prev))
        bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
        for name,head,tail,parent in bone_specs:
            bone=rig.data.edit_bones.new(name);bone.head=head;bone.tail=tail;bone.parent=rig.data.edit_bones[parent];bone.use_deform=True
        bpy.ops.object.mode_set(mode='OBJECT')
        obj=B.mesh('AW_ConnectedGlove_'+side,v,faces,'charcoal')
        for i,w in enumerate(weights):
            for name,value in w.items():
                group=obj.vertex_groups.get(name) or obj.vertex_groups.new(name=name);group.add([i],value,'REPLACE')
        results.append({'side':side,'vertices':len(v),'bones':[x[0] for x in bone_specs],'construction':'Palm, all four fingers and thumb share the same boundary vertices; no separate floating fingers.'})
    return results
