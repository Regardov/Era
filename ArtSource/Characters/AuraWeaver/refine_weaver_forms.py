"""Reference comparison corrections shared by the MCP checkpoint and final build."""
from mathutils import Vector
import bpy


def refine_large_forms(B,rig):
    # The reference hood ends in a cloth point, not a round helmet on the nape.
    for name in ['AW_Hood','AW_HoodRear']:
        o=bpy.data.objects[name]
        for v in o.data.vertices:
            if v.co.y>.11 and v.co.z<1.50:
                v.co.z-=.045*max(0,1-abs(v.co.x)/.14)
    # Overlapping rear seam; the front remains open over the charcoal inner layer.
    for label in ['Back.L','Back.R']:
        o=bpy.data.objects['AW_Coat_'+label]
        for v in o.data.vertices:
            if abs(v.co.x)<.060:
                v.co.x*=-.12
                if label.endswith('L'):v.co.y+=.0015
    def weights(p,front=True):
        prefix='Front' if front else 'Back'
        if p.z>.94:return {'Pelvis':1}
        t=max(0,min(1,(p.z-.34)/.40))
        return {'Coat1.'+prefix+'.L':t*.5,'Coat1.'+prefix+'.R':t*.5,
                'Coat2.'+prefix+'.L':(1-t)*.5,'Coat2.'+prefix+'.R':(1-t)*.5}
    for rear in [False,True]:
        rows=[]
        for z,w in [(1.014,.133),(.80,.153),(.55,.189),(.32,.225),(.12,.254)]:
            y=(.128 if rear else -.128)*(1+.36*(1-z));rows.append([(-w,y,z),(0,y+(.007 if rear else -.007),z), (w,y,z)])
        o=B.folded_panel('AW_InnerRobe_'+('Back' if rear else 'Front'),rows,['charcoal']*4,.007,rear)
        B.weight(o,lambda p,rear=rear:weights(p,not rear))
    rows=[ [(-.020,-.147,.974),(0,-.152,.974),(.020,-.147,.974)],
           [(-.027,-.165,.73),(0,-.172,.73),(.027,-.165,.73)],
           [(-.035,-.185,.43),(0,-.192,.43),(.035,-.185,.43)],
           [(-.033,-.199,.16),(0,-.206,.125),(.033,-.199,.16)]]
    B.weight(B.folded_panel('AW_CentralTabard',rows,['honey','amber','ochre'],.007),weights)
    for side,s in [('L',1),('R',-1)]:
        rows=[[(s*.107,-.068,1.678),(s*.142,-.063,1.674),(s*.171,-.057,1.663)],
              [(s*.113,-.098,1.489),(s*.157,-.080,1.464),(s*.197,-.052,1.472)],
              [(s*.126,-.132,1.288),(s*.158,-.119,1.304),(s*.185,-.093,1.315)]]
        o=B.folded_panel('AW_HoodLapel_'+side,rows,['ivory','ivory'],.008)
        B.weight(o,lambda p: {'Head':max(0,min(1,(p.z-1.34)/.22)),'Chest':1-max(0,min(1,(p.z-1.34)/.22))})
        # A broad geometric ochre lining follows the opening, rather than a thin frame.
        path=[(row[0][0],row[0][1]-.003,row[0][2]) for row in rows]
        o=B.ribbon('AW_HoodLapelEdge_'+side,path,.012,'honey','Head',.003)
        B.weight(o,lambda p: {'Head':max(0,min(1,(p.z-1.34)/.22)),'Chest':1-max(0,min(1,(p.z-1.34)/.22))})
