﻿using UnityEngine;
using UnityEngine.Rendering;

public partial class PostFXStack
{

	const string bufferName = "Post FX";

	const int maxBloomPyramidLevels = 16;

	int fxSourceId = Shader.PropertyToID("_PostFXSource");

	int bloomPyramidId;

	CommandBuffer buffer = new CommandBuffer
	{
		name = bufferName
	};

	ScriptableRenderContext context;

	Camera camera;

	PostFXSettings settings;

	public bool IsActive => settings != null;
	enum Pass
	{
		Copy
	}
	public void Setup(
		ScriptableRenderContext context, Camera camera, PostFXSettings settings
	)
	{
		this.context = context;
		this.camera = camera;
		this.settings = camera.cameraType <= CameraType.SceneView?settings : null;
		ApplySceneViewState();
	}

	public void Render(int sourceId)
	{
		//buffer.Blit(sourceId, BuiltinRenderTextureType.CameraTarget);
		//Draw(sourceId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
		DoBloom(sourceId);
		context.ExecuteCommandBuffer(buffer);
		buffer.Clear();
	}

	void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, Pass pass)
	{
		buffer.SetGlobalTexture(fxSourceId, from);
		buffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
		buffer.DrawProcedural(Matrix4x4.identity, settings.Material, (int)pass,	MeshTopology.Triangles, 3);
	}

	public PostFXStack()
	{
		bloomPyramidId = Shader.PropertyToID("_BloomPyramid0");
		for (int i = 1; i < maxBloomPyramidLevels; i++)
		{
			Shader.PropertyToID("_BloomPyramid" + i);
		}
	}

	void DoBloom(int sourceId)
	{
		buffer.BeginSample("Bloom");
		PostFXSettings.BloomSettings bloom = settings.Bloom;
		int width = camera.pixelWidth / 2, height = camera.pixelHeight / 2;
		RenderTextureFormat format = RenderTextureFormat.Default;
		int fromId = sourceId, toId = bloomPyramidId;

		int i;
		for (i = 0; i < bloom.maxIterations; i++)
		{
			if (height < bloom.downscaleLimit || width < bloom.downscaleLimit)
			{
				break;
			}
			buffer.GetTemporaryRT(
				toId, width, height, 0, FilterMode.Bilinear, format
			);
			Draw(fromId, toId, Pass.Copy);
			fromId = toId;
			toId += 1;
			width /= 2;
			height /= 2;
		}

		Draw(fromId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);

		for (i -= 1; i >= 0; i--)
		{
			buffer.ReleaseTemporaryRT(bloomPyramidId + i);
		}

		buffer.EndSample("Bloom");
	}
}
