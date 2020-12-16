using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class Shadows
{
	const string bufferName = "Shadows";

	const int maxShadowedDirLightCount = 4, maxShadowedOtherLightCount = 16;
	const int maxCascades = 4;

	static string[] directionalFilterKeywords = {
		"_DIRECTIONAL_PCF3",
		"_DIRECTIONAL_PCF5",
		"_DIRECTIONAL_PCF7",
	};

	static string[] otherFilterKeywords = {
		"_OTHER_PCF3",
		"_OTHER_PCF5",
		"_OTHER_PCF7",
	};

	static string[] cascadeBlendKeywords = {
		"_CASCADE_BLEND_SOFT",
		"_CASCADE_BLEND_DITHER"
	};

	static string[] shadowMaskKeywords = {
		"_SHADOW_MASK_ALWAYS",
		"_SHADOW_MASK_DISTANCE"
	};

	private ScriptableRenderContext context;
    private CullingResults cullingResults;
    private ShadowSettings settings;
    private bool useShadowMask;
	int shadowedDirLightCount, shadowedOtherLightCount;
	CommandBuffer buffer = new CommandBuffer
	{
		name = bufferName
	};

	static int	dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas"),	
		dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices"),		otherShadowAtlasId = Shader.PropertyToID("_OtherShadowAtlas"),	
		otherShadowMatricesId = Shader.PropertyToID("_OtherShadowMatrices"),			otherShadowTilesId = Shader.PropertyToID("_OtherShadowTiles"),	
		cascadeCountId = Shader.PropertyToID("_CascadeCount"),							shadowPancakingId = Shader.PropertyToID("_ShadowPancaking"),
		cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres"),		cascadeDataId = Shader.PropertyToID("_CascadeData"),
		shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize"),					shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade");

	static Vector4[]
		cascadeCullingSpheres = new Vector4[maxCascades],
		cascadeData = new Vector4[maxCascades],
		otherShadowTiles = new Vector4[maxShadowedOtherLightCount];

	static Matrix4x4[]
		dirShadowMatrices = new Matrix4x4[maxShadowedDirLightCount * maxCascades],
		otherShadowMatrices = new Matrix4x4[maxShadowedOtherLightCount];

	struct ShadowedDirectionalLight
	{
		public int visibleLightIndex;
		public float slopeScaleBias;
		public float nearPlaneOffset;
	}

	ShadowedDirectionalLight[] shadowedDirectionalLights =
		new ShadowedDirectionalLight[maxShadowedDirLightCount];

	struct ShadowedOtherLight
	{
		public int visibleLightIndex;
		public float slopeScaleBias;
		public float normalBias;
		public bool isPoint;
	}

	ShadowedOtherLight[] shadowedOtherLights =
		new ShadowedOtherLight[maxShadowedOtherLightCount];

	Vector4 atlasSizes;

	public void Setup(ScriptableRenderContext context , CullingResults cullingResults,ShadowSettings settings)
    {
		this.context = context;
		this.cullingResults = cullingResults;
		this.settings = settings;
		shadowedDirLightCount = shadowedOtherLightCount = 0;
		useShadowMask = false;
	}

	public void Render()
    {
        if (shadowedDirLightCount > 0)
        {
			//render dicrectional shadow;
			RenderDirectionalShadows();

		}
        else
        {
			buffer.GetTemporaryRT(dirShadowAtlasId, 1, 1, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        }
        if (shadowedOtherLightCount > 0)
        {
            //render other shadow;
        }
        else
        {
			buffer.SetGlobalTexture(otherShadowAtlasId, dirShadowAtlasId);
        }

		buffer.BeginSample(bufferName);
		SetKeywords(shadowMaskKeywords, useShadowMask ? QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask ? 0 : 1 : -1);
		buffer.SetGlobalInt(cascadeCountId, shadowedDirLightCount > 0 ? settings.directional.cascadeCount : 0);

		float f = 1f - settings.directional.cascadeFade;
		buffer.SetGlobalVector(shadowDistanceFadeId, new Vector4(1f / settings.maxDistance, 1f / settings.distanceFade, 1f / (1f - f * f)));

		buffer.SetGlobalVector(shadowAtlasSizeId, atlasSizes);
		buffer.EndSample(bufferName);
		ExecuteBuffer();

    }

	void RenderDirectionalShadows()
    {
		int atlasSize = (int)settings.directional.atlasSize;
		atlasSizes.x = atlasSize;
		atlasSizes.y = 1 / atlasSize;
		buffer.GetTemporaryRT(dirShadowAtlasId, atlasSize, atlasSize, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
		buffer.SetRenderTarget(dirShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
		buffer.ClearRenderTarget(true, false, Color.clear);
		buffer.SetGlobalFloat(shadowPancakingId, 1f);
		buffer.BeginSample(bufferName);
		ExecuteBuffer();

		int tiles = shadowedDirLightCount * settings.directional.cascadeCount;
		//从左到右，是否小于等于1选1，是否大于1小于等于4，
		int split = tiles <= 1 ? 1 : tiles < 4 ? 2 : 4;
		int tileSize = atlasSize / split;

		for(int i = 0; i < shadowedDirLightCount; i++)
        {
			RenderDirectionalShadows(i, split, tileSize);
        }
		buffer.SetGlobalVectorArray(cascadeCullingSpheresId, cascadeCullingSpheres);
		buffer.SetGlobalVectorArray(cascadeDataId, cascadeData);
		buffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices);

		SetKeywords(directionalFilterKeywords, (int)settings.directional.filter - 1);
		SetKeywords(cascadeBlendKeywords, (int)settings.directional.cascadeBlend - 1);
		buffer.EndSample(bufferName);
		ExecuteBuffer();
    }
	void RenderDirectionalShadows(int index, int split, int tileSize)
	{
		ShadowedDirectionalLight light = shadowedDirectionalLights[index];
		var shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);
		int cascadeCount = settings.directional.cascadeCount;
		int tileOffset = index * cascadeCount;
		Vector3 ratios = settings.directional.CascadeRatios;
		float cullingFactor = Mathf.Max(0f, 0.8f - settings.directional.cascadeFade);
		float tileScale = 1f / split;
		for(int i = 0; i < cascadeCount; i++)
        {
			cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(light.visibleLightIndex, i, cascadeCount, ratios, tileSize, 
				light.nearPlaneOffset, out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix, out ShadowSplitData splitData);
			splitData.shadowCascadeBlendCullingFactor = cullingFactor;
			shadowSettings.splitData = splitData;
			if(i == 0)
            {
				//set cascade data
				SetCascadeData(i, splitData.cullingSphere, tileSize);
            }

			int tileIndex = tileOffset + i;
			dirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(projectionMatrix * viewMatrix, SetTileViewport(tileIndex, split, tileSize), tileScale);
			buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
			buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
			ExecuteBuffer();
			context.DrawShadows(ref shadowSettings);
			buffer.SetGlobalDepthBias(0f, 0f);
        }
	}

	void SetCascadeData(int index,Vector4 cullingSphere,float tileSize)
    {
		float texelSize = 2f * cullingSphere.w / tileSize;
		float filterSzie = texelSize * ((float)settings.directional.filter + 1f);
		cullingSphere.w -= filterSzie;
		cullingSphere.w *= cullingSphere.w;
		cascadeCullingSpheres[index] = cullingSphere;
		cascadeData[index] = new Vector4(1f / cullingSphere.w, filterSzie * 1.4142136f);
	}

	void RenderOtherShadows()
    {
		int atlasSize = (int)settings.other.atlasSize;
		atlasSizes.z = atlasSize;
		atlasSizes.w = 1f / atlasSize;
		buffer.GetTemporaryRT(otherShadowAtlasId, atlasSize, atlasSize, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
		buffer.SetRenderTarget(otherShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
		buffer.ClearRenderTarget(true, false, Color.clear);
		buffer.SetGlobalFloat(shadowPancakingId, 0f);
		buffer.BeginSample(bufferName);
		ExecuteBuffer();

		int tiles = shadowedOtherLightCount;
		int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
		int tileSzie = atlasSize / split;

		for(int i = 0; i < shadowedOtherLightCount;)
        {
            if (shadowedOtherLights[i].isPoint)
            {
				//render point shadow
				i += 6;
            }
            else{
				//render spot shadows
				i += 1;
            }
        }

		buffer.SetGlobalMatrixArray(otherShadowMatricesId, otherShadowMatrices);
		buffer.SetGlobalVectorArray(otherShadowTilesId, otherShadowTiles);
		SetKeywords(otherFilterKeywords, (int)settings.other.filter - 1);
		buffer.EndSample(bufferName);
		ExecuteBuffer();
    }

	void RenderSpotShadows(int index,int split ,int tileSize)
    {
		ShadowedOtherLight light = shadowedOtherLights[index];
		var shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);

    }
	public void Cleanup()
	{
		buffer.ReleaseTemporaryRT(dirShadowAtlasId);
		if (shadowedOtherLightCount > 0)
		{ 
			buffer.ReleaseTemporaryRT(otherShadowAtlasId);
		}
		ExecuteBuffer();

	}

	void SetOtherTileDate(int index,Vector2 offset,float scale,float bias)
    {
		float border = atlasSizes.w * 0.5f;
		Vector4 data;
		data.x = offset.x * scale + border;
		data.y = offset.y * scale + border;
		data.z = scale - border - border;
		data.w = bias;

    }

	Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m,Vector2 offset,float scale)
    {
        if (SystemInfo.usesReversedZBuffer)
        {
			m.m20 = -m.m20;
			m.m21 = -m.m21;
			m.m22 = -m.m22;
			m.m23 = -m.m23;
		}
		m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
		m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
		m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
		m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
		m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
		m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
		m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
		m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;
		m.m20 = 0.5f * (m.m20 + m.m30);
		m.m21 = 0.5f * (m.m21 + m.m31);
		m.m22 = 0.5f * (m.m22 + m.m32);
		m.m23 = 0.5f * (m.m23 + m.m33);
		return m;
    }

	Vector2 SetTileViewport(int index,int split,float  tileSize)
    {
		Vector2 offset = new Vector2(index % split, index / split);
		buffer.SetViewport(new Rect(offset.x * tileSize, offset.y * tileSize, tileSize, tileSize));
		return offset;
    }

	void SetKeywords(string[] keywords,int enabledIndex)
    {
		for(int i = 0; i < keywords.Length; i++)
        {
			if(i == enabledIndex)
            {
				buffer.EnableShaderKeyword(keywords[i]);
            }
            else
            {
				buffer.DisableShaderKeyword(keywords[i]);
            }
        }
    }

	void ExecuteBuffer()
    {
		context.ExecuteCommandBuffer(buffer);
		buffer.Clear();
    }

}
