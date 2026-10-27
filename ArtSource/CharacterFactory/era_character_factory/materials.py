"""Shared local palette, with explicit sRGB -> linear conversion. Never sample lighting."""
import bpy
from .components import require_copy


def srgb_to_linear(value):
    return value/12.92 if value <= .04045 else ((value+.055)/1.055)**2.4


def linear_to_srgb(value):
    return 12.92*value if value <= .0031308 else 1.055*value**(1/2.4)-.055


def hex_from_linear(rgba):
    return '#'+''.join(f'{round(max(0, min(1, linear_to_srgb(c)))*255):02X}' for c in rgba[:3])


def material_from_hex(name, color, roughness=.85, metallic=0):
    color = color.lstrip('#')
    if len(color) != 6:
        raise ValueError('Expected six-digit sRGB hex')
    rgba = [srgb_to_linear(int(color[i:i+2], 16)/255) for i in (0, 2, 4)]+[1]
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    material.diffuse_color = rgba
    node = material.node_tree.nodes.get('Principled BSDF')
    node.inputs['Base Color'].default_value = rgba
    node.inputs['Roughness'].default_value = roughness
    node.inputs['Metallic'].default_value = metallic
    return material


def replace_material(objects, old_name, new_material):
    for obj in objects:
        require_copy(obj)
        for slot in obj.material_slots:
            if slot.material and slot.material.name == old_name:
                slot.material = new_material


def assign_faces(obj, material, indices):
    require_copy(obj)
    slot = next((i for i, m in enumerate(obj.data.materials) if m == material), None)
    if slot is None:
        slot = len(obj.data.materials)
        obj.data.materials.append(material)
    for i in indices:
        obj.data.polygons[i].material_index = slot


def compact_slots(obj):
    """Remove unused slots only, preserving each face's material identity."""
    require_copy(obj)
    assigned = [obj.data.materials[p.material_index] for p in obj.data.polygons]
    unique = list(dict.fromkeys(assigned))
    obj.data.materials.clear()
    for material in unique:
        obj.data.materials.append(material)
    for polygon, material in zip(obj.data.polygons, assigned):
        polygon.material_index = unique.index(material)


def reuse_palette_slots(obj, existing):
    """Reuse an identical tagged flat palette material, never a recolored user copy."""
    def signature(material):
        if not material or not material.get('era_palette_key') or not material.use_nodes:
            return None
        node = material.node_tree.nodes.get('Principled BSDF')
        if not node or any(node.inputs[k].is_linked for k in ['Base Color','Roughness','Metallic']):
            return None
        return (material['era_palette_key'], tuple(node.inputs['Base Color'].default_value),
                node.inputs['Roughness'].default_value, node.inputs['Metallic'].default_value, tuple(material.diffuse_color))
    targets = {signature(m):m for m in existing if signature(m) is not None}
    for slot in obj.material_slots:
        key = signature(slot.material)
        if key in targets:
            slot.material = targets[key]
