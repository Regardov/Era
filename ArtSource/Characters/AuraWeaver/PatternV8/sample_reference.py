"""External Python + Pillow: reproducible mirrored color-family reference guide.
Does not generate a model, a texture, or actual material values from sunlight.
"""
from pathlib import Path
import json,sys
from PIL import Image
HERE=Path(__file__).resolve().parent
im=Image.open(HERE.parent/'ReferenceV6/TargetReference.png').convert('RGB')
colors=['FCEABD','F4D59F','E4BD82','EDBF7C','D4A662','E59F3C','CE8B32','D98B28','B96C25','944F23','6A3D27','492C22','37241F']
palette=[tuple(int(h[i:i+2],16) for i in (0,2,4)) for h in colors];W,H=96,160;regions={}
def sample(x,y):
    x=max(1,min(im.width-2,int(x)));y=max(1,min(im.height-2,int(y)))
    return tuple(sum(im.getpixel((x+dx,y+dy))[c] for dx in [-1,0,1] for dy in [-1,0,1])/9 for c in range(3))
def classify(c):return min(range(len(palette)),key=lambda i:sum((c[k]-palette[i][k])**2 for k in range(3)))
for region in ['front','back','sleeve']:
    grid=[]
    for row in range(H):
        t=1-(row+.5)/H;line=[]
        for column in range(W):
            u=(column+.5)/W
            if region in ['front','back']:
                cx=1263 if region=='front' else 900;half=151-102*t;y=944-492*t
                pair=[sample(cx+s*u*half,y) for s in [-1,1]]
            else:
                y=551-162*t;left=1136+22*t;half=43-22*t
                pair=[sample(left+(u-.5)*2*half,y),sample(2526-left-(u-.5)*2*half,y)]
            role=classify(tuple(sum(v[k] for v in pair)/2 for k in range(3)))
            if region in ['front','back']:
                if t>.64:role=min(role,3)
                if region=='front' and u<.19:role=5 if t<.65 else 1
                if region=='back' and u<.055:role=5
            elif t>.42:role=min(role,4)
            if region in ['front','back']:
                if t>.64 and role>=3:role=1
                if t<.10 and u>.06:role=max(role,10)
                if region=='front' and t>.28 and role>=10:role=6
            line.append(role)
        grid.append(line)
    regions[region]=grid
path=HERE/'reference_atlas.json'
if '--check' in sys.argv:
    assert regions==json.loads(path.read_text(encoding='utf8'))['regions'];print('REFERENCE_GUIDE_REPRODUCED')
else:
    data={'width':W,'height':H,'quantization_srgb':colors,'regions':regions,
          'method':'Mirrored illustration samples quantized into color families; bag, belt, central opening and background occlusions masked. Local shader palette is separately defined in pattern_variants.py.'}
    path.write_text(json.dumps(data,separators=(',',':')),encoding='utf8')
    canvas=Image.new('RGB',(W*3,H))
    for k,grid in enumerate(regions.values()):
        for y,row in enumerate(grid):
            for x,v in enumerate(row):canvas.putpixel((k*W+x,y),palette[v])
    canvas.resize((864,480),Image.Resampling.NEAREST).save(HERE/'Reference-Family-Atlas.png')
