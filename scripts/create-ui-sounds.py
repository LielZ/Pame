"""Generate original, low-amplitude Pame menu cues; no external recordings."""
from pathlib import Path
import math, wave, struct
out=Path(__file__).resolve().parents[1]/'src/Pame.App/Assets/Sounds'
out.mkdir(parents=True,exist_ok=True)
rate=44100
def tone(name,notes):
    duration=max(start+length for start,length,freq,amp in notes)+.015
    values=[]
    for n in range(int(duration*rate)):
        t=n/rate; sample=0
        for start,length,freq,amp in notes:
            p=t-start
            if 0<=p<length:
                env=min(1,p/.007)*math.exp(-5*p/length)*min(1,(length-p)/.012)
                sample+=amp*env*(math.sin(2*math.pi*freq*p)+.12*math.sin(2*math.pi*freq*2*p))
        values.append(struct.pack('<h',int(max(-1,min(1,sample))*32767)))
    with wave.open(str(out/(name+'.wav')),'wb') as w:
        w.setnchannels(1);w.setsampwidth(2);w.setframerate(rate);w.writeframes(b''.join(values))
tone('move',[(0,.065,760,.16)])
tone('select',[(0,.11,784,.18),(.045,.15,1175,.13)])
tone('back',[(0,.10,659,.13),(.045,.12,440,.12)])
tone('page',[(0,.14,523,.12),(.055,.16,784,.12),(.1,.17,1047,.08)])
tone('open',[(0,.16,587,.14),(.06,.22,880,.10)])
tone('error',[(0,.12,294,.13),(.11,.14,262,.10)])
print('Created 6 original PCM menu cues')
