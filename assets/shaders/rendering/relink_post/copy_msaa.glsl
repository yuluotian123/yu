#[vertex]
#version 450
layout(location = 0) out vec2 uv;
void main() {
	vec2 position = vec2((gl_VertexIndex << 1) & 2, gl_VertexIndex & 2);
	uv = position;
	gl_Position = vec4(position * 2.0 - 1.0, 0.0, 1.0);
}

#[fragment]
#version 450
layout(location = 0) in vec2 uv;
layout(location = 0) out vec4 color;
layout(set = 0, binding = 0) uniform sampler2D graded_color;
void main() {
	// Preserve pre-transparent grading when the engine resolves MSAA again after transparency.
	color = textureLod(graded_color, uv, 0.0);
}
