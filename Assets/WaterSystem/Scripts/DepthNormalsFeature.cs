using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

namespace WaterSystem
{
	public class DepthNormalsFeature : ScriptableRendererFeature
	{
		private DepthNormalsPass depthNormalsPass;
		private RenderTargetHandle depthNormalsTexture;
		private Material depthNormalsMaterial;

		public override void Create()
		{
			depthNormalsMaterial = CoreUtils.CreateEngineMaterial("Hidden/Internal-DepthNormalsTexture");
			depthNormalsPass = new DepthNormalsPass(RenderQueueRange.opaque, -1, depthNormalsMaterial)
			{
				renderPassEvent = RenderPassEvent.AfterRenderingPrePasses
			};
			depthNormalsTexture.Init("_CameraDepthNormalsTexture");
		}
        
		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
            // Inject render pass (called when setting up renderer once per camera)
			depthNormalsPass.Setup(renderingData.cameraData.cameraTargetDescriptor, depthNormalsTexture);
			renderer.EnqueuePass(depthNormalsPass);
		}


        private class DepthNormalsPass : ScriptableRenderPass
        {
	        private readonly Material depthNormalsMaterial;
	        private FilteringSettings m_FilteringSettings;

	        private readonly string profilerTag = "DepthNormals Prepass";
            private readonly ShaderTagId shaderTagId = new ShaderTagId("DepthOnly");

            private RenderTargetHandle DepthAttachmentHandle { get; set; }

            private RenderTextureDescriptor Descriptor { get; set; }


            public DepthNormalsPass(RenderQueueRange renderQueueRange, LayerMask layerMask, Material material)
            {
                m_FilteringSettings = new FilteringSettings(renderQueueRange, layerMask);
                depthNormalsMaterial = material;
            }

            public void Setup(RenderTextureDescriptor baseDescriptor, RenderTargetHandle depthAttachmentHandle)
            {
                DepthAttachmentHandle = depthAttachmentHandle;
                baseDescriptor.colorFormat = RenderTextureFormat.ARGB32;
                baseDescriptor.depthBufferBits = 32;
                Descriptor = baseDescriptor;
            }

            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                // Create temporary render texture and configure target and clear state before executing render pass
                cmd.GetTemporaryRT(DepthAttachmentHandle.id, Descriptor, FilterMode.Point);
                ConfigureTarget(DepthAttachmentHandle.Identifier());
                ConfigureClear(ClearFlag.All, Color.black);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                // Rendering logic
                var cmd = CommandBufferPool.Get(profilerTag);

                var depthNormalsSample = new ProfilingSampler(profilerTag);
                using (new ProfilingScope(cmd, depthNormalsSample))
                {
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();

                    var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
                    var drawSettings = CreateDrawingSettings(shaderTagId, ref renderingData, sortFlags);
                    drawSettings.perObjectData = PerObjectData.None;

                    ref var cameraData = ref renderingData.cameraData;
                    var camera = cameraData.camera;

                    if (XRSettings.enabled)
                        context.StartMultiEye(camera);

                    drawSettings.overrideMaterial = depthNormalsMaterial;

                    context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref m_FilteringSettings);

                    // Set texture with depth and normals as a global texture to all shaders
                    cmd.SetGlobalTexture("_CameraDepthNormalsTexture", DepthAttachmentHandle.id);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public override void FrameCleanup(CommandBuffer cmd)
            {
                // Clean allocated resources used for execution of this render pass
	            if (DepthAttachmentHandle == RenderTargetHandle.CameraTarget) 
		            return;

	            cmd.ReleaseTemporaryRT(DepthAttachmentHandle.id);
	            DepthAttachmentHandle = RenderTargetHandle.CameraTarget;
            }
        }
    }
}
