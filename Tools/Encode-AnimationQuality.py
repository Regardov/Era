"""Encode actual quality test captures using Blender's bundled H.264 only."""
import bpy,json
from pathlib import Path
root=Path(__file__).resolve().parents[1];dest=root/'Docs/AnimationQuality/Video';dest.mkdir(parents=True,exist_ok=True)
s=bpy.context.scene;s.render.image_settings.media_type='VIDEO';s.render.image_settings.file_format='FFMPEG';s.render.ffmpeg.format='MPEG4';s.render.ffmpeg.codec='H264';s.render.ffmpeg.constant_rate_factor='HIGH'
s.render.resolution_x=640;s.render.resolution_y=720;s.render.resolution_percentage=100;s.render.fps=20;s.view_settings.view_transform='Standard';s.render.use_sequencer=True
editor=s.sequence_editor_create()
for label in ['Before','After']:
 folder=root/f'Artifacts/AnimationQuality/{label}-30';report=json.loads((folder/'refinement-runtime.json').read_text());assert report['ok'];counts={p['name']:p['recordedFrames'] for p in report['phases']}
 for name in ['RunCast','RunCastShoot','TurnShoot','AirAim']:
  for strip in list(editor.strips):editor.strips.remove(strip)
  files=[folder/name/f'{i:04}.png' for i in range(counts[name])];assert files and all(p.exists() for p in files)
  strip=editor.strips.new_image(name=name,filepath=str(files[0]),channel=1,frame_start=1)
  for p in files[1:]:strip.elements.append(p.name)
  strip.frame_final_duration=len(files);s.frame_start=1;s.frame_end=len(files);s.render.filepath=str(dest/f'{label}-{name}.mp4')
  bpy.ops.render.render(animation=True);print('QUALITY_VIDEO',s.render.filepath)

# Same real prefab/timer fixtures before/after the ensemble presentation fixes.
for label in ['Before','After']:
 folder=root/f'Artifacts/AnimationQuality/Ensemble-{label}-30'
 if not (folder/'ensemble-runtime.json').exists():continue
 report=json.loads((folder/'ensemble-runtime.json').read_text(encoding='utf8'))
 counts={p['name']:p['recordedFrames'] for p in report['actors']}
 s.render.resolution_x=480;s.render.resolution_y=600;s.render.fps=10
 for name in ['StoneGolem','Boss5','Lumen','HiveMind']:
  for strip in list(editor.strips):editor.strips.remove(strip)
  files=[folder/name/f'{i:04}.png' for i in range(counts[name])];assert files and all(p.exists() for p in files)
  strip=editor.strips.new_image(name=name,filepath=str(files[0]),channel=1,frame_start=1)
  for p in files[1:]:strip.elements.append(p.name)
  strip.frame_final_duration=len(files);s.frame_start=1;s.frame_end=len(files);s.render.filepath=str(dest/f'{label}-{name}.mp4')
  bpy.ops.render.render(animation=True);print('QUALITY_VIDEO',s.render.filepath)
s.render.resolution_x=640;s.render.resolution_y=720;s.render.fps=20

# Runtime actions use the same GameApp actor/controller as the gameplay checks.
folder=root/'Artifacts/AnimationQuality/Actions'
if (folder/'archer-runtime.json').exists():
 report=json.loads((folder/'archer-runtime.json').read_text(encoding='utf8'));assert report['ok']
 counts={p['name']:p['recordedFrames'] for p in report['phases']}
 for name in ['Hit','Dash','Cast','Interact','Death']:
  for strip in list(editor.strips):editor.strips.remove(strip)
  files=[folder/'Frames'/name/f'{i:04}.png' for i in range(counts[name])];assert files and all(p.exists() for p in files)
  strip=editor.strips.new_image(name=name,filepath=str(files[0]),channel=1,frame_start=1)
  for p in files[1:]:strip.elements.append(p.name)
  strip.frame_final_duration=len(files);s.frame_start=1;s.frame_end=len(files);s.render.filepath=str(dest/f'After-{name}.mp4')
  bpy.ops.render.render(animation=True);print('QUALITY_VIDEO',s.render.filepath)
