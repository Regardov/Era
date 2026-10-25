"""Original ERA score, editable MIDI sources and an offline SoundFont rendering workflow.

Requires numpy, soundfile, FluidSynth 2.6.1 and GeneralUser GS 2.0.3.
No melodies or MIDI demos from the instrument bank are used.
"""
from __future__ import annotations
import argparse
import hashlib
import json
import math
from pathlib import Path
import random
import struct
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'Artifacts/MusicTools/python'))
import numpy as np
import soundfile as sf

RATE = 44100
PPQ = 480
BANK_SHA256 = '9575028c7a1f589f5770fccc8cff2734566af40cd26ed836944e9a5152688cfe'

# MIDI programs are zero based. Voices: lead, answer, arpeggio, strings, bass, brass/choir.
SCORES = [
    dict(file='Menu-Homeward-Light', title='Свет у порога', bpm=76, bars=40, beats=4, key=50,
         scale=[0,2,3,5,7,9,10], chords=[0,5,3,6,0,3,5,4],
         voices=[0,73,46,48,42,89], mood='menu',
         theme=[(2,1),(4,.5),(5,.5),(4,1),(1,1),(0,1.5),(2,.5),(1,1),(-1,1),
                (0,1),(2,1),(4,1),(6,.5),(5,.5),(4,1.5),(2,.5),(1,1),(0,1)],
         instruments='grand piano, concert harp, flute, warm strings, bowed bass, airy pad'),
    dict(file='Realm01-Mistveil', title='Роса туманных лугов', bpm=104, bars=48, beats=4, key=43,
         scale=[0,2,3,5,7,9,10], chords=[0,3,5,4,0,6,3,4],
         voices=[73,68,46,48,32,60], mood='meadow',
         theme=[(7,.5),(9,.5),(11,1),(9,.5),(8,.5),(7,1),(4,1),(5,.5),(7,.5),(8,1),(7,1),
                (9,1),(11,.5),(12,.5),(11,1),(9,1),(8,.5),(7,.5),(5,1),(4,1),(7,1)],
         instruments='flute, oboe, harp, legato strings, acoustic bass, soft horn, frame drums'),
    dict(file='Realm02-Amberwood', title='Танец янтарных ветвей', bpm=112, bars=56, beats=3, key=45,
         scale=[0,2,3,5,7,8,10], chords=[0,5,2,6,0,3,5,4],
         voices=[68,73,24,45,42,48], mood='amber',
         theme=[(7,.5),(9,.5),(11,.5),(9,.5),(8,.5),(7,.5),(5,1),(7,.5),(8,.5),(9,1),
                (11,1),(12,.5),(11,.5),(9,1),(8,.5),(7,.5),(5,1),(4,1)],
         instruments='oboe, flute, nylon guitar, pizzicato strings, cello, hand percussion'),
    dict(file='Realm03-Azure', title='Стеклянные вершины', bpm=96, bars=48, beats=4, key=40,
         scale=[0,2,3,5,7,9,10], chords=[0,6,3,4,0,2,5,4],
         voices=[8,73,46,49,42,91], mood='azure',
         theme=[(11,1.5),(9,.5),(7,2),(8,1),(11,1),(12,2),(14,1),(12,1),(11,1),(9,1),
                (8,1.5),(7,.5),(5,2)],
         instruments='celesta, flute, harp, slow strings, cello, bowed glass pad, bells'),
    dict(file='Realm04-Cinder', title='Пульс пепельной кузни', bpm=132, bars=64, beats=4, key=36,
         scale=[0,2,3,5,7,8,10], chords=[0,0,5,4,0,3,6,4],
         voices=[60,42,48,44,43,61], mood='cinder',
         theme=[(7,1),(7,.5),(9,.5),(10,1),(9,1),(7,1.5),(5,.5),(4,2),
                (7,1),(10,.5),(11,.5),(12,1),(11,1),(9,1.5),(8,.5),(7,2)],
         instruments='French horns, cello, string ostinato, tremolo strings, contrabass, brass, low toms'),
    dict(file='Realm05-Twilight', title='Фонари сумеречной топи', bpm=88, bars=48, beats=4, key=42,
         scale=[0,1,3,5,7,8,10], chords=[0,5,1,4,0,3,5,4],
         voices=[77,71,12,50,33,89], mood='mire',
         theme=[(7,1.5),(8,.5),(11,1),(10,1),(8,2),(7,1),(5,1),
                (4,1.5),(5,.5),(7,1),(8,1),(11,2),(8,1),(7,1)],
         instruments='shakuhachi, clarinet, marimba, low choir, fingered bass, suspended pad, heartbeat drums'),
    dict(file='Realm06-Stormpass', title='Шествие сквозь грозу', bpm=144, bars=64, beats=4, key=47,
         scale=[0,2,3,5,7,8,10], chords=[0,3,5,4,0,6,5,4],
         voices=[60,56,48,49,43,61], mood='storm',
         theme=[(7,1),(9,.5),(11,.5),(12,1),(11,1),(9,1),(7,1),(8,.5),(9,.5),(11,1),
                (12,1.5),(11,.5),(9,1),(8,1),(7,1.5),(5,.5),(4,2)],
         instruments='French horns, trumpet answers, driving strings, contrabass, brass, timpani and snare'),
    dict(file='Realm07-Rift', title='Сердце разлома', bpm=128, bars=64, beats=3.5, key=38,
         scale=[0,2,3,5,7,8,11], chords=[0,5,3,4,0,6,5,4],
         voices=[52,60,48,50,43,19], mood='rift',
         theme=[(7,1),(9,.5),(11,1),(10,1),(7,1.5),(6,.5),(4,1.5),
                (7,1),(10,.5),(12,1),(11,1),(9,1.5),(8,.5),(7,1.5)],
         instruments='choir, horns, urgent strings, low choir, contrabass, church organ, cinematic toms and cymbals'),
]

