shader_type canvas_item;

uniform float progress : hint_range(0.0, 1.0) = 1.0;
uniform int style = 0;
uniform vec2 panel_origin = vec2(0.0);
uniform vec2 panel_extent = vec2(1.0);

void fragment() {
    vec2 local = (SCREEN_UV - panel_origin) / panel_extent;
    float edge = local.x * 0.7 + local.y * 0.3;
    if (style == 1) {
        edge = fract(local.y * 10.0) * 0.75 + local.x * 0.25;
    } else if (style == 2) {
        edge = (abs(local.x - 0.5) + abs(local.y - 0.5));
    }
    float boundary = mix(-0.06, 1.06, progress);
    float mask = 1.0 - smoothstep(boundary - 0.025, boundary + 0.025, edge);
    float rim = (1.0 - smoothstep(0.0, 0.04, abs(edge - boundary))) * step(0.001, progress) * step(progress, 0.999);
    COLOR.rgb = mix(COLOR.rgb, vec3(1.0, 0.83, 0.39), rim * 0.85);
    COLOR.a *= mask;
}
