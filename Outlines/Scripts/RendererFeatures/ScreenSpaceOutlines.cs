using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;

public class ScreenSpaceOutlines : ScriptableRendererFeature {

    [System.Serializable]
    private class ScreenSpaceOutlineSettings {

        [Header("General Outline Settings")]
        public Color outlineColor = Color.black;
        [Range(0.0f, 20.0f)]
        public float outlineScale = 1.0f;
        
        [Header("Depth Settings")]
        [Range(0.0f, 100.0f)]
        public float depthThreshold = 1.5f;
        [Range(0.0f, 500.0f)]
        public float robertsCrossMultiplier = 100.0f;

        [Header("Normal Settings")]
        [Range(0.0f, 1.0f)]
        public float normalThreshold = 0.4f;

        [Header("Depth Normal Relation Settings")]
        [Range(0.0f, 2.0f)]
        public float steepAngleThreshold = 0.2f;
        [Range(0.0f, 500.0f)]
        public float steepAngleMultiplier = 25.0f;
        
        [Header("General Scene View Space Normal Texture Settings")]
        public RenderTextureFormat colorFormat;
        public int depthBufferBits = 16;
        public FilterMode filterMode;
        public Color backgroundColor = Color.black;

        [Header("View Space Normal Texture Object Draw Settings")]
        public PerObjectData perObjectData;
        public bool enableDynamicBatching;
        public bool enableInstancing;

    }

    private class ScreenSpaceOutlinePass : ScriptableRenderPass {
        
        private readonly Material screenSpaceOutlineMaterial;
        private ScreenSpaceOutlineSettings settings;

        private FilteringSettings filteringSettings;
        private FilteringSettings occluderFilteringSettings;

        private readonly List<ShaderTagId> shaderTagIdList;
        private readonly Material normalsMaterial;
        private readonly Material occludersMaterial;

        private RTHandle normals;
        private RendererList normalsRenderersList;
        private RendererList occuldersRenderersList;

        RTHandle temporaryBuffer;