def varlen(n):
    data = [n & 127]
    while n >> 7:
        n >>= 7
        data.insert(0, (n & 127) | 128)
    return bytes(data)

def chunk(events):
    events.sort(key=lambda e: (e[0], e[1]))
    result = bytearray(); previous = 0
    for tick, priority, message in events:
        result.extend(varlen(tick - previous)); result.extend(message); previous = tick
    result.extend(b'\x00\xff\x2f\x00')
    return b'MTrk' + struct.pack('>I', len(result)) + result

def degree(score, n):
    octave, step = divmod(n, 7)
    return score['key'] + octave * 12 + score['scale'][step]

def write_midi(score, path, cycles=1):
    rng = random.Random(10687 + SCORES.index(score) * 7919)
    tracks = [[] for _ in range(8)]
    tempo = round(60000000 / score['bpm'])
    signature = (6, 3) if score['beats'] == 3 else (7, 3) if score['beats'] == 3.5 else (4, 2)
    title = score['title'].encode('utf-8')
    tracks[0] = [(0,0,b'\xff\x51\x03'+tempo.to_bytes(3,'big')),
                 (0,0,bytes([255,88,4,*signature,24,8])),
                 (0,0,b'\xff\x03'+varlen(len(title))+title)]
    channels = [0,1,2,3,4,5,9]
    volumes = [91,73,69,63,87,53,78]
    pans = [60,82,42,72,64,48,64]
    for voice, channel in enumerate(channels):
        track = tracks[voice+1]
        if voice < 6: track.append((0,0,bytes([0xc0+channel,score['voices'][voice]])))
        for controller,value in [(7,volumes[voice]),(10,pans[voice]),(91,48 if voice!=4 else 20),(93,12)]:
            track.append((0,0,bytes([0xb0+channel,controller,value])))

    def note(voice, pitch, beat, duration, velocity=75, human=True):
        channel=channels[voice]
        timing=rng.uniform(-.012,.012) if human else 0
        tick=max(0,round((beat+timing)*PPQ)); end=max(tick+1,round((beat+duration)*PPQ))
        velocity=max(20,min(113,round(velocity+rng.uniform(-5,5))))
        tracks[voice+1].append((tick,2,bytes([0x90+channel,max(0,min(127,pitch)),velocity])))
        tracks[voice+1].append((end,1,bytes([0x80+channel,max(0,min(127,pitch)),0])))

    bars=score['bars']; beats=score['beats']; mood=score['mood']; cycle_beats=bars*beats
    # The same performance repeats for warm-up and tail rendering, including human timing.
    first_state=rng.getstate()
    for cycle in range(cycles):
        rng.setstate(first_state)
        base=cycle*cycle_beats
        for bar in range(bars):
            start=base+bar*beats; phrase=bar//8
            root=score['chords'][(bar//2)%len(score['chords'])]
            chord=[degree(score,root+k) for k in (0,2,4,6)]
            intensity=[.50,.72,.93,.68,1,.85,1,.65][phrase%8]
            if mood=='menu': intensity=[.45,.65,.8,.6,.5][phrase]
            # Pads breathe through four-bar phrases instead of restarting on every beat.
            if bar%2==0:
                for i,p in enumerate(chord):
                    note(3,p+12,start+i*.035,beats*2-.07,round(43+intensity*15))
                if phrase in (2,4,5,6):
                    for p in chord[:3]: note(5,p+12,start+.02,beats*2-.10,45+intensity*12)
            bass_note=chord[0]-12
            if mood=='menu':
                note(4,bass_note,start,beats*1.85,54)
            elif mood in ('azure','mire'):
                note(4,bass_note,start,beats*.82,61)
                if bar%2: note(4,chord[0]-5,start+beats*.65,beats*.25,48)
            else:
                for b in range(math.ceil(beats)):
                    if b>=beats: break
                    note(4,bass_note if b%2==0 else bass_note+7,start+b,min(.70,beats-b),62+intensity*15)
            # Each location has its own accompaniment rhythm and instrumental identity.
            if mood in ('cinder','storm','rift'):
                count=round(beats*2)
                pattern=[0,2,1,2,0,3,2,1]
                for eighth in range(count):
                    pitch=chord[pattern[eighth%8]]+12
                    note(2,pitch,start+eighth*.5,.32,52+intensity*21+(7 if eighth%2==0 else 0))
                if phrase>=1:
                    for b in (0,beats/2):
                        for p in chord[:3]:note(5,p,start+b,min(.9,beats/2-.1),45+intensity*15)
            elif mood=='amber':
                for eighth in range(6):
                    note(2,chord[[0,2,1,3,2,1][eighth]]+12,start+eighth*.5,.64,55+intensity*12)
                    if eighth in (0,3):
                        for p in chord[:3]:note(3,p+12,start+eighth*.5,.28,48+intensity*12)
            else:
                pattern=[0,2,1,3,2,1,3,2]
                step=1 if mood in ('menu','mire') else .5
                for i in range(round(beats/step)):
                    if mood=='mire' and (bar+i)%3==0: continue
                    octave=24 if mood=='azure' else 12
                    note(2,chord[pattern[(i+bar)%8]]+octave,start+i*step,step*1.6,46+intensity*15)

            # Drums have fills, off-beat articulations, and breathing room under melodic phrases.
            if mood!='menu' and (phrase>0 or mood in ('cinder','storm','rift')):
                if mood=='amber':
                    for eighth in range(6):
                        note(6,64 if eighth in (0,3) else 69,start+eighth*.5,.13,56 if eighth%3==0 else 35)
                    note(6,36,start,.18,68);note(6,37,start+1.5,.14,52)
                elif mood in ('azure','mire'):
                    note(6,41,start,.22,47 if mood=='azure' else 63)
                    if bar%2:note(6,51 if mood=='azure' else 37,start+beats*.625,.18,34)
                    if mood=='mire': note(6,43,start+.75,.20,39)
                else:
                    power=mood in ('cinder','storm','rift')
                    note(6,36,start,.22,round((76 if power else 56)+intensity*13))
                    note(6,41 if power else 43,start+beats*.5,.25,68 if power else 44)
                    for eighth in range(round(beats*2)):
                        note(6,42 if power else 69,start+eighth*.5,.10,31+(12 if eighth%2==0 else 0))
                    if power:
                        note(6,38,start+1,.17,65)
                        if beats>3:note(6,38,start+3,.17,70)
                    if bar%8==0:note(6,49,start,.8,53 if power else 30)
                    if bar%8==7:
                        for i in range(4): note(6,[47,45,43,41][i],start+beats-1+i*.25,.15,48+i*6)
            elif mood=='menu' and phrase in (2,3) and bar%2==0:
                note(6,81,start+beats*.5,.25,29)

        # Four-bar themes and answering phrases leave rests instead of running all the time.
        theme=score['theme']; theme_span=sum(length for _,length in theme)
        phrase_bars=4
        for first_bar in range(0,bars,phrase_bars):
            section=first_bar//8
            if first_bar<4 or (mood=='menu' and first_bar>=32): continue
            if first_bar%16==12 and mood in ('menu','azure','mire'): continue
            window=phrase_bars*beats
            factor=window/theme_span
            cursor=base+first_bar*beats
            for at,(pitch,length) in enumerate(theme):
                # The B section moves the motif through the scale, with a separate answering voice.
                variation=2 if section in (2,5) else 0
                n=degree(score,pitch+variation)
                lead_shift=0 if mood=='storm' else 12
                n+=lead_shift
                duration=length*factor
                velocity=(63 if mood=='menu' else 72)+(8 if section in (2,4,6) else 0)
                note(0,n,cursor,duration*.84,velocity)
                if section in (2,4,6) and at%3==1:
                    note(1,degree(score,pitch+variation-2)+lead_shift,cursor+duration*.25,duration*.64,velocity-15)
                cursor+=duration
        total=round((base+cycle_beats)*PPQ)
        tracks[0].append((total,3,b'\xff\x06\x04LOOP'))
    path.parent.mkdir(parents=True,exist_ok=True)
    path.write_bytes(b'MThd'+struct.pack('>IHHH',6,1,len(tracks),PPQ)+b''.join(chunk(t) for t in tracks))

def master_loop(raw, score):
    length=round(score['bars']*score['beats']*60/score['bpm']*RATE)
    # The middle performance already contains the previous cycle's release/reverb tails.
    out=raw[length:length*2].copy()
    bridge=round(RATE*.06)
    w=(.5-.5*np.cos(np.linspace(0,math.pi,bridge,dtype=np.float32)))[:,None]
    out[:bridge]=raw[length*2:length*2+bridge]*(1-w)+out[:bridge]*w
    # Remove subsonic/DC energy with an FFT shelf. This keeps the bass clear under SFX.
    freq=np.fft.rfftfreq(len(out),1/RATE)
    shelf=np.clip((freq-22)/18,0,1)
    for channel in range(2):
        out[:,channel]=np.fft.irfft(np.fft.rfft(out[:,channel])*shelf,n=len(out)).astype(np.float32)
    rms=float(np.sqrt(np.mean(out.astype(np.float64)**2)))
    out*=10**(-18.5/20)/max(rms,1e-7)
    out=.89*np.tanh(out/.89)
    return out

def write_preview():
    assets=ROOT/'Assets/Era/Resources/Audio'; excerpts=[]
    for score in SCORES:
        audio,rate=sf.read(assets/(score['file']+'.ogg'),start=round(12*score['beats']*60/score['bpm']*RATE),frames=RATE*10,dtype='float32',always_2d=True)
        fade=np.linspace(0,1,round(RATE*.18),dtype=np.float32)[:,None]
        audio[:len(fade)]*=fade;audio[-len(fade):]*=fade[::-1];excerpts.append(audio)
    preview=np.concatenate(excerpts)
    with sf.SoundFile(Path(__file__).parent/'ERA-Soundtrack-Preview.ogg','w',samplerate=RATE,channels=2,format='OGG',subtype='VORBIS',compression_level=.35) as output:
        for at in range(0,len(preview),16384):output.write(preview[at:at+16384])

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--fluidsynth',type=Path,required=True)
    parser.add_argument('--soundfont',type=Path,required=True)
    parser.add_argument('--only',type=int,nargs='*')
    args=parser.parse_args()
    if hashlib.sha256(args.soundfont.read_bytes()).hexdigest()!=BANK_SHA256:
        raise RuntimeError('Expected the documented GeneralUser GS 2.0.3 bank')
    assets=ROOT/'Assets/Era/Resources/Audio'; work=ROOT/'Artifacts/MusicRender'; work.mkdir(parents=True,exist_ok=True)
    manifest=[]
    for index,score in enumerate(SCORES):
        entry={k:v for k,v in score.items() if k not in ('theme','chords','scale','voices')}
        if args.only is None or index in args.only:
            print(f"COMPOSING {index}: {score['file']}",flush=True)
            midi=Path(__file__).parent/(score['file']+'.mid'); write_midi(score,midi)
            render_midi=work/(score['file']+'-warm.mid');write_midi(score,render_midi,cycles=3)
            raw_path=work/(score['file']+'-raw.wav')
            command=[str(args.fluidsynth),'-ni','-q','-r',str(RATE),'-g','.55',
                     '-o','synth.reverb.active=1','-o','synth.reverb.room-size=.62',
                     '-o','synth.reverb.damp=.48','-o','synth.reverb.width=70',
                     '-o','synth.reverb.level=.27','-o','synth.chorus.active=1',
                     '-o','synth.polyphony=256','-T','wav','-O','float','-F',str(raw_path),
                     str(args.soundfont),str(render_midi)]
            completed=subprocess.run(command,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True,timeout=240)
            if completed.returncode:raise RuntimeError(completed.stdout)
            raw,rate=sf.read(raw_path,dtype='float32',always_2d=True)
            if rate!=RATE or raw.shape[1]!=2:raise RuntimeError('Invalid stereo render')
            audio=master_loop(raw,score); destination=assets/(score['file']+'.ogg')
            print('MASTERED '+score['file'],flush=True)
            # Small writes avoid exhausting the Windows encoder's native stack on long tracks.
            with sf.SoundFile(destination,'w',samplerate=RATE,channels=2,format='OGG',subtype='VORBIS',compression_level=.35) as output:
                for at in range(0,len(audio),16384):output.write(audio[at:at+16384])
            decoded,_=sf.read(destination,dtype='float32',always_2d=True)
            peak=float(np.max(np.abs(decoded)));rms=float(np.sqrt(np.mean(decoded.astype(np.float64)**2)))
            seam=float(np.max(np.abs(decoded[0]-decoded[-1])))
            max_gap=0;gap=0
            for block in np.array_split(decoded,math.ceil(len(decoded)/(RATE*.1))):
                if float(np.max(np.abs(block)))<.0003:gap+=.1;max_gap=max(max_gap,gap)
                else:gap=0
            if peak>=.99 or rms<.035 or max_gap>.5 or seam>.08:raise RuntimeError(f'Audio QC failed: {peak=}, {rms=}, {seam=}, {max_gap=}')
            entry.update(duration=round(len(decoded)/RATE,3),peakDb=round(20*math.log10(peak),2),
                         rmsDb=round(20*math.log10(rms),2),seamStep=round(seam,6),silenceSeconds=round(max_gap,2),
                         bytes=destination.stat().st_size,sha256=hashlib.sha256(destination.read_bytes()).hexdigest())
            print('RENDERED '+json.dumps(entry,ensure_ascii=False),flush=True)
        manifest.append(entry)
    old_path=Path(__file__).parent/'soundtrack.json'
    if args.only is not None and old_path.exists():
        old=json.loads(old_path.read_text(encoding='utf-8'))
        for i in range(len(manifest)):
            if i not in args.only:manifest[i]=old['tracks'][i]
    old_path.write_text(json.dumps(dict(rate=RATE,soundfontSha256=BANK_SHA256,tracks=manifest),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    if all((assets/(s['file']+'.ogg')).exists() for s in SCORES):write_preview()

if __name__=='__main__':main()
