Shader "Detector"
{
    Properties
    {
        [Header(Base Properties)]
        [Space] _BaseColorMap ("Base Color", 2D) = "white" {}
        _BaseColorStrength ("Base Color Strength", Range(0, 1)) = 1.0
        _OpacityMap ("Opacity Mask", 2D) = "white" {}
        _OpacityStrength ("Opacity Strength", Range(0, 1)) = 1.0
        _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _BumpMap ("Bump", 2D) = "bump" {}
        _BumpStrength ("Bump Strength", Range(0, 1)) = 1.0
        _AOMap ("AO", 2D) = "white" {}
        _AOStrength ("AO Strength", Range(0, 1)) = 1.0
        _MetallicMap ("Metallic", 2D) = "black" {}
        _MetallicStrength ("Metallic Strength", Range(0, 1)) = 1.0
        _RoughnessMap ("Roughness", 2D) = "white" {}
        _RoughnessStrength ("Roughness Strength", Range(0, 1)) = 1.0
        _DisplacementMap ("Displacement", 2D) = "black" {}
        _GlowMap ("Glow", 2D) = "black" {}
        _GlowStrength ("Glow Strength", Range(0, 1)) = 1.0
        _BlendMap ("Blend", 2D) = "white" {}
        _BlendStrength ("Blend Strength", Range(0, 1)) = 1.0
        [Toggle] _DoubleSided ("Double Sided", Float) = 0

        [Header(Rain Properties)]
        [Space] _Rain_drops ("Rain", 2D) = "bump" {}
        _Rain_static ("Rain static", 2D) = "bump" {}
        _Rain_intensity ("Rain intensity", Range(0, 10)) = 0
        _Rainspeed ("Rain speed", Range(0, 40)) = 0
        _Raintiling ("Rain tiling", Vector) = (0, 0, 0, 0)
        _RainRotation ("Rain Rotation", Range(0, 360)) = 0

        [Header(Material Properties)]
        [Space] _Metallicshift ("Metallic shift", Range(0, 2)) = 1.0
        _Occlusionshift ("Occlusion shift", Range(0, 2)) = 1.0
        _Smoothness ("Smoothness", Range(0, 5)) = 1.0

        [Header(GUI Layer)]
        [Space] _GUI_2_Flipbook ("GUI_2_Flipbook", 2D) = "white" {}
        _GUIPanOffset ("GUI Pan Offset", Vector) = (0, 0, 0, 0)
        _GUIScale ("GUI Scale", Range(0.01, 1.0)) = 0.5
        _GUIRotation ("GUI Rotation", Range(0, 360)) = 0
        _GUIColor ("GUI Color", Color) = (1, 1, 1, 1)
        _GUIAnimSpeed ("GUI Anim Speed", Range(0, 50)) = 25
        _Emissionintensity ("Emission intensity", Range(0, 3)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200
        Cull Off

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        // Texture samplers
        sampler2D _BaseColorMap;
        sampler2D _OpacityMap;
        sampler2D _BumpMap;
        sampler2D _AOMap;
        sampler2D _MetallicMap;
        sampler2D _RoughnessMap;
        sampler2D _DisplacementMap;
        sampler2D _GlowMap;
        sampler2D _BlendMap;
        sampler2D _Rain_drops;
        sampler2D _Rain_static;
        sampler2D _GUI_2_Flipbook;

        // Properties
        float _BaseColorStrength;
        float _OpacityStrength;
        float _AlphaCutoff;
        float _BumpStrength;
        float _AOStrength;
        float _MetallicStrength;
        float _RoughnessStrength;
        float _GlowStrength;
        float _BlendStrength;
        float _DoubleSided;
        float _Rain_intensity;
        float _Rainspeed;
        float2 _Raintiling;
        float _RainRotation;
        float _Metallicshift;
        float _Occlusionshift;
        float _Smoothness;
        float4 _GUIPanOffset;
        float _GUIScale;
        float _GUIRotation;
        float4 _GUIColor;
        float _GUIAnimSpeed;
        float _Emissionintensity;

        struct Input
        {
            float2 uv_BaseColorMap;
            float facing : VFACE;
            float3 worldNormal;
            INTERNAL_DATA
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Sample base textures
            fixed4 baseColor = tex2D(_BaseColorMap, IN.uv_BaseColorMap);
            fixed opacity = tex2D(_OpacityMap, IN.uv_BaseColorMap).r;
            fixed4 ao = tex2D(_AOMap, IN.uv_BaseColorMap);
            fixed4 glow = tex2D(_GlowMap, IN.uv_BaseColorMap);
            fixed metallic = tex2D(_MetallicMap, IN.uv_BaseColorMap).r;
            fixed roughness = tex2D(_RoughnessMap, IN.uv_BaseColorMap).r;

            // Set albedo for diffuse map
            o.Albedo = baseColor.rgb * _BaseColorStrength;

            // Apply ambient occlusion with shift
            o.Occlusion = lerp(1.0, ao.r * _Occlusionshift, _AOStrength);

            // Set metallic with shift
            o.Metallic = metallic * _MetallicStrength * _Metallicshift;

            // Set smoothness with slider
            o.Smoothness = _Smoothness;

            // Handle normals with double-sided support
            float3 normal = UnpackNormal(tex2D(_BumpMap, IN.uv_BaseColorMap));
            if (_DoubleSided > 0.5)
            {
                normal = lerp(float3(0, 0, 1), normal, _BumpStrength) * (IN.facing > 0 ? 1 : -1);
            }
            else
            {
                normal = lerp(float3(0, 0, 1), normal, _BumpStrength);
            }

            // Rain effect
            float2 uv_Rain = IN.uv_BaseColorMap * _Raintiling;
            float angle = _RainRotation * 3.14159 / 180.0;
            float2x2 rotationMatrix = float2x2(cos(angle), -sin(angle), sin(angle), cos(angle));
            uv_Rain = mul(rotationMatrix, uv_Rain);

            float fbtotaltiles = 5.0 * 5.0;
            float fbcolsoffset = 1.0 / 5.0;
            float fbrowsoffset = 1.0 / 5.0;
            float fbspeed = _Time.y * _Rainspeed;
            float2 fbtiling = float2(fbcolsoffset, fbrowsoffset);
            float fbcurrenttileindex = round(fmod(fbspeed, fbtotaltiles));
            fbcurrenttileindex += (fbcurrenttileindex < 0) ? fbtotaltiles : 0;
            float fblinearindextox = round(fmod(fbcurrenttileindex, 5.0));
            float fboffsetx = fblinearindextox * fbcolsoffset;
            float fblinearindextoy = round(fmod((fbcurrenttileindex - fblinearindextox) / 5.0, 5.0));
            fblinearindextoy = (5.0 - 1) - fblinearindextoy;
            float fboffsety = fblinearindextoy * fbrowsoffset;
            float2 fboffset = float2(fboffsetx, fboffsety);
            half2 fbuv = frac(uv_Rain) * fbtiling + fboffset;

            float3 rainDynamic = UnpackScaleNormal(tex2D(_Rain_drops, fbuv), _Rain_intensity);
            float2 rainStaticUV = uv_Rain * float2(40.0, 10.0);
            float3 rainStatic = UnpackNormal(tex2D(_Rain_static, rainStaticUV));

            float3 worldNormal = WorldNormalVector(IN, float3(0, 0, 1));
            float3 rainNormal = lerp(rainDynamic, rainStatic, worldNormal.y);
            normal = BlendNormals(normal, rainNormal);

            o.Normal = normal;

            // GUI Layer effect
            float2 uv_GUI = IN.uv_BaseColorMap - _GUIPanOffset.xy;
            float guiAngle = _GUIRotation * 3.14159 / 180.0;
            float2x2 rotationMatrixGUI = float2x2(cos(guiAngle), -sin(guiAngle), sin(guiAngle), cos(guiAngle));
            uv_GUI = mul(rotationMatrixGUI, uv_GUI) + _GUIPanOffset.xy;
            float quadSize = _GUIScale;
            float2 quadUV = float2(
                saturate((uv_GUI.x - _GUIPanOffset.x + 0.5 * quadSize) / quadSize),
                saturate((uv_GUI.y - _GUIPanOffset.y + 0.5 * quadSize) / quadSize)
            );
            if (quadUV.x >= 0 && quadUV.x <= 1 && quadUV.y >= 0 && quadUV.y <= 1)
            {
                float fbtotaltilesGUI = 4.0 * 4.0;
                float fbcolsoffsetGUI = 1.0 / 4.0;
                float fbrowsoffsetGUI = 1.0 / 4.0;
                float fbspeedGUI = _Time.y * _GUIAnimSpeed;
                float2 fbtilingGUI = float2(fbcolsoffsetGUI, fbrowsoffsetGUI);
                float fbcurrenttileindexGUI = round(fmod(fbspeedGUI, fbtotaltilesGUI));
                fbcurrenttileindexGUI += (fbcurrenttileindexGUI < 0) ? fbtotaltilesGUI : 0;
                float fblinearindextoxGUI = round(fmod(fbcurrenttileindexGUI, 4.0));
                float fboffsetxGUI = fblinearindextoxGUI * fbcolsoffsetGUI;
                float fblinearindextoyGUI = round(fmod((fbcurrenttileindexGUI - fblinearindextoxGUI) / 4.0, 4.0));
                fblinearindextoyGUI = (4.0 - 1) - fblinearindextoyGUI;
                float fboffsetyGUI = fblinearindextoyGUI * fbrowsoffsetGUI;
                float2 fboffsetGUI = float2(fboffsetxGUI, fboffsetyGUI);
                half2 fbuvGUI = quadUV * fbtilingGUI + fboffsetGUI;

                float guiEmission = tex2D(_GUI_2_Flipbook, fbuvGUI).a * _Emissionintensity;
                o.Emission += guiEmission * _GUIColor.rgb;
            }

            o.Emission = (glow.rgb * _GlowStrength) + o.Emission;

            o.Alpha = opacity * _OpacityStrength;
            clip(o.Alpha - _AlphaCutoff);
        }
        ENDCG
    }
    FallBack "Diffuse"
}