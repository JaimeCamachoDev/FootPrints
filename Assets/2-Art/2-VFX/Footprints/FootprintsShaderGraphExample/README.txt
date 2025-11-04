Footprints Lit (URP) — Shader Graph Example
===========================================

This folder contains:
- FootprintsLit.shadergraph — minimal URP Lit Shader Graph with all properties.
- FootprintsHelper.hlsl — helper functions for Custom Function nodes (optional).

Why minimal?
Unity versions differ in Shader Graph JSON. A minimal graph is safest to import; wire nodes in the editor using the steps below. The HLSL helpers reduce node complexity if you prefer Custom Function nodes.

Properties created
------------------
- BaseMap (Texture2D), BaseColor (Color)
- FootMask (Texture2D) — uses R channel
- FootNormalMap (Texture2D) — tangent-space normal
- FootNormalIntensity (Float, 0..2)
- FootAlbedoDarken (Float, 0..1)
- FootSmoothnessMul (Float, 0..1)
- Smoothness (Float, 0..1)
- SpecularColor (Color) — Lit (Specular) workflow

Wiring inside Shader Graph
--------------------------
1) Graph Inspector → Target: Universal, **Lit**, Workflow: **Specular**. Surface: Opaque/Alpha as needed.
2) Base:
   - Sample Texture 2D (BaseMap) × BaseColor → Base Color input.
3) Mask:
   - Sample Texture 2D (FootMask) → Split → use R as mask m.
4) Darken Albedo under footprint:
   - Custom Function (String) → Name: DarkenAlbedo_float, Source: File → FootprintsHelper.hlsl
     Inputs: baseAlbedo(float3), mask(float), darkenFactor(float) ← FootAlbedoDarken
     Output → Base Color.
5) Normal:
   - Sample Texture 2D (FootNormalMap) with Type: Normal.
   - Custom Function → NormalStrength_float (normalTex float4, intensity float) ← FootNormalIntensity → Normal (Tangent).
6) Smoothness:
   - Custom Function → MultiplySmoothness_float (Smoothness, mask m, FootSmoothnessMul) → Smoothness pin.
7) Specular:
   - Plug SpecularColor into Specular pin.
8) Optional tiny imprint:
   - Vertex stage: Normal Vector (Object) + mask m + Float property FootHeightStrength → Custom Function VertexOffsetOS_float → add to Position (Object).

Quest 3 notes
-------------
- ASTC textures, modest resolutions (e.g., 1024 for mask/normal).
- Avoid Parallax/Height maps. Small vertex offset is cheaper.
- Pack additional masks into FootMask G/B/A if needed.

