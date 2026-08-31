using DogSnatcher.Data;
using UnityEngine;

namespace DogSnatcher.Gameplay
{
    /// <summary>
    /// Keeps an upright sprite squared up to the tilted rig camera - the ground art is flat on
    /// XZ but riders, cars and dogs are drawn from behind, so their sprite has to stand up and
    /// face the lens. Reads the pitch from <see cref="CameraRigAsset"/>, so retuning the camera
    /// angle re-aligns every billboard with no hand editing.
    ///
    /// Applied on enable and, in the editor, every frame - never per-frame at runtime, so it
    /// stays out of the way of anything that rolls the sprite in play (the player's lean).
    /// Assumes the parent transform is unrotated.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class CameraFacingBillboard : MonoBehaviour
    {
        [SerializeField] private CameraRigAsset cameraRig;

        [Tooltip("Degrees to stand the sprite up beyond square-to-camera (toward fully vertical). " +
                 "0 = flat billboard; a rider reads a little better a touch more upright.")]
        [SerializeField, Range(0f, 45f)] private float extraUprightDegrees;

        private void OnEnable() => Face();

        private void OnValidate()
        {
            if (isActiveAndEnabled) Face();
        }

        private void Update()
        {
            if (!Application.isPlaying) Face();
        }

        private void Face()
        {
            if (cameraRig == null) return;
            float pitch = Mathf.Min(90f, cameraRig.CameraPitchDegrees + extraUprightDegrees);
            transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }
}
