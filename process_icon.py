from PIL import Image

def process_icon(input_path, output_path):
    img = Image.open(input_path).convert('RGBA')
    # Find bounding box of non-white pixels
    data = img.getdata()
    width, height = img.size
    
    min_x, min_y, max_x, max_y = width, height, 0, 0
    bg_color = (255, 255, 255, 255)
    
    # We will compute the background color from the top-left pixel
    bg_color = img.getpixel((0,0))
    threshold = 30
    
    for y in range(height):
        for x in range(width):
            p = img.getpixel((x, y))
            # calculate distance from background
            dist = abs(p[0]-bg_color[0]) + abs(p[1]-bg_color[1]) + abs(p[2]-bg_color[2])
            if dist > threshold:
                if x < min_x: min_x = x
                if x > max_x: max_x = x
                if y < min_y: min_y = y
                if y > max_y: max_y = y

    if min_x > max_x or min_y > max_y:
        print("Empty image")
        return
        
    print(f"BBox: {min_x}, {min_y}, {max_x}, {max_y}")
    
    # Crop
    cropped = img.crop((min_x, min_y, max_x+1, max_y+1))
    
    # Make background transparent with soft edges
    c_data = cropped.getdata()
    c_width, c_height = cropped.size
    new_data = []
    
    for item in c_data:
        r, g, b, a = item
        # distance from white (or bg_color)
        dist = abs(r-bg_color[0]) + abs(g-bg_color[1]) + abs(b-bg_color[2])
        if dist < 40:
            new_data.append((255, 255, 255, 0)) # Fully transparent
        elif dist < 100:
            # blending
            alpha = int(((dist - 40) / 60.0) * 255)
            new_data.append((r, g, b, alpha))
        else:
            new_data.append(item)
            
    cropped.putdata(new_data)
    
    # Resize to 32x32 for AutoCAD standards (it scales automatically, but 32x32 is large icon)
    resized = cropped.resize((32, 32), Image.Resampling.LANCZOS)
    resized.save(output_path, 'PNG')

process_icon(r'C:\Users\Administrator\.gemini\antigravity\brain\8da12894-df8e-4c17-b04f-51b511ce4426\icon_manual_1776667979737.png', r'C:\my_project\shopdrawing-app\ShopDrawing.Plugin\Resources\Icons\icon_manual.png')
print("Processed icon!")
