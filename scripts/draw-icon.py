"""Draw the original, editable Mechanical Toolbox vector mark and Windows icon.
Run: uv run --with pymupdf --with pillow python scripts/draw-icon.py
"""
from pathlib import Path
from math import cos, sin, pi
import pymupdf
from PIL import Image
root=Path(__file__).resolve().parents[1]
assets=root/'src/MechToolbox.Desktop/Assets'
points=[]
for tooth in range(12):
    for phase,radius in [(-.5,151),(-.28,151),(-.22,180),(.22,180),(.28,151),(.5,151)]:
        angle=(tooth+phase)*2*pi/12-pi/2
        points.append((256+radius*cos(angle),256+radius*sin(angle)))
path='M '+' L '.join(f'{x:.2f},{y:.2f}' for x,y in points)+' Z'
svg=f'''<svg xmlns="http://www.w3.org/2000/svg" width="512" height="512" viewBox="0 0 512 512">
<title>机设工具箱 · 齿轮与游标卡尺</title>
<rect x="8" y="8" width="496" height="496" rx="112" fill="#152A3B"/>
<path d="{path}" fill="#39B6A5"/>
<circle cx="256" cy="256" r="112" fill="#152A3B"/>
<g transform="rotate(-32 256 256)" fill="#F6FAFC">
<rect x="113" y="229" width="295" height="34" rx="8"/>
<path d="M130 190 H157 V316 H130 Z M130 291 H205 V316 H130 Z"/>
<rect x="278" y="214" width="57" height="64" rx="8"/>
<path d="M300 268 H326 V316 H256 V291 H300 Z"/>
<g stroke="#152A3B" stroke-width="4"><path d="M185 230 V245 M208 230 V251 M231 230 V245 M254 230 V251 M352 230 V245 M375 230 V251"/></g>
<rect x="291" y="226" width="31" height="21" rx="4" fill="#152A3B"/>
</g></svg>'''
(assets/'toolbox.svg').write_text(svg)
doc=pymupdf.open(stream=svg.encode(),filetype='svg')
pix=doc[0].get_pixmap(matrix=pymupdf.Matrix(2,2),alpha=True)
large=Image.frombytes('RGBA',(pix.width,pix.height),pix.samples)
large.resize((512,512),Image.Resampling.LANCZOS).save(assets/'toolbox.png')
large.resize((256,256),Image.Resampling.LANCZOS).save(assets/'toolbox.ico',sizes=[(16,16),(20,20),(24,24),(32,32),(40,40),(48,48),(64,64),(128,128),(256,256)])
print('Created original SVG, PNG and 9-size Windows ICO')
