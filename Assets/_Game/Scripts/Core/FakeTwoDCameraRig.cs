using DogSnatcher.Data;
using UnityEngine;

namespace DogSnatcher.Core
{
    /// <summary>
    /// Drives the fake-2D rig. The scene is a real 3D scene - ground lies on the XZ plane and
    /// "forward" is +Z - but the camera hangs straight above it looking down, orthographic, so
    /// there is no perspective skew and the top-down art reads as flat 2D.
    ///
    /// Sits on the rig root, which tracks the player's distance along +Z. The camera and the
    /// ground layer are children, so the whole rig travels together and the ground stays framed.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class FakeTwoDCameraRig : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private CameraRigAsset rig;
        [SerializeField] private RunSpeedChannel speedChannel;

        [Header("Scene refs")]
        [SerializeField] private Camera targetCamera;
        [Tooltip("Parent of the scrolling ground strips. Kept centred on the camera view.")]
        [SerializeField] private Transform groundLayer;

        /// <summary>Ground-plane point the camera is centred on. Systems can read this to cull.</summary>
        public Vector3 ViewCenter { get; private set; }

        /// <summary>
        /// Rotation an upright billboard needs to face the camera square-on. The ground art is
        /// flat on XZ, but the rider is drawn from behind, so it stands up and faces the lens.
        /// </summary>
        public Quaternion BillboardRotation => rig != null ? rig.CameraRotation : Quaternion.identity;

        private void OnEnable() => ApplyRigLayout();

        private void OnValidate()
        {
            if (!isActiveAndEnabled) return;
            ApplyRigLayout();
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            if (speedChannel != null)
            {
                Vector3 p = transform.position;
                p.z = speedChannel.DistanceMetres;
                transform.position = p;
            }

            ViewCenter = transform.position + new Vector3(0f, 0f, rig != null ? rig.CameraForwardOffset : 0f);
        }

        /// <summary>Pushes every value from <see cref="CameraRigAsset"/> onto the actual transforms.</summary>
        [ContextMenu("Apply Rig Layout")]
        public void ApplyRigLayout()
        {
            if (rig == null) return;

            float forward = rig.CameraForwardOffset;

            if (targetCamera != null)
            {
                Transform ct = targetCamera.transform;
                ct.localPosition = rig.CameraLocalPosition;
                // Pitched down from behind: +Z still reads as "up the screen" so the world
                // scrolls toward the bottom, but the shot now looks over the rider's shoulder.
                ct.localRotation = rig.CameraRotation;

                targetCamera.orthographic = !rig.IsPerspective;
                targetCamera.orthographicSize = rig.OrthographicSize;
                targetCamera.fieldOfView = rig.FieldOfView;
                targetCamera.nearClipPlane = rig.NearClip;
                targetCamera.farClipPlane = rig.FarClip;
            }

            if (groundLayer != null)
                groundLayer.localPosition = new Vector3(0f, 0f, forward);

            ViewCenter = transform.position + new Vector3(0f, 0f, forward);
        }
    }
}
