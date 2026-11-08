shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_always, cull_disabled, shadows_disabled, fog_disabled;

void vertex() {
    MODELVIEW_MATRIX = VIEW_MATRIX * mat4(MAIN_CAM_INV_VIEW_MATRIX[0],
        MAIN_CAM_INV_VIEW_MATRIX[1], MAIN_CAM_INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
    MODELVIEW_MATRIX *= mat4(vec4(length(MODEL_MATRIX[0].xyz), 0.0, 0.0, 0.0),
        vec4(0.0, length(MODEL_MATRIX[1].xyz), 0.0, 0.0),
        vec4(0.0, 0.0, length(MODEL_MATRIX[2].xyz), 0.0), vec4(0.0, 0.0, 0.0, 1.0));
}

void fragment() {
    float diamond = abs(UV.x - 0.5) + abs(UV.y - 0.5);
    // Write only real particle pixels, so Relink can preserve them without clearing a rectangle.
    if (diamond >= 0.5 || COLOR.a < 0.01) discard;
    ALBEDO = mix(COLOR.rgb, vec3(1.0, 0.97, 0.75), 1.0 - step(0.18, diamond));
    ALPHA = COLOR.a;
}
