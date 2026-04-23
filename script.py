import sys
with open('ShopDrawing.Plugin/Core/AccessoryDataManager.cs', 'rb') as f:
    content = f.read()

index = content.find(b'CategoryScope = \"Tr')
if index != -1:
    print('Found at', index)
    print(content[index:index+50].hex())
else:
    print('Not found')
