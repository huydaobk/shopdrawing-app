from rembg import remove
from PIL import Image

input_path = r'C:\Users\Administrator\.gemini\antigravity\brain\8da12894-df8e-4c17-b04f-51b511ce4426\icon_manual_1776667979737.png'
output_path = r'C:\my_project\shopdrawing-app\ShopDrawing.Plugin\Resources\Icons\icon_manual.png'

print('Opening image...')
input_img = Image.open(input_path)

print('Removing background...')
output_img = remove(input_img)

print('Cropping and resizing...')
bbox = output_img.getbbox()
if bbox:
    cropped = output_img.crop(bbox)
else:
    cropped = output_img

# Expand canvas slightly so it does not touch the very edge
w, h = cropped.size
padding = int(max(w, h) * 0.1) # 10% padding
canvas = Image.new('RGBA', (w + padding*2, h + padding*2), (0, 0, 0, 0))
canvas.paste(cropped, (padding, padding))

# Resize to 32x32
resized = canvas.resize((32, 32), Image.Resampling.LANCZOS)
resized.save(output_path, 'PNG')
print('Done!')
