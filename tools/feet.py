import sys
sys.path.insert(0,'tools')
from PIL import Image
import import_sprite as imp
def bg(c): return c[0]>235 and c[1]>235 and c[2]>235
name = sys.argv[1]
path='assets/images/%s.gif' % name
img = imp.load_frame(path,0)
box = imp.content_box(img, bg)
with Image.open(path) as im: n = im.n_frames
for k in range(n):
    o = imp.content_box(imp.load_frame(path,k), bg)
    box = (min(box[0],o[0]),min(box[1],o[1]),max(box[2],o[2]),max(box[3],o[3]))
x0,y0,x1,y1 = box
cols, rows = x1-x0+1, y1-y0+1
rgba = imp.read_rgba(path,0,quiet=True)
grid = imp.sample_grid(imp.flatten(rgba), (x0,y0), 1.0, cols, rows, bg)
outside = imp.clear_background(grid, bg)
alpha = imp.sample_alpha(rgba, (x0,y0), 1.0, cols, rows)
def solid(x,y): return (not outside[y][x]) and alpha[y][x] >= 128
# 맨 아래에서부터 내용이 있는 마지막 줄
bottom = max(y for y in range(rows) if any(solid(x,y) for x in range(cols)))
band = int(sys.argv[2]) if len(sys.argv)>2 else 6
print("%s: 격자 %dx%d, 내용 마지막 줄 y=%d" % (name, cols, rows, bottom))
for y in range(max(0,bottom-band+1), bottom+1):
    runs=[]; start=None
    for x in range(cols+1):
        on = x<cols and solid(x,y)
        if on and start is None: start=x
        if not on and start is not None:
            runs.append((start,x-1)); start=None
    print("  y=%2d  덩어리: %s" % (y, ", ".join("x %d..%d"%r for r in runs)))
