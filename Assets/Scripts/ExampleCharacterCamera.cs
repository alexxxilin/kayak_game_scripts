using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YG;

namespace KinematicCharacterController.Examples
{
    public class ExampleCharacterCamera : MonoBehaviour
    {
        [Header("Framing")]
        public Camera Camera;
        public Vector2 FollowPointFraming = new Vector2(0f, 0f);
        public float FollowingSharpness = 10000f;

        [Header("Distance")]
        public float DefaultDistance = 6f;
        public float MinDistance = 1f;
        public float MaxDistance = 10f;
        public float ZoomSmoothness = 10f;
        public float ZoomSpeed = 3f;

        [Header("Rotation")]
        public bool InvertX = false;
        public bool InvertY = true;
        [Range(-90f, 90f)]
        public float DefaultVerticalAngle = 20f;
        [Range(-90f, 90f)]
        public float MinVerticalAngle = -90f;
        [Range(-90f, 90f)]
        public float MaxVerticalAngle = 90f;
        public float RotationSpeed = 1f;
        public float RotationSharpness = 10000f;
        public bool RotateWithPhysicsMover = false;

        [Header("Mouse Sensitivity")]
        public float mouseSensitivity = 1f;

        [Header("Obstruction")]
        public float ObstructionCheckRadius = 0.2f;
        public LayerMask ObstructionLayers = -1;
        public float ObstructionSharpness = 10000f;
        public List<Collider> IgnoredColliders = new List<Collider>();

        [Header("Camera Control Zone")]
        public Rect CameraControlRect = new Rect(0.5f, 0, 0.5f, 1f);
        public bool UseCameraControlZone = true;

        [Header("Character Hiding")]
        public List<GameObject> CharactersToHide = new List<GameObject>();
        public float HideAtZoomThreshold = 0.5f;
        public float HideCharacterHysteresis = 0.3f;
        public bool HideWholeGameObject = false;

        // Режим спуска – полностью отключён (оставлены поля для совместимости, но они не используются)
        // [Header("Descent Mode")] – удалён, так как функциональность не нужна

        public Transform Transform { get; private set; }
        public Transform FollowTransform { get; private set; }

        public Vector3 PlanarDirection { get; set; }
        public float TargetDistance { get; set; }

        private bool _distanceIsObstructed;
        private float _currentDistance;
        private float _targetVerticalAngle;
        private RaycastHit _obstructionHit;
        private int _obstructionCount;
        private RaycastHit[] _obstructions = new RaycastHit[MaxObstructions];
        private float _obstructionTime;
        private Vector3 _currentFollowPosition;
        private bool _isCharacterHidden = false;
        
        private float _zoomVelocity = 0f;
        private float _smoothZoomTarget = 0f;

        private Vector3 _shakeOffset = Vector3.zero;
        private Coroutine _shakeCoroutine;

        private const int MaxObstructions = 32;
        private Dictionary<GameObject, Renderer[]> _characterRenderersCache = new Dictionary<GameObject, Renderer[]>();

        void OnValidate()
        {
            DefaultDistance = Mathf.Clamp(DefaultDistance, MinDistance, MaxDistance);
            DefaultVerticalAngle = Mathf.Clamp(DefaultVerticalAngle, MinVerticalAngle, MaxVerticalAngle);
            HideAtZoomThreshold = Mathf.Max(0f, HideAtZoomThreshold);
            HideCharacterHysteresis = Mathf.Max(0f, HideCharacterHysteresis);
            ZoomSmoothness = Mathf.Max(1f, ZoomSmoothness);
            ZoomSpeed = Mathf.Max(0.1f, ZoomSpeed);
        }

        void Awake()
        {
            Transform = this.transform;
            _currentDistance = DefaultDistance;
            TargetDistance = DefaultDistance;
            _smoothZoomTarget = DefaultDistance;
            _targetVerticalAngle = DefaultVerticalAngle;
            PlanarDirection = Vector3.forward;
        }

        void Start()
        {
            CacheCharacterRenderers();
        }

