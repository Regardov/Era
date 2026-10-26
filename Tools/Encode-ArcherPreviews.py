"""Encode recorded Unity frames with Blender's bundled H.264 encoder.
blender --background --factory-startup --python Tools/Encode-ArcherPreviews.py
No extra libraries or changes to the user's live Blender file.
"""
import bpy
import json
from pathlib import Path

root=Path(__file__).resolve().parents[1]
frames=root/'Artifacts/ArcherRefinement-30'
report=json.loads((frames/'refinement-runtime.json').read_text(encoding='utf8'))
assert report['ok'],'Runtime validation must pass before encoding'
recorded={p['name']:p['recordedFrames'] for p in report['phases']}
destination=root/'Docs/Previews'
destination.mkdir(parents=True,exist_ok=True)
scene=bpy.context.scene
assert 'FFMPEG' in {i.identifier for i in bpy.types.ImageFormatSettings.bl_rna.properties['file_format'].enum_items}
assert 'H264' in {i.identifier for i in bpy.types.FFmpegSettings.bl_rna.properties['codec'].enum_items}
scene.render.image_settings.media_type='VIDEO'
scene.render.image_settings.file_format='FFMPEG'
scene.render.ffmpeg.format='MPEG4';scene.render.ffmpeg.codec='H264';scene.render.ffmpeg.constant_rate_factor='HIGH'
scene.render.resolution_x=640;scene.render.resolution_y=720;scene.render.resolution_percentage=100
scene.render.fps=20;scene.render.fps_base=1;scene.render.use_sequencer=True
scene.view_settings.view_transform='Standard'
editor=scene.sequence_editor_create()
movies={'Walk-Run':['SlowWalk','Normal','Run','Diagonal'], 'Jump-Air-Land':['IdleJump','RunJump','JumpShoot'], 'Run-Shoot':['RunShoot'], 'Quiver-Cloak':['BackView','BowDraw']}
for name,parts in movies.items():
    for strip in list(editor.strips):editor.strips.remove(strip)
    start=1
    for part in parts:
        files=[frames/part/(str(i).zfill(4)+'.png') for i in range(recorded[part])]
        assert all(p.is_file() for p in files),'Missing current-run frames: '+part
        if not files:raise RuntimeError('Record the real Player first: '+part)
        strip=editor.strips.new_image(name=part,filepath=str(files[0]),channel=1,frame_start=start)
        for file in files[1:]:strip.elements.append(file.name)
        strip.frame_final_duration=len(files);start+=len(files)
    scene.frame_start=1;scene.frame_end=start-1
    scene.render.filepath=str(destination/('Archer-'+name+'.mp4'))
    bpy.ops.render.render(animation=True)
    print('ARCHER_PREVIEW',name,scene.frame_end,scene.render.filepath)
