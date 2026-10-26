"""Read-only Blender audit of baked runtime poses from Test-ArcherMotion.

Run: blender --background --factory-startup --python Tools/Audit-ArcherContacts.py
No source scene is loaded, changed or saved. No extra packages are needed.
Surface crossings are evidence of contact, not exact penetration depth.
"""
import json, sys
from collections import Counter
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree

root = Path(__file__).resolve().parents[1]
folder = root / 'Artifacts' / 'ArcherMotion-30'
if '--poses' in sys.argv:folder=Path(sys.argv[sys.argv.index('--poses')+1]).resolve()
pairs = [('Cloak', part) for part in ['Leg_L', 'Leg_R', 'Quiver_Case', 'Harness']]
pairs += [('Bow_Wood', part) for part in ['Body_Head', 'Hood', 'Cloak', 'Clothing_Tunic_Coat', 'Leg_L', 'Leg_R']]
pairs += [(part, 'Hood') for part in ['Arm_L', 'Arm_R']]
pairs += [('Quiver_Case', part) for part in ['Shoulder_Armor_L', 'Shoulder_Armor_R']]
pairs += [(part, 'Scarf') for part in ['Shoulder_Armor_L', 'Shoulder_Armor_R']]
report = {'method': 'BVH triangle surface intersections, epsilon=0, real combined Player poses. '
          'Open/intersecting garment shells can have intentional contacts. '
          'This is a sampled audit, not a continuous collision guarantee.', 'samples': [], 'contacts': []}
def penetration(vertices, triangles, indices, target):
    """Approximate interior sample depth; meaningful only for closed shells."""
    depth = 0
    for index in set(indices):
        a, b, c = [vertices[i] for i in triangles[index]]
        for p in [a, b, c, (a+b+c)/3, (a+b)/2, (a+c)/2, (b+c)/2]:
            inside = True
            for direction in [Vector((.8391,.4217,.3521)).normalized(), Vector((-.262,.928,.265)).normalized()]:
                origin = p.copy(); count = 0
                for _ in range(35):
                    hit, _, _, _ = target.ray_cast(origin, direction, 10)
                    if hit is None: break
                    count += 1; origin = hit + direction*.000002
                if count % 2 == 0: inside = False; break
            if inside:
                _, _, _, distance = target.find_nearest(p)
                depth = max(depth, distance)
    return depth
valid_counts={}
runtime_report=folder.parent/'refinement-runtime.json'
if runtime_report.exists():valid_counts={p['name']:p['recordedFrames'] for p in json.loads(runtime_report.read_text(encoding='utf8'))['phases']}
for path in sorted(folder.glob('*-surfaces.json')):
    if valid_counts:
        phase,index=path.stem.removesuffix('-surfaces').rsplit('-',1)
        if int(index)>valid_counts.get(phase,0):continue
    surfaces = json.loads(path.read_text(encoding='utf-8'))['surfaces']
    trees = {}; geometry = {}
    for surface in surfaces:
        vertices = [Vector((v['x'], v['y'], v['z'])) for v in surface['vertices']]
        ids = surface['triangles']
        triangles = [tuple(ids[i:i+3]) for i in range(0, len(ids), 3)]
        trees[surface['name']] = BVHTree.FromPolygons(vertices, triangles, all_triangles=True, epsilon=0)
        geometry[surface['name']] = (vertices, triangles)
    sample = {'pose': path.stem.replace('-surfaces', ''), 'pairs': {}}
    for a, b in pairs:
        if a not in trees or b not in trees:
            raise ValueError('Missing surface: ' + a + ' / ' + b)
        hits = trees[a].overlap(trees[b])
        sample['pairs'][a + ' / ' + b] = len(hits)
        if hits:
            contact = {'pose': sample['pose'], 'pair': a + ' / ' + b, 'trianglePairs': len(hits)}
            if (a == 'Cloak' and b.startswith('Leg')) or a.startswith('Arm'):
                av, at = geometry[a]; bv, bt = geometry[b]
                contact['approxSampleDepthMeters'] = max(penetration(av, at, [i for i,j in hits], trees[b]), penetration(bv, bt, [j for i,j in hits], trees[a]))
            report['contacts'].append(contact)
    report['samples'].append(sample)
report['sampleCount'] = len(report['samples'])
report['byPair'] = dict(Counter(item['pair'] for item in report['contacts']))
(folder / 'surface-contacts.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print('ARCHER_RUNTIME_CONTACTS', json.dumps({'sampleCount': report['sampleCount'], 'byPair': report['byPair']}))
