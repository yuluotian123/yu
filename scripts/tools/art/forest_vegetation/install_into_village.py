"""Switch the six vegetation prefab references without repacking authored scene data."""
import json
import hashlib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
SCENE = ROOT/'assets/scenes/environment/forest_village_whitebox/forest_village_whitebox.tscn'
MAP = {'ancient_oak':'oak_broad','branching_oak':'hornbeam_tall','canopy_oak':'oak_sweeping',
       'shrub_round':'hazel_round','shrub_tall':'hazel_tall','shrub_low':'woodland_low'}

def install():
    original=SCENE.read_bytes()
    content=original.decode('utf8')
    if all(f'res://assets/scenes/environment/forest_vegetation/{name}.tscn' in content for name in MAP.values()):
        print('The village already uses the forest vegetation kit.')
        return
    backup=ROOT/'.godot/vegetation_v5/village_before_vegetation.tscn'
    if not backup.exists(): backup.write_bytes(original)
    changed=0
    for old,new in MAP.items():
        old_path=f'res://assets/scenes/environment/forest_town/models/{old}.tscn'
        new_path=f'res://assets/scenes/environment/forest_vegetation/{new}.tscn'
        assert content.count(old_path)+content.count(new_path)==1,(old,'unexpected scene reference count')
        changed+=content.count(old_path)
        content=content.replace(old_path,new_path)
    # Every byte outside the six resource paths must remain unchanged.
    restored=content
    for old,new in MAP.items():
        restored=restored.replace(f'res://assets/scenes/environment/forest_vegetation/{new}.tscn',
                                  f'res://assets/scenes/environment/forest_town/models/{old}.tscn')
    baseline=backup.read_bytes()
    assert restored.encode('utf8')==baseline
    SCENE.write_bytes(content.encode('utf8'))
    report={'prefab_replacements':MAP,'changed_resource_paths':changed,
            'all_other_scene_bytes_unchanged':True,'baseline_sha256':hashlib.sha256(baseline).hexdigest()}
    (ROOT/'assets/resources/art/forest_vegetation/integration.json').write_text(json.dumps(report,indent=2),encoding='utf8')
    print(json.dumps(report))

if __name__=='__main__': install()
