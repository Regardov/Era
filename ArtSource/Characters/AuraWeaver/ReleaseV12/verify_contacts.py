"""Measure the actual evaluated boot soles during each stance phase."""
import bpy,sys,json
from pathlib import Path
HERE=Path(__file__).resolve().parent;SOURCE=HERE.parent
sys.path.insert(0,str(SOURCE))
from animate_aura_weaver import set_action
bpy.ops.wm.open_mainfile(filepath=str(HERE/'AuraWeaver_Lumen_V12.blend'))
r=bpy.data.objects['Archer_Rig'];scene=bpy.context.scene;results=[]
for name in ['Walk','Run']:
    action=bpy.data.actions['AuraWeaver_'+name];set_action(r,action);first,last=map(int,action.frame_range);samples=[]
    for frame in range(first,last):
        scene.frame_set(frame);deps=bpy.context.evaluated_depsgraph_get()
        for side,offset in [('L',0),('R',.5)]:
            phase=((frame-first)/(last-first)+offset)%1
            if phase>=(.56 if name=='Run' else .62):continue
            boot=bpy.data.objects['AW_Boot_'+side].evaluated_get(deps);mesh=boot.to_mesh()
            samples.append(min((boot.matrix_world@v.co).z for v in mesh.vertices));boot.to_mesh_clear()
    row={'animation':name,'samples':len(samples),'minPlantedSoleZ':min(samples),'maxPlantedSoleZ':max(samples)}
    assert row['minPlantedSoleZ']>-.012 and row['maxPlantedSoleZ']<.012,row
    results.append(row)
(HERE/'contacts.json').write_text(json.dumps({'ok':True,'scope':'Blender evaluated stance soles; runtime terrain and garment clearance are tested separately','results':results},indent=2))
print('ERA_STANCE_CONTACTS_OK',results)