        private void CacheCharacterRenderers()
        {
            _characterRenderersCache.Clear();
            if (CharactersToHide == null || CharactersToHide.Count == 0) return;

            foreach (var characterObj in CharactersToHide)
            {
                if (characterObj != null && !HideWholeGameObject)
                {
                    Renderer[] renderers = characterObj.GetComponentsInChildren<Renderer>();
                    _characterRenderersCache[characterObj] = renderers;
                }
            }
        }

        public void SetFollowTransform(Transform t)
        {
            FollowTransform = t;
            PlanarDirection = Vector3.ProjectOnPlane(FollowTransform.forward, Vector3.up).normalized;
            _currentFollowPosition = FollowTransform.position;
        }

        // Методы для совместимости с контроллером – не делают ничего
        public void SetDescentMode(bool enabled) { }
        public void SetAdditionalRoll(float roll) { }

        private bool IsPointInCameraControlZone(Vector2 screenPoint)
        {
            if (!UseCameraControlZone) return true;
            float x = screenPoint.x / Screen.width;
            float y = screenPoint.y / Screen.height;
            return CameraControlRect.Contains(new Vector2(x, y));
        }

        private bool IsPointOverJoystick(Vector2 screenPoint)
        {
            if (Character == null) return false;
            ExampleCharacterController characterController = Character.GetComponent<ExampleCharacterController>();
            if (characterController == null || characterController.MobileJoystick == null) return false;
            RectTransform joystickRect = characterController.MobileJoystick.GetComponent<RectTransform>();
            if (joystickRect == null) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(joystickRect, screenPoint);
        }

        public void UpdateWithInput(float deltaTime)
        {
            if (FollowTransform == null) return;

            Vector2 input = Vector2.zero;
            Vector2 currentMousePosition = Input.mousePosition;
            float scrollInput = 0f;
            bool isMobile = YG2.envir.isMobile;

            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch touch = Input.GetTouch(i);
                    if (touch.phase == TouchPhase.Moved)
                    {
                        if (UnityEngine.EventSystems.EventSystem.current != null &&
                            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                            continue;
                        input = touch.deltaPosition * 0.1f;
                        break;
                    }
                }
            }
            else if (!isMobile && Input.GetMouseButton(0))
            {
                bool isTouchingUI = false;
                if (UnityEngine.EventSystems.EventSystem.current != null)
                    isTouchingUI = UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

                if (!isTouchingUI)
                {
                    bool shouldCheckZone = Cursor.lockState != CursorLockMode.Locked;
                    if (!shouldCheckZone || IsPointInCameraControlZone(currentMousePosition))
                    {
                        input = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
                        scrollInput = Input.GetAxisRaw("Mouse ScrollWheel");
                    }
                }
            }
            else if (!isMobile && Cursor.lockState == CursorLockMode.Locked)
            {
                input = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
                scrollInput = Input.GetAxisRaw("Mouse ScrollWheel");
            }

            bool useInvertX = isMobile ? true : InvertX;
            bool useInvertY = isMobile ? true : InvertY;

            Vector3 rotationInput = new Vector3(input.x, input.y, 0f) * mouseSensitivity;
            if (useInvertX) rotationInput.x *= -1f;
            if (useInvertY) rotationInput.y *= -1f;

            ApplyCameraInput(deltaTime, rotationInput, scrollInput);
        }

        public void UpdateWithInput(float deltaTime, Vector3 rotationInput)
        {
            ApplyCameraInput(deltaTime, rotationInput, 0f);
        }

        public void UpdateWithInput(float deltaTime, float zoomInput, Vector3 rotationInput)
        {
            ApplyCameraInput(deltaTime, rotationInput, zoomInput);
        }

