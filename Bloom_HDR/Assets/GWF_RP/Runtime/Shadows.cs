using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class Shadows
{
	const string bufferName = "Shadows";
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
	public void Setup(ScriptableRenderContext context , CullingResults cullingResults,ShadowSettings settings)
    {
		this.context = context;
		this.cullingResults = cullingResults;
		this.settings = settings;
		shadowedDirLightCount = shadowedOtherLightCount = 0;
		useShadowMask = false;
	}

	public void Cleanup()
    {
		buffer.ReleaseTemporaryRT(dirShadowAtlasId);
        if (shadowedOtherLightCount > 0)
        {			
			buffer.ReleaseTemporaryRT(otherShadowAtlasId);
        }
		ExecutrBuffer();

	}

	void SetOtherTileDate(int index,Vector2 offset,float scale,float bias)
    {

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

	void ExecutrBuffer()
    {
		context.ExecuteCommandBuffer(buffer);
		buffer.Clear();
    }

}
