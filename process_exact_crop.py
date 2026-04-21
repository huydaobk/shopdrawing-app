from PIL import Image

input_path = r'C:\Users\Administrator\.gemini\antigravity\brain\8da12894-df8e-4c17-b04f-51b511ce4426\icon_manual_1776667979737.png'
output_path = r'C:\my_project\shopdrawing-app\ShopDrawing.Plugin\Resources\Icons\icon_manual.png'

print('Opening image...')
img = Image.open(input_path).convert('RGBA')

data = img.getdata()
width, height = img.size

min_x = width
min_y = height
max_x = 0
max_y = 0

for y in range(height):
    for x in range(width):
        r, g, b, a = img.getpixel((x, y))
        # If not close to white (allow some tolerance for white background)
        if not (r > 240 and g > 240 and b > 240):
            if x < min_x: min_x = x
            if x > max_x: max_x = x
            if y < min_y: min_y = y
            if y > max_y: max_y = y

if min_x > max_x or min_y > max_y:
    print("Failed to find non-white pixels")
else:
    print(f"BBox: {min_x}, {min_y}, {max_x}, {max_y}")
    # add a tiny padding (5%)
    w, h = max_x - min_x, max_y - min_y
    pad_w = int(w * 0.05)
    pad_h = int(h * 0.05)
    
    crop_x1 = max(0, min_x - pad_w)
    crop_y1 = max(0, min_y - pad_h)
    crop_x2 = min(width, max_x + pad_w)
    crop_y2 = min(height, max_y + pad_h)
    
    cropped = img.crop((crop_x1, crop_y1, crop_x2, crop_y2))
    
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