        private void ApplyCameraInput(float deltaTime, Vector3 rotationInput, float zoomInput)
        {
            if (zoomInput != 0f)
            {
                _smoothZoomTarget -= zoomInput * ZoomSpeed;
                _smoothZoomTarget = Mathf.Clamp(_smoothZoomTarget, MinDistance, MaxDistance);
            }

            TargetDistance = Mathf.Lerp(TargetDistance, _smoothZoomTarget, 1f - Mathf.Exp(-ZoomSmoothness * deltaTime));
            _currentDistance = Mathf.Lerp(_currentDistance, TargetDistance, 1f - Mathf.Exp(-ObstructionSharpness * deltaTime));

            float inputX = rotationInput.x * mouseSensitivity;
            float inputY = rotationInput.y * mouseSensitivity;
            if (InvertX) inputX *= -1f;
            if (InvertY) inputY *= -1f;

            Quaternion rotationFromInput = Quaternion.Euler(Vector3.up * (inputX * RotationSpeed));
            PlanarDirection = rotationFromInput * PlanarDirection;
            PlanarDirection = Vector3.Cross(Vector3.up, Vector3.Cross(PlanarDirection, Vector3.up));
            Quaternion planarRot = Quaternion.LookRotation(PlanarDirection, Vector3.up);

            _targetVerticalAngle -= (inputY * RotationSpeed);
            _targetVerticalAngle = Mathf.Clamp(_targetVerticalAngle, MinVerticalAngle, MaxVerticalAngle);
            Quaternion verticalRot = Quaternion.Euler(_targetVerticalAngle, 0, 0);
            Quaternion targetRotation = Quaternion.Slerp(
                Transform.rotation,
                planarRot * verticalRot,
                1f - Mathf.Exp(-RotationSharpness * deltaTime)
            );

            // Стандартное следование позиции – без задержки и сглаживания
            _currentFollowPosition = Vector3.Lerp(
                _currentFollowPosition,
                FollowTransform.position,
                1f - Mathf.Exp(-FollowingSharpness * deltaTime)
            );

            Transform.rotation = targetRotation;

            UpdateCharacterHidingByZoom();

            RaycastHit closestHit = new RaycastHit();
            closestHit.distance = Mathf.Infinity;
            _obstructionCount = Physics.SphereCastNonAlloc(
                _currentFollowPosition,
                ObstructionCheckRadius,
                -Transform.forward,
                _obstructions,
                _currentDistance,
                ObstructionLayers,
                QueryTriggerInteraction.Ignore
            );

            float adjustedDistance = _currentDistance;
            for (int i = 0; i < _obstructionCount; i++)
            {
                bool isIgnored = false;
                for (int j = 0; j < IgnoredColliders.Count; j++)
                {
                    if (IgnoredColliders[j] == _obstructions[i].collider)
                    {
                        isIgnored = true;
                        break;
                    }
                }

                if (!isIgnored && _obstructions[i].distance < closestHit.distance && _obstructions[i].distance > 0)
                {
                    closestHit = _obstructions[i];
                }
            }

            if (closestHit.distance < Mathf.Infinity)
            {
                adjustedDistance = closestHit.distance;
            }

            adjustedDistance = Mathf.Lerp(_currentDistance, adjustedDistance, 1f - Mathf.Exp(-ObstructionSharpness * deltaTime));

            Vector3 targetPosition = _currentFollowPosition - ((targetRotation * Vector3.forward) * adjustedDistance);
            targetPosition += Transform.right * FollowPointFraming.x;
            targetPosition += Transform.up * FollowPointFraming.y;
            targetPosition += _shakeOffset;
            Transform.position = targetPosition;
        }

        private void UpdateCharacterHidingByZoom()
        {
            if (CharactersToHide == null || CharactersToHide.Count == 0) return;

            float zoomThreshold = MinDistance + HideAtZoomThreshold;
            float showThreshold = zoomThreshold + HideCharacterHysteresis;

            if (!_isCharacterHidden)
            {
                if (_currentDistance <= zoomThreshold)
                {
                    HideCharacter();
                }
            }
            else
            {
                if (_currentDistance >= showThreshold)
                {
                    ShowCharacter();
                }
            }
        }

        private void HideCharacter()
        {
            if (CharactersToHide == null || CharactersToHide.Count == 0) return;

            foreach (var characterObj in CharactersToHide)
            {
                if (characterObj == null) continue;

                if (HideWholeGameObject)
                {
                    characterObj.SetActive(false);
                }
                else
                {
                    Renderer[] renderers;
                    if (!_characterRenderersCache.TryGetValue(characterObj, out renderers))
                    {
                        renderers = characterObj.GetComponentsInChildren<Renderer>();
                        _characterRenderersCache[characterObj] = renderers;
                    }

                    foreach (var renderer in renderers)
                    {
                        if (renderer != null) renderer.enabled = false;
                    }
                }
            }
            _isCharacterHidden = true;
        }

