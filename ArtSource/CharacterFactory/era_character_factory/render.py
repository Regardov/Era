"""Consistent orthographic/material/clay views in a temporary scene."""
from pathlib import Path
import bpy
from mathutils import Vector
from .qa import evaluated_bounds


def review_views(rig, meshes, folder, prefix, views=('front','side','back','perspective'), clay=False, resolution=600):
    folder = Path(folder)
    folder.mkdir(parents=True, exist_ok=True)
    current = bpy.context.window.scene
    scene = bpy.data.scenes.new('ERA_QA_TEMP')
    try:
        scene.collection.objects.link(rig)
        for obj in meshes:
            scene.collection.objects.link(obj)
        bpy.context.window.scene = scene
        scene.frame_set(current.frame_current, subframe=current.frame_subframe)
        box = evaluated_bounds(meshes)
        low, high = Vector(box['min']), Vector(box['max'])
        center, extent = (low+high)/2, max(box['size'])
        scene.render.engine = 'BLENDER_WORKBENCH'
        shading = scene.display.shading
        shading.light = 'STUDIO'
        shading.color_type = 'SINGLE' if clay else 'MATERIAL'
        shading.single_color = (.55,.55,.55)
        shading.show_shadows = True
        shading.show_cavity = True
        shading.cavity_type = 'BOTH'
        shading.background_type = 'WORLD'
        scene.world = bpy.data.worlds.new('ERA_QA_World')
        scene.world.color = (.12,.14,.16)
        scene.view_settings.view_transform = 'Standard'
        scene.render.resolution_x = resolution
        scene.render.resolution_y = resolution
        scene.render.resolution_percentage = 100
        scene.render.image_settings.file_format = 'PNG'
        camera = bpy.data.objects.new('ERA_QA_Camera', bpy.data.cameras.new('ERA_QA_Camera'))
        scene.collection.objects.link(camera)
        scene.camera = camera
        camera.data.lens = 55
        directions = {'front': (0,-1,0), 'side': (1,0,0), 'back': (0,1,0), 'perspective': (1,-2,.6)}
        outputs = []
        for view in views:
            camera.data.type = 'PERSP' if view == 'perspective' else 'ORTHO'
            camera.data.ortho_scale = extent*1.16
            camera.location = center+Vector(directions[view]).normalized()*extent*2.6
            camera.rotation_euler = (center-camera.location).to_track_quat('-Z','Y').to_euler()
            output = folder/f'{prefix}-{view}{"-clay" if clay else ""}.png'
            scene.render.filepath = str(output)
            bpy.ops.render.render(write_still=True)
            outputs.append(str(output))
        return outputs
    finally:
        bpy.context.window.scene = current
        world = scene.world
        cameras = [o for o in scene.objects if o.type == 'CAMERA']
        bpy.data.scenes.remove(scene)
        for camera in cameras:
            data = camera.data
            bpy.data.objects.remove(camera, do_unlink=True)
            bpy.data.cameras.remove(data)
        if world and world.users == 0:
            bpy.data.worlds.remove(world)
        current.frame_set(current.frame_current, subframe=current.frame_subframe)
