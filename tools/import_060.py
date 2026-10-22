"""Copy exact approved art/audio and convert tuned Godot definitions to Unity DTOs."""
from pathlib import Path
import hashlib, json, shutil, sys

root = Path(__file__).resolve().parents[1]
source = root.parents[1] / 'outputs' / 'ufn-combate-0.6.0'
export = root.parent / 'catalog060-export.json'
content = root / 'Assets/StreamingAssets/Content'
raw = json.loads(export.read_text(encoding='utf-8'))

def pascal(value):
    return ''.join(part[:1].upper()+part[1:] for part in value.split('_'))

def move(value):
    return {pascal(key): val for key,val in value.items() if isinstance(val,(str,int,float,bool))}

characters = []
for c in raw['characters']:
    characters.append({'Id':c['id'], 'Key':c['key'], 'Name':c['name'], 'Title':c['title'],
        'ColorHex':c['color'], 'UltimateName':c['super'], 'Quote':c['quote'], 'Ending':c['ending'],
        'WalkSpeed':c['base']['walk_speed'], 'Specials':[move(m) for m in c['specials']],
        'Ultimate':move(c['ultimate']), 'Offensive':move(c['offensive']), 'Defense':move(c['defense'])})
content.mkdir(parents=True,exist_ok=True)
(content/'catalog.json').write_text(json.dumps({'Characters':characters,'Normals':[move(m) for m in raw['normals']],
    # The first jab has implicit stage 0; chain_normal is called only with 1/2.
    'Chains':[move(m) for m in raw['chains'] if m.get('chain_stage') in (1,2)]}, ensure_ascii=False, indent=2), encoding='utf-8')
inventory = []
for category in ('sprites','arenas','audio'):
    for file in (source/'assets'/category).rglob('*'):
        if not file.is_file() or file.suffix not in ('.png','.json','.wav','.txt'):
            continue
        relative=file.relative_to(source/'assets')
        destination=content/relative
        destination.parent.mkdir(parents=True,exist_ok=True)
        shutil.copy2(file,destination)
        inventory.append({'path':relative.as_posix(),'sha256':hashlib.sha256(file.read_bytes()).hexdigest()})
font_dest=root/'Assets/Resources/Fonts'
font_dest.mkdir(parents=True,exist_ok=True)
for f in (source/'assets/fonts').iterdir():
    if f.suffix in ('.ttf','.txt'):shutil.copy2(f,font_dest/f.name)
shutil.copy2(source/'LICENSES.md',root/'LICENSES-ORIGINAIS.md')
(root/'docs/ASSET-INTEGRITY.json').write_text(json.dumps(inventory,indent=2),encoding='utf-8')
print(f'Imported {len(characters)} fighters; {len(inventory)} original files, unchanged pixels/audio.')