        private void ShowCharacter()
        {
            if (CharactersToHide == null || CharactersToHide.Count == 0) return;

            foreach (var characterObj in CharactersToHide)
            {
                if (characterObj == null) continue;

                if (HideWholeGameObject)
                {
                    characterObj.SetActive(true);
                }
                else
                {
                    Renderer[] renderers;
                    if (!_characterRenderersCache.TryGetValue(characterObj, out renderers))
                    {
                        renderers = characterObj.GetComponentsInChildren<Renderer>();
                        _characterRenderersCache[characterObj] = renderers;
                    }

                    foreach (var renderer in renderers)
                    {
                        if (renderer != null) renderer.enabled = true;
                    }
                }
            }
            _isCharacterHidden = false;
        }

        public void SetCharactersToHide(List<GameObject> characters)
        {
            CharactersToHide = characters;
            CacheCharacterRenderers();
        }

        public void AddCharacterToHide(GameObject character)
        {
            if (character != null && !CharactersToHide.Contains(character))
            {
                CharactersToHide.Add(character);
                CacheCharacterRenderers();
            }
        }

        public void RemoveCharacterToHide(GameObject character)
        {
            if (CharactersToHide.Contains(character))
            {
                CharactersToHide.Remove(character);
                if (_characterRenderersCache.ContainsKey(character))
                {
                    _characterRenderersCache.Remove(character);
                }
            }
        }

        public void ResetZoom()
        {
            _smoothZoomTarget = DefaultDistance;
            TargetDistance = DefaultDistance;
        }

        public void HitDownShake(float duration, float downDistance)
        {
            if (_shakeCoroutine != null) StopCoroutine(_shakeCoroutine);
            _shakeCoroutine = StartCoroutine(HitDownShakeCoroutine(duration, downDistance));
        }

        private IEnumerator HitDownShakeCoroutine(float duration, float downDistance)
        {
            float elapsed = 0f;
            Vector3 originalPos = _shakeOffset;
            Vector3 targetPos = originalPos + Vector3.down * downDistance;

            // Рывок вниз (30% времени)
            while (elapsed < duration * 0.3f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (duration * 0.3f);
                _shakeOffset = Vector3.Lerp(originalPos, targetPos, t);
                yield return null;
            }

            // Плавный возврат (70% времени)
            elapsed = 0f;
            while (elapsed < duration * 0.7f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (duration * 0.7f);
                _shakeOffset = Vector3.Lerp(targetPos, originalPos, t);
                yield return null;
            }

            _shakeOffset = originalPos;
            _shakeCoroutine = null;
        }

        private void OnDrawGizmosSelected()
        {
            if (!UseCameraControlZone || Camera == null) return;

            Vector3[] corners = new Vector3[4];
            corners[0] = Camera.ViewportToWorldPoint(new Vector3(CameraControlRect.x, CameraControlRect.y, 10));
            corners[1] = Camera.ViewportToWorldPoint(new Vector3(CameraControlRect.x + CameraControlRect.width, CameraControlRect.y, 10));
            corners[2] = Camera.ViewportToWorldPoint(new Vector3(CameraControlRect.x + CameraControlRect.width, CameraControlRect.y + CameraControlRect.height, 10));
            corners[3] = Camera.ViewportToWorldPoint(new Vector3(CameraControlRect.x, CameraControlRect.y + CameraControlRect.height, 10));

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(corners[0], corners[1]);
            Gizmos.DrawLine(corners[1], corners[2]);
            Gizmos.DrawLine(corners[2], corners[3]);
            Gizmos.DrawLine(corners[3], corners[0]);

            if (CharactersToHide != null && CharactersToHide.Count > 0 && CharactersToHide[0] != null)
            {
                GameObject referenceObject = CharactersToHide[0];
                float zoomThreshold = MinDistance + HideAtZoomThreshold;
                float showThreshold = zoomThreshold + HideCharacterHysteresis;
                
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(referenceObject.transform.position, zoomThreshold);
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(referenceObject.transform.position, showThreshold);
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(referenceObject.transform.position, _currentDistance);
            }
        }

        private GameObject Character;
        public void SetCharacterReference(GameObject character)
        {
            Character = character;
        }
    }
}