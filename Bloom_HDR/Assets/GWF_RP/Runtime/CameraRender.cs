using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRender
{
    const string bufferName = "Render Camera";
    Camera camera;
    ScriptableRenderContext context;
    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };
    CullingResults cullingResults;
    static ShaderTagId
        unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit"),
        litShaderTagId = new ShaderTagId("CustomLit");

    Lighting lighting = new Lighting();
    
    public void Render(ScriptableRenderContext context, Camera camera,bool useDynamicBatching, bool useGPUInstancing,bool useLightsPerObject, ShadowSettings shadowSettings, PostFXSettings postFXSettings)
    {
        this.context = context;
        this.camera = camera;
        PrepareBuffer();
        PrepareForSceneWindow();
        if (!Cull(shadowSettings.maxDistance))
        {
            return;
        }
        buffer.BeginSample(SampleName);
        ExecuteBuffer();
        lighting.Setup(context,cullingResults,useLightsPerObject,shadowSettings);

        buffer.EndSample(SampleName);
        Setup();
        DrawVisibleGeometry(useDynamicBatching, useGPUInstancing,useLightsPerObject);
        DrawUnsupportedShader();
        DrawGizmos();
        lighting.Cleanup();
        Submit();
    }

    void DrawVisibleGeometry(bool useDynamicBatching, bool useGPUInstancing,bool useLightsPerObject)
    {
        PerObjectData lightsPerObjectFlags = useLightsPerObject ?
        PerObjectData.LightData | PerObjectData.LightIndices :
        PerObjectData.None;
        SortingSettings sortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque};
        DrawingSettings drawingSettings = new DrawingSettings(unlitShaderTagId, sortingSettings)
        {
            enableDynamicBatching = useDynamicBatching,
            enableInstancing = useGPUInstancing,
            perObjectData = PerObjectData.ReflectionProbes | PerObjectData.Lightmaps| PerObjectData.ShadowMask 
            | PerObjectData.LightProbe| PerObjectData.OcclusionProbe | 
            PerObjectData.LightProbeProxyVolume |
                PerObjectData.OcclusionProbeProxyVolume|lightsPerObjectFlags
        };
        drawingSettings.SetShaderPassName(1, litShaderTagId);
        FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.opaque);

        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);

        context.DrawSkybox(camera);

        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;

        context.DrawRenderers(
            cullingResults, ref drawingSettings, ref filteringSettings
        );
    }


    void Setup()
    {
        context.SetupCameraProperties(camera);
        CameraClearFlags flags = camera.clearFlags;
        buffer.ClearRenderTarget(
            flags <= CameraClearFlags.Depth,
            flags == CameraClearFlags.Color,
            flags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear);
        buffer.BeginSample(SampleName);       
        ExecuteBuffer();        
        
    }

    void Submit()
    {
        buffer.EndSample(SampleName);
        ExecuteBuffer();
        context.Submit();
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    bool Cull(float maxShadowDistance)
    {
        if(camera.TryGetCullingParameters(out ScriptableCullingParameters p))
        {
            // cull 会在editor scene 地面网格画出来
            p.shadowDistance = Mathf.Min(maxShadowDistance, camera.farClipPlane);
            cullingResults = context.Cull(ref p);
            return true;
        }
        return false;
    }
  
}
