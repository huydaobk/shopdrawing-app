from PIL import Image

input_path = r'C:\Users\Administrator\.gemini\antigravity\brain\8da12894-df8e-4c17-b04f-51b511ce4426\icon_manual_1776667979737.png'
output_path = r'C:\my_project\shopdrawing-app\ShopDrawing.Plugin\Resources\Icons\icon_manual.png'

print('Opening image...')
img = Image.open(input_path).convert('RGBA')

# Center crop box (left, upper, right, lower)
# For 1024x1024, let's crop 300 from each side -> 424x424 size
# Let's crop from 256 to 768 (512x512)
left = 256
upper = 256
right = 768
lower = 768
cropped = img.crop((left, upper, right, lower))

# Now remove background (pure whiteish pixels)
c_data = cropped.getdata()
new_data = []
for r, g, b, a in c_data:
    if r > 240 and g > 240 and b > 240:
        new_data.append((255, 255, 255, 0))
    # Soft edges for slightly off white
    elif r > 220 and g > 220 and b > 220:
        alpha = int((min(r,g,b) - 220) / 20.0 * 255)
        new_data.append((r, g, b, 255 - alpha))
    else:
        new_data.append((r, g, b, a))
        
cropped.putdata(new_data)

# Resize to 32x32 for Large Image
resized = cropped.resize((32, 32), Image.Resampling.LANCZOS)
resized.save(output_path, 'PNG')
print('Done!')
