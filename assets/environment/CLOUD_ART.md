# Original author cloud implementation

Source: [新杨XIYAG article](https://zhuanlan.zhihu.com/p/660806535), [GitHub repository](https://github.com/xinyangaa/Unity_URP_Genshin_Impact_Programmed_Skybox/tree/ef1bd4c6fa486977a323c3badaba5740feae1d55). Pinned commit: `ef1bd4c6fa486977a323c3badaba5740feae1d55`.

## Active resources

`spacelevel/EnvironmentRig/OriginalClouds/BaseClouds` is a scene-authored MeshInstance3D from `assets/scenes/environment/reference_clouds.tscn`. No cloud nodes, meshes or textures are created at runtime.

The active FBX and PNGs in `assets/environment/reference_clouds` are byte-for-byte copies, checked against Git blob hashes in `source_manifest.json`. `original_cloud_mesh.res` is the Godot importer output of the author's `cloud.fbx` mesh (80 vertices, 20 quads, original UV2). `78678.png` is the exact RGBA image referenced by the source base-cloud material, retaining its sRGB import flag, mipmaps and clamp wrap. There is no synthetic SDF, blur, resizing or tile repacking. The original noise PNG is also used at its full 2048² resolution.

`cloud.gdshader` preserves the original `Cloud.shader` fragment equations: UV2; twice-applied noise tiling; `_Time.x * 0.05`; scalar positive 0–0.03 UV disturbance; one-sided 0.08 SDF transition multiplied by A; squared light-direction weight; four-color mixing through R plus G edge lighting. Material noise tiling is (0.3, 0.3). The exact Unity shader and material metadata are retained beside the port for comparison.

`base_timeline.tres` contains all 22 material curves from clip `1387710026462685712`, bound in the source scene to the base cloud. Keys and unweighted Hermite tangents are preserved; the 0–4 time domain is normalized to 0–1, scaling tangents by 4. In particular, the first SDF value is 0.041 and the last is 0.050, without the previous endpoint repair. High and secondary material tracks are also preserved as resources, but are not applied to invented geometry.

## Godot integration differences

- HLSL is translated to Godot spatial shader syntax. Clouds use unshaded ALBEDO, alpha blending and no depth writes; the source's `ZWrite On / ZTest NotEqual` cannot be carried into the game's reversed-Z sky background behavior. The shader puts clouds at far depth so scene geometry remains in front.
- Vertex projection is centered on the active game camera, using its real perspective projection or the existing orthographic sky FOV and pitch. The GPU projection Y sign is preserved. Original cloud UVs and geometry stay intact. The author's reference camera height (-14) is retained as a view offset.
- The cloud node uses the saved Unity scene position (0, 0, 0). Its example-specific animated position (-119.6 Y) is not applied, because it places the entire layer below this level's playable view. This is a scene integration choice, not a change to the mesh or shader equations.
- Noise time is driven by the existing deterministic world clock rather than Unity time. SDF animation defaults to the author's 80-second period (4 seconds / 0.05 playback speed). Original color and SunMoon curves map sunrise/noon/sunset/midnight to the existing 20-minute game day. Pause, GM time changes and save/restore keep working.

## Missing upstream assets

The uploaded FBX has GUID `6cc1303ea41d42846945248ef433ee35`, matching the source base cloud. The example scene's **high and second cloud layers reference mesh GUID `4e842a0dab424954fbbe40e4d81b5a3d`, which is absent from the repository**. They cannot be reproduced exactly from this checkout and are not replaced with self-made cards.

The material's noise GUID `efdab6692df1af443b3b1b6d4996d2ed` is unresolved. The implementation uses the repository-provided `Noise_091.png`; this is not a verified match for the missing material binding. These missing inputs prevent claiming a complete visual one-to-one reproduction of the article GIF.

The previous AI-generated painting, packed atlas and packer are retained as unused historical assets. Neither environment scene references them, and the sky shader no longer has a cloud-card path.
