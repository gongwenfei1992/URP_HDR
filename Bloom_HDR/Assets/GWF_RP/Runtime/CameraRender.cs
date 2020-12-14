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
    public void Render(ScriptableRenderContext context,Camera camera,bool allowHDR,bool useDynamicBatch,bool useGPUInstance,bool useLightPerObject)
    {
        this.context = context;
        this.camera = camera;
    }
}
