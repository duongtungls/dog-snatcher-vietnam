using DogSnatcher.Data;
using UnityEngine;

namespace DogSnatcher.Gameplay
{
    /// <summary>
    /// Horizontal lane control for the player. Starts on the road's true centreline - not on any
    /// of the four lane centres, since with an even lane count none of them sits at x=0 - and
    /// locks onto the nearest lane the first time the player moves, then steps discretely between
    /// lanes after that. GDD 2.1 (lane layout), 3 (swipe left/right, one lane per swipe).
    ///
    /// Input is a touch swipe left/right, or A/D as the desktop equivalent of the same two
    /// gestures. Deliberately no mouse-drag fallback: the Editor's Game view can receive real
    /// mouse events from unrelated tool/UI interaction, which read as phantom swipes. Nothing
    /// here ever triggers a snatch - GDD 4.3 makes snatching proximity-driven once
    /// DogSpawner/SnarePole exist, never input-driven, so a tap that doesn't clear the swipe
    /// threshold does nothing rather than falling back to some other action.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerLaneController : MonoBehaviour
    {
        [SerializeField] private RoadLayoutAsset layout;

        [Tooltip("GDD 3: swipe left/right changes lane. 0.18s default, 0.12s fully upgraded.")]
        [SerializeField, Min(0.01f)] private float laneChangeTime = 0.18f;

        [Tooltip("GDD 3: minimum swipe distance to register, in screen pixels.")]
        [SerializeField, Min(1f)] private float minSwipeDistancePixels = 40f;

        [Tooltip("Desktop stand-in for swipe left/right while testing without a touch screen.")]
        [SerializeField] private bool debugKeyboard = true;

        // Lane position as a float coordinate: 1.0 = centre of lane 0, 2.0 = centre of lane 1,
        // and so on; a half-integer is a boundary - 2.0 (laneCount * 0.5 for 4 lanes) is the
        // road's true centreline, which is where a run starts.
        private float laneCoordinate;
        private int? currentLaneIndex;     // null until the first move locks onto a lane
        private float startX;
        private float targetX;
        private float moveElapsed;
        private float moveDuration;
        private bool moving;

        private bool pointerDown;
        private Vector2 pointerDownPos;

        private void OnEnable()
        {
            moving = false;
            currentLaneIndex = null;
            pointerDown = false;
            if (layout == null) return;

            laneCoordinate = layout.LaneCount * 0.5f;
            Vector3 p = transform.localPosition;
            p.x = 0f;                       // road centreline, regardless of where it last sat
            transform.localPosition = p;
        }

        private void Update()
        {
            if (layout == null) return;

            ReadSwipe();
            if (debugKeyboard)
            {
                if (Input.GetKeyDown(KeyCode.A)) MoveLeft();
                else if (Input.GetKeyDown(KeyCode.D)) MoveRight();
            }

            TickMovement();
        }

        private void ReadSwipe()
        {
            if (Input.touchCount == 0) return;

            Touch t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began)
            {
                pointerDown = true;
                pointerDownPos = t.position;
            }
            else if (pointerDown && (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled))
            {
                EvaluateSwipe(t.position);
                pointerDown = false;
            }
        }

        private void EvaluateSwipe(Vector2 endPos)
        {
            float deltaX = endPos.x - pointerDownPos.x;
            if (Mathf.Abs(deltaX) < minSwipeDistancePixels) return;   // short tap: no action
            if (deltaX < 0f) MoveLeft(); else MoveRight();
        }

        public void MoveLeft() => Step(-1);
        public void MoveRight() => Step(1);

        private void Step(int direction)
        {
            int next = currentLaneIndex.HasValue
                ? currentLaneIndex.Value + direction
                : (direction < 0 ? Mathf.FloorToInt(laneCoordinate) : Mathf.CeilToInt(laneCoordinate));

            next = Mathf.Clamp(next, 0, layout.LaneCount - 1);
            if (currentLaneIndex.HasValue && next == currentLaneIndex.Value) return;   // already at the edge

            currentLaneIndex = next;
            laneCoordinate = next + 0.5f;
            BeginMoveTo(layout.GetLaneCenterX(next));
        }

        private void BeginMoveTo(float x)
        {
            startX = transform.localPosition.x;
            targetX = x;
            moveElapsed = 0f;
            moveDuration = Mathf.Max(0.01f, laneChangeTime);
            moving = true;
        }

        private void TickMovement()
        {
            if (!moving) return;

            moveElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(moveElapsed / moveDuration);
            float eased = t * t * (3f - 2f * t);   // smoothstep, no per-frame allocation

            Vector3 p = transform.localPosition;
            p.x = Mathf.Lerp(startX, targetX, eased);
            transform.localPosition = p;

            if (t >= 1f) moving = false;
        }
    }
}