        public ScreenSpaceOutlinePass(RenderPassEvent renderPassEvent, LayerMask layerMask, LayerMask occluderLayerMask,
            ScreenSpaceOutlineSettings settings) {
            this.settings = settings;
            this.renderPassEvent = renderPassEvent;

            screenSpaceOutlineMaterial = new Material(Shader.Find("Hidden/Outlines"));
            screenSpaceOutlineMaterial.SetColor("_OutlineColor", settings.outlineColor);
            screenSpaceOutlineMaterial.SetFloat("_OutlineScale", settings.outlineScale);

            screenSpaceOutlineMaterial.SetFloat("_DepthThreshold", settings.depthThreshold);
            screenSpaceOutlineMaterial.SetFloat("_RobertsCrossMultiplier", settings.robertsCrossMultiplier);

            screenSpaceOutlineMaterial.SetFloat("_NormalThreshold", settings.normalThreshold);

            screenSpaceOutlineMaterial.SetFloat("_SteepAngleThreshold", settings.steepAngleThreshold);
            screenSpaceOutlineMaterial.SetFloat("_SteepAngleMultiplier", settings.steepAngleMultiplier);
            
            filteringSettings = new FilteringSettings(RenderQueueRange.opaque, layerMask);
            occluderFilteringSettings = new FilteringSettings(RenderQueueRange.opaque, occluderLayerMask);

            shaderTagIdList = new List<ShaderTagId> {
                new ShaderTagId("UniversalForward"),
                new ShaderTagId("UniversalForwardOnly"),
                new ShaderTagId("LightweightForward"),
                new ShaderTagId("SRPDefaultUnlit")
            };

            normalsMaterial = new Material(Shader.Find("Hidden/ViewSpaceNormals"));

            occludersMaterial = new Material(Shader.Find("Hidden/UnlitColor"));
            occludersMaterial.SetColor("_Color", settings.backgroundColor);
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData) {
            // Normals
            RenderTextureDescriptor textureDescriptor = renderingData.cameraData.cameraTargetDescriptor;
            textureDescriptor.colorFormat = settings.colorFormat;
            textureDescriptor.depthBufferBits = settings.depthBufferBits;
            RenderingUtils.ReAllocateIfNeeded(ref normals, textureDescriptor, settings.filterMode);
            
            // Color Buffer
            textureDescriptor.depthBufferBits = 0;
            RenderingUtils.ReAllocateIfNeeded(ref temporaryBuffer, textureDescriptor, FilterMode.Bilinear);

            ConfigureTarget(normals, renderingData.cameraData.renderer.cameraDepthTargetHandle);
            ConfigureClear(ClearFlag.Color, settings.backgroundColor);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) {
            if (!screenSpaceOutlineMaterial || !normalsMaterial || !occludersMaterial)
                return;

            CommandBuffer cmd = CommandBufferPool.Get();
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
                
            // Normals
            DrawingSettings drawSettings = CreateDrawingSettings(shaderTagIdList, ref renderingData, renderingData.cameraData.defaultOpaqueSortFlags);
            drawSettings.perObjectData = settings.perObjectData;
            drawSettings.enableDynamicBatching = settings.enableDynamicBatching;
            drawSettings.enableInstancing = settings.enableInstancing;
            drawSettings.overrideMaterial = normalsMaterial;
            
            RendererListParams normalsRenderersParams = new RendererListParams(renderingData.cullResults, drawSettings, filteringSettings);
            normalsRenderersList = context.CreateRendererList(ref normalsRenderersParams);
            cmd.DrawRendererList(normalsRenderersList);
            
            // Occluder
            DrawingSettings occluderSettings = drawSettings;
            occluderSettings.overrideMaterial = occludersMaterial;
            
            RendererListParams occulderRenderersParams = new RendererListParams(renderingData.cullResults, occluderSettings, occluderFilteringSettings);
            occuldersRenderersList = context.CreateRendererList(ref occulderRenderersParams);
            cmd.DrawRendererList(occuldersRenderersList);
            
            // Pass in RT for Outlines shader
            cmd.SetGlobalTexture(Shader.PropertyToID("_SceneViewSpaceNormals"), normals.rt);
            
            using (new ProfilingScope(cmd, new ProfilingSampler("ScreenSpaceOutlines"))) {

                Blitter.BlitCameraTexture(cmd, renderingData.cameraData.renderer.cameraColorTargetHandle, temporaryBuffer, screenSpaceOutlineMaterial, 0);
                Blitter.BlitCameraTexture(cmd, temporaryBuffer, renderingData.cameraData.renderer.cameraColorTargetHandle);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Release(){
            CoreUtils.Destroy(screenSpaceOutlineMaterial);
            CoreUtils.Destroy(normalsMaterial);
            CoreUtils.Destroy(occludersMaterial);
            normals?.Release();
            temporaryBuffer?.Release();
        }

    }

    [SerializeField] private RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
    [SerializeField] private LayerMask outlinesLayerMask;
    [SerializeField] private LayerMask outlinesOccluderLayerMask;
    
    [SerializeField] private ScreenSpaceOutlineSettings outlineSettings = new ScreenSpaceOutlineSettings();

    private ScreenSpaceOutlinePass screenSpaceOutlinePass;
    
    public override void Create() {
        if (renderPassEvent < RenderPassEvent.BeforeRenderingPrePasses)
            renderPassEvent = RenderPassEvent.BeforeRenderingPrePasses;

        screenSpaceOutlinePass = new ScreenSpaceOutlinePass(renderPassEvent, outlinesLayerMask, outlinesOccluderLayerMask, outlineSettings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
        renderer.EnqueuePass(screenSpaceOutlinePass);
    }

    protected override void Dispose(bool disposing){
        if (disposing)
        {
            screenSpaceOutlinePass?.Release();
        }
    }

}
