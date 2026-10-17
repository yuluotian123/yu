// Pure surface patterns shared by raster weather and DDGI hit evaluation.
float wet_hash(vec2 p) {
	// 整数散列在相邻格子的两侧完全一致，避免 sin/fract 放大浮点误差形成切边。
	uvec2 cell = uvec2(ivec2(p));
	uint h = cell.x * 1597334677u ^ cell.y * 3812015801u;
	h = (h ^ (h >> 16u)) * 2246822519u;
	h = (h ^ (h >> 13u)) * 3266489917u;
	return float((h ^ (h >> 16u)) & 16777215u) / 16777216.0;
}

float wet_noise(vec2 p) {
	vec2 cell = floor(p);
	vec2 f = fract(p);
	f = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
	return mix(mix(wet_hash(cell), wet_hash(cell + vec2(1.0, 0.0)), f.x),
			   mix(wet_hash(cell + vec2(0.0, 1.0)), wet_hash(cell + vec2(1.0)), f.x), f.y);
}

float snow_pattern(vec2 position) {
	return wet_noise(position * 1.25 + vec2(31.0)) * 0.72
		 + wet_noise(position * 4.3 + vec2(9.0)) * 0.28;
}

float puddle_pattern(vec2 p) {
	float noise = wet_noise(p) * 0.7 + wet_noise(p * 2.07 + vec2(17.0)) * 0.3;
	return smoothstep(0.18, 0.82, noise);
}
