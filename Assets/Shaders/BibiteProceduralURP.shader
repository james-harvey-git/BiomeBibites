Shader "Bibites/ProceduralBibiteURP"
{
    Properties
    {
        _BibiteColor ("Bibite Color", Color) = (0.82,0.82,0.82,1)
        _BibiteParamsA ("Params A", Vector) = (1.2, 0.35, 0.8, 0.5)
        _BibiteParamsB ("Params B", Vector) = (0.55, 0.55, 0.45, 1.0)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Unlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BibiteColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BibiteParamsA)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BibiteParamsB)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            float hash21(float2 p)
            {
                float3 p3 = frac(float3(p.x, p.y, p.x) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float noise2(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = hash21(i);
                float b = hash21(i + float2(1,0));
                float c = hash21(i + float2(0,1));
                float d = hash21(i + float2(1,1));
                float2 u = f*f*(3.0 - 2.0*f);
                return lerp(lerp(a,b,u.x), lerp(c,d,u.x), u.y);
            }

            float sdEllipse(float2 p, float2 ab)
            {
                float2 q = p / ab;
                return length(q) - 1.0;
            }

            float sdCircle(float2 p, float r) { return length(p) - r; }

            float sdCapsule(float2 p, float2 a, float2 b, float r)
            {
                float2 pa = p - a, ba = b - a;
                float h = saturate(dot(pa, ba) / dot(ba, ba));
                return length(pa - ba*h) - r;
            }

            float smoothMin(float a, float b, float k)
            {
                float h = saturate(0.5 + 0.5*(b - a)/k);
                return lerp(b, a, h) - k*h*(1.0 - h);
            }

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float4 baseCol = UNITY_ACCESS_INSTANCED_PROP(Props, _BibiteColor);
                float4 A = UNITY_ACCESS_INSTANCED_PROP(Props, _BibiteParamsA);
                float4 B = UNITY_ACCESS_INSTANCED_PROP(Props, _BibiteParamsB);

                float bodyAspect = max(0.4, A.x);
                float rimNoise   = saturate(A.y);
                float finAmount  = saturate(A.z);
                float mouthMood  = saturate(A.w);

                float eyeSep     = saturate(B.x);
                float eyeSize    = saturate(B.y);
                float pupilSize  = saturate(B.z);
                float seed       = B.w;

                float2 uv = IN.uv * 2.0 - 1.0;
                float2 p = uv;

                float2 bodyAB = float2(0.55 * bodyAspect, 0.45);
                float dBody = sdEllipse(p, bodyAB);

                float ang = atan2(p.y, p.x);
                float rad = length(p);
                float n = noise2(float2(ang * 3.0, rad * 6.0) + seed * 11.7);
                float rim = (n - 0.5) * 0.18 * rimNoise;
                dBody += rim;

                // Tail fin
                float2 tailA = float2(-0.55 * bodyAspect, 0.05);
                float2 tailB = float2(-0.85 * bodyAspect, 0.10);
                float dTail  = sdCapsule(p, tailA, tailB, 0.14 * finAmount);
                dBody = smoothMin(dBody, dTail, 0.18);

                // Top fin
                float2 finA = float2(0.00, 0.25);
                float2 finB = float2(-0.15 * bodyAspect, 0.55);
                float dFin  = sdCapsule(p, finA, finB, 0.10 * finAmount);
                dBody = smoothMin(dBody, dFin, 0.20);

                float aa = fwidth(dBody) * 1.5;
                float inside = 1.0 - smoothstep(0.0, aa, dBody);

                float outlineW = 0.05;
                float outline = 1.0 - smoothstep(outlineW, outlineW + aa, abs(dBody));

                float core = smoothstep(-0.45, 0.15, dBody);
                float highlight = saturate(dot(normalize(float3(p.x, p.y, 0.35)), normalize(float3(0.6, 0.8, 0.4))));
                highlight = pow(highlight, 2.4);

                float3 bodyColor = baseCol.rgb;
                float3 shaded = lerp(bodyColor * 0.55, bodyColor, core);
                shaded += highlight * 0.18;

                float speck = noise2(p * 10.0 + seed * 3.1);
                shaded *= lerp(0.92, 1.05, speck);

                // Eyes
                float faceX = 0.22 * bodyAspect;
                float2 e1 = p - float2(faceX,  (eyeSep * 0.20));
                float2 e2 = p - float2(faceX, -(eyeSep * 0.20));

                float eyeR = lerp(0.06, 0.11, eyeSize);
                float dEye1 = sdCircle(e1, eyeR);
                float dEye2 = sdCircle(e2, eyeR);
                float eyeMask = (1.0 - smoothstep(0.0, fwidth(dEye1)*1.5, dEye1)) +
                                (1.0 - smoothstep(0.0, fwidth(dEye2)*1.5, dEye2));
                eyeMask = saturate(eyeMask);

                float pupilR = eyeR * lerp(0.35, 0.65, pupilSize);
                float2 pupilOff = float2(0.018, 0.006) * (hash11(seed*19.3) - 0.5);
                float dP1 = sdCircle(e1 + pupilOff, pupilR);
                float dP2 = sdCircle(e2 + pupilOff, pupilR);
                float pupilMask = (1.0 - smoothstep(0.0, fwidth(dP1)*1.5, dP1)) +
                                  (1.0 - smoothstep(0.0, fwidth(dP2)*1.5, dP2));
                pupilMask = saturate(pupilMask);

                // Mouth
                float2 mA = float2(0.32 * bodyAspect, -0.06);
                float2 mB = float2(0.42 * bodyAspect, -0.06 + (mouthMood - 0.5) * 0.06);
                float dMouth = sdCapsule(p, mA, mB, 0.018);
                float mouthMask = 1.0 - smoothstep(0.0, fwidth(dMouth)*1.5, dMouth);

                float3 outlineCol = float3(0.08, 0.08, 0.08);
                float3 col = shaded;

                col = lerp(col, float3(0.92,0.92,0.92), eyeMask * inside);
                col = lerp(col, float3(0.12,0.12,0.12), pupilMask * inside);
                col = lerp(col, float3(0.15,0.15,0.15), mouthMask * inside);

                col = lerp(col, outlineCol, outline * inside);

                float alpha = inside;
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}