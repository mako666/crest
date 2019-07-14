﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Crest
{

    [RequireComponent(typeof(Camera))]
    public class UnderwaterPostProcess : MonoBehaviour
    {
        public Material _underWaterPostProcMat;
        public Material _oceanMaskMat;
        private Camera _mainCamera;
        RenderTexture _textureMask;
        RenderTexture _depthBuffer;
        static int sp_HorizonHeight = Shader.PropertyToID("_HorizonHeight");
        static int sp_HorizonOrientation = Shader.PropertyToID("_HorizonOrientation");
        static int sp_MaskTex = Shader.PropertyToID("_MaskTex");

        void Start()
        {
            _mainCamera = GetComponent<Camera>();
            if (_mainCamera == null)
            {
                Debug.LogError("Underwater effects expect to be attached to a camera", this);
                enabled = false;

                return;
            }
        }

        void OnRenderImage(RenderTexture source, RenderTexture target)
        {
            if (_oceanMaskMat == null)
            {
                _oceanMaskMat = OceanRenderer.Instance.OceanMaterial;
            }

            if (_textureMask == null)
            {
                _textureMask = new RenderTexture(source);
                _textureMask.name = "Ocean Mask";
                _textureMask.format = RenderTextureFormat.R8;
                _textureMask.Create();

                _depthBuffer = new RenderTexture(source);
                _depthBuffer.name = "Ocean Mask Depth";
                _depthBuffer.format = RenderTextureFormat.Depth;
                _depthBuffer.Create();
            }

            // Get all ocean chunks and render them using cmd buffer, but with
            // a custom material that only does vert displacements.
            CommandBuffer commandBuffer = new CommandBuffer();
            commandBuffer.name = "Underwater Post Process";
            commandBuffer.SetRenderTarget(_textureMask.colorBuffer, _depthBuffer.depthBuffer);
            OceanChunkRenderer[] chunkComponents = Object.FindObjectsOfType<OceanChunkRenderer>();
            foreach (OceanChunkRenderer chunkComponent in chunkComponents)
            {
                MeshFilter meshFilter = chunkComponent.GetComponent<MeshFilter>();
                Matrix4x4 cameraTransform = GL.GetGPUProjectionMatrix(_mainCamera.projectionMatrix, true) * _mainCamera.worldToCameraMatrix;
                commandBuffer.DrawMesh(
                    meshFilter.mesh,
                   cameraTransform * chunkComponent.transform.localToWorldMatrix,
                   _oceanMaskMat
                );
            }

            // TODO(UPP): handle Roll
            float horizonRoll = 0.0f;
            float horizonHeight = 0.0f;
            float halfFov = _mainCamera.fieldOfView * 0.5f;
            Vector3 cameraForward = _mainCamera.transform.forward;
            float cameraRotation = Mathf.Atan2(-1.0f * cameraForward.y, (new Vector2(cameraForward.x, cameraForward.z)).magnitude);
            float halfProp = Mathf.Tan(cameraRotation * 0.5f) / Mathf.Tan(halfFov * Mathf.Deg2Rad);
            horizonHeight = halfProp + 0.5f;

            _underWaterPostProcMat.SetFloat(sp_HorizonHeight, horizonHeight);
            _underWaterPostProcMat.SetFloat(sp_HorizonOrientation, horizonRoll);
            _underWaterPostProcMat.SetTexture(sp_MaskTex, _textureMask);

            commandBuffer.Blit(source, target, _underWaterPostProcMat);

            Graphics.ExecuteCommandBuffer(commandBuffer);
        }
    }

}
