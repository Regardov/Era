"""ERA_Humanoid54_v1 compatibility and weight checks. Generic, not Humanoid."""
import bpy
from .audit import skeleton_payload, action_payload, digest
from .components import require_copy

SCHEMA = 'ERA_Humanoid54_v1'
CORE = {'Root','Pelvis','Spine','Chest','Neck','Head'} | {n+'.'+s for s in ['L','R'] for n in ['Clavicle','UpperArm','Forearm','Hand','Fingers','Thigh','Shin','Foot','Toe']}


def compatibility(rig, schema, tolerance=1e-5):
    current = {b['name']: b for b in skeleton_payload(rig)}
    expected = {b['name']: b for b in schema['skeleton']}
    missing, extra = sorted(expected.keys()-current.keys()), sorted(current.keys()-expected.keys())
    errors = []
    for name in current.keys() & expected.keys():
        a, b = current[name], expected[name]
        if a['parent'] != b['parent']:
            errors.append(name+': parent changed')
        if max(abs(a['matrix'][i][j]-b['matrix'][i][j]) for i in range(4) for j in range(4)) > tolerance:
            errors.append(name+': rest matrix changed')
    return {'schema': SCHEMA, 'compatible': not missing and not extra and not errors and not rig.get('era_requires_rebake', False),
            'missing': missing, 'extra': extra, 'errors': errors}


def weight_report(rig, meshes, tolerance=1e-4):
    report = []
    for obj in meshes:
        deform = {g.index for g in obj.vertex_groups if g.name in rig.data.bones and rig.data.bones[g.name].use_deform}
        counts, bad, unweighted = [], [], []
        for vertex in obj.data.vertices:
            values = [g.weight for g in vertex.groups if g.group in deform and g.weight > 1e-6]
            counts.append(len(values))
            if not values:
                unweighted.append(vertex.index)
            elif abs(sum(values)-1) > tolerance:
                bad.append(vertex.index)
        report.append({'object': obj.name, 'max_influences': max(counts, default=0),
                       'unweighted': unweighted, 'non_normalized': bad})
    return report


def load_actions(filepath, names=None):
    with bpy.data.libraries.load(str(filepath), link=False) as (source, target):
        selected = list(names) if names else [n for n in source.actions if n.startswith('Archer_')]
        if any(n not in source.actions for n in selected):
            raise ValueError('Missing Action')
        target.actions = list(selected)
    return {original: action for original, action in zip(selected, target.actions)}


def bind_action(rig, action):
    require_copy(rig)
    if rig.get('era_requires_rebake', False):
        raise ValueError('Rest pose changed: rebake before direct Action reuse')
    rig.animation_data_create()
    rig.animation_data.action = action
    if len(action.slots) != 1:
        raise ValueError('Expected one compatible armature Action slot')
    rig.animation_data.action_slot = action.slots[0]


def action_bank_hash(actions):
    return {a.name: digest(action_payload(a)) for a in actions}
