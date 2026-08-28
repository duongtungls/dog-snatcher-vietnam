using UnityEngine;

namespace DogSnatcher.Data
{
    /// <summary>
    /// Framing for the fake-2D rig: a 3D scene, gameplay confined to lanes on the XZ plane, shot
    /// by a camera tilted back off straight-down so the rider reads from behind. GDD 2.2.
    ///
    /// Orthographic keeps the GDD's flat, distortion-free read - tilting it only foreshortens the
    /// road, it never converges. Perspective is what actually sells "behind the bike": the road
    /// narrows toward the top of the screen. Both share the same lane layout and framing maths.
    /// </summary>
    [CreateAssetMenu(menuName = "Dog Snatcher/Camera Rig", fileName = "CameraRig")]
    public sealed class CameraRigAsset : ScriptableObject
    {
        public enum ProjectionMode
        {
            /// <summary>Flat, no convergence. Tilt only foreshortens. GDD 2.2 default.</summary>
            Orthographic,
            /// <summary>Road converges toward the horizon - the behind-the-bike look.</summary>
            Perspective
        }

        [Header("Projection")]
        [SerializeField] private ProjectionMode projection = ProjectionMode.Perspective;

        [Tooltip("Half the visible world height at the camera's focus point, in metres. " +
                 "Drives framing in BOTH projection modes, so switching mode keeps the same framing.")]
        [SerializeField, Min(0.1f)] private float orthographicSize = 10.5f;

        [Tooltip("Degrees tilted back off straight-down. 0 = pure top-down, 90 = level with the road. " +
                 "30 keeps the play space readable while showing the rider from behind.")]
        [SerializeField, Range(0f, 80f)] private float tiltFromVerticalDegrees = 30f;

        [Tooltip("Perspective only. Vertical field of view. Narrower = flatter, less convergence, " +
                 "camera further back; wider = more dramatic recession.")]
        [SerializeField, Range(10f, 80f)] private float fieldOfView = 38f;

        [Tooltip("Orthographic only. Metres above the ground plane.")]
        [SerializeField, Min(0.1f)] private float cameraHeight = 24f;

        [SerializeField, Min(0.01f)] private float nearClip = 0.3f;
        [SerializeField, Min(0.02f)] private float farClip = 200f;

        [Header("Framing")]
        [Tooltip("Where the player sits vertically on screen. 0 = bottom edge, 1 = top edge. " +
                 "GDD 2.2 puts them at 0.35 for maximum read-ahead.")]
        [SerializeField, Range(0f, 1f)] private float playerScreenAnchor = 0.35f;

        [Tooltip("Aspect the layout is authored against. 9:16 portrait = 0.5625.")]
        [SerializeField, Min(0.05f)] private float referenceAspect = 0.5625f;

        public ProjectionMode Projection => projection;
        public float OrthographicSize => orthographicSize;
        public float TiltFromVerticalDegrees => tiltFromVerticalDegrees;
        public float FieldOfView => fieldOfView;
        public float CameraHeight => cameraHeight;
        public float NearClip => nearClip;
        public float FarClip => farClip;
        public float PlayerScreenAnchor => playerScreenAnchor;
        public float ReferenceAspect => referenceAspect;
        public bool IsPerspective => projection == ProjectionMode.Perspective;

        public float ViewportHeight => orthographicSize * 2f;
        public float ReferenceViewportWidth => ViewportHeight * referenceAspect;
        public float GetViewportWidth(float aspect) => ViewportHeight * aspect;

        /// <summary>Angle of the view direction below the horizon. 90 = straight down.</summary>
        public float CameraPitchDegrees => 90f - tiltFromVerticalDegrees;

        /// <summary>Local rotation of the camera, and of any billboard that must face it square-on.</summary>
        public Quaternion CameraRotation => Quaternion.Euler(CameraPitchDegrees, 0f, 0f);

        /// <summary>
        /// Distance from camera to its ground focus point, chosen so a perspective shot frames the
        /// same world height as the orthographic one. Keeps the two modes interchangeable.
        /// </summary>
        public float PerspectiveDistance
        {
            get
            {
                float t = Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
                return t > 0.0001f ? ViewportHeight / (2f * t) : ViewportHeight;
            }
        }

        /// <summary>Metres of ground along +Z that the viewport height covers at the focus point.</summary>
        public float GroundViewLength
        {
            get
            {
                float sin = Mathf.Sin(CameraPitchDegrees * Mathf.Deg2Rad);
                return sin > 0.001f ? ViewportHeight / sin : ViewportHeight;
            }
        }

        /// <summary>
        /// How far ahead of the player (+Z) the camera's ground focus sits, to land the player
        /// at <see cref="PlayerScreenAnchor"/>.
        /// </summary>
        public float CameraForwardOffset => GroundViewLength * (0.5f - playerScreenAnchor);

        /// <summary>
        /// Camera position relative to the rig root. It sits back along -Z and up along +Y so the
        /// view stays centred on the ground focus point whichever projection is in use.
        /// </summary>
        public Vector3 CameraLocalPosition
        {
            get
            {
                float pitchRad = CameraPitchDegrees * Mathf.Deg2Rad;

                if (IsPerspective)
                {
                    float d = PerspectiveDistance;
                    return new Vector3(0f,
                                       d * Mathf.Sin(pitchRad),
                                       CameraForwardOffset - d * Mathf.Cos(pitchRad));
                }

                float tan = Mathf.Tan(pitchRad);
                float pullBack = Mathf.Abs(tan) > 0.001f ? cameraHeight / tan : 0f;
                return new Vector3(0f, cameraHeight, CameraForwardOffset - pullBack);
            }
        }
    }
}
