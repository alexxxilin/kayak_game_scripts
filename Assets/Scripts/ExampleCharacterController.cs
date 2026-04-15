using UnityEngine;
using UnityEngine.UI;
using KinematicCharacterController;
using System;
using System.Collections;
using System.Collections.Generic;
using YG;
using TMPro;

namespace KinematicCharacterController.Examples
{
    public struct PlayerCharacterInputs
    {
        public float MoveAxisForward;
        public float MoveAxisRight;
        public Quaternion CameraRotation;
        public bool JumpDown;
        public bool CrouchDown;
        public bool CrouchUp;
    }

    public struct AICharacterInputs
    {
        public Vector3 MoveVector;
        public Vector3 LookVector;
    }

    public enum CharacterState
    {
        Default,
        OnTrampoline,
        JumpingFromTrampoline,
        Climbing
    }

    public enum OrientationMethod
    {
        TowardsCamera,
        TowardsMovement,
        None
    }

    public enum BonusOrientationMethod
    {
        None,
        TowardsGravity,
        TowardsGroundSlopeAndGravity
    }

    [Serializable]
    public class TrampolineSettings
    {
        public float RunSpeed = 20f;
        public float TrampolineLength = 5f;
        public float BaseJumpForce = 15f;
        public AnimationClip RunningAnimation;
    }

    [Serializable]
    public class CoinSettings
    {
        public float HeightMultiplier = 20f;
        public Text CoinCounterText;
        public ParticleSystem CoinEffect;
        public AudioClip CoinSound;
        public TMP_Text RewardText;
        public float RewardDisplayTime = 3f;
    }

    [Serializable]
    public class ClimbingSettings
    {
        [Tooltip("Скорость подъема по лестнице")]
        public float ClimbSpeed = 3f;

        [Tooltip("Расстояние притягивания к лестнице")]
        public float SnapDistance = 1f;

        [Tooltip("Смещение от лестницы при подъеме")]
        public Vector3 MountOffset = new Vector3(0.5f, 0, 0);

        [Tooltip("Позиция для начала подъема (Y относительно земли)")]
        public float ClimbStartHeight = 0.5f;

        [Tooltip("Автоматический выход сверху лестницы (больше не используется)")]
        public bool AutoExitAtTop = false;

        [Tooltip("Разрешить спрыгивание с лестницы")]
        public bool AllowJumpOff = true;

        [Tooltip("Разрешить спуск по лестнице вниз")]
        public bool AllowClimbingDown = false;

        [Tooltip("Максимальный множитель скорости анимации лазания")]
        public float MaxClimbAnimationSpeed = 3f;

        [Header("Эффекты спуска")]
        [Tooltip("Начальный множитель скорости спуска (1 = базовая скорость)")]
        public float descentStartSpeedMultiplier = 1f;

        [Tooltip("Максимальный множитель скорости при спуске (в конце горки)")]
        public float descentMaxSpeedMultiplier = 10f;

        [Tooltip("Время разгона скорости и нарастания синусоиды (сек)")]
        public float descentAccelerationTime = 4f;

        [Tooltip("Множитель начальной частоты синусоиды (0 - полный штиль, 1 - сразу полная частота)")]
        [Range(0f, 1f)]
        public float descentStartFrequencyMultiplier = 0.1f;

        [Tooltip("Амплитуда виляния (смещение в стороны, метры)")]
        public float descentOscillationAmplitude = 0.5f;

        [Tooltip("Частота виляния (Гц)")]
        public float descentOscillationFrequency = 5f;

        [Tooltip("Максимальное смещение при вилянии (метры) – ограничитель, установите большим, если не нужен")]
        public float descentOscillationMaxOffset = 2f;

        [Tooltip("Длительность рывка камеры (сек)")]
        public float descentCameraShakeDuration = 0.3f;

        [Tooltip("Расстояние рывка камеры вниз (метры)")]
        public float descentCameraShakeDownDistance = 0.2f;

        [Header("Поворот и наклон при спуске")]
        [Tooltip("Максимальный угол поворота (градусы)")]
        public float descentRotationAmplitude = 30f;

        [Tooltip("Максимальный угол наклона (градусы)")]
        public float descentTiltAmplitude = 15f;

        [Tooltip("Множитель частоты для поворота (чем выше, тем быстрее повороты)")]
        public float descentRotationSpeedFactor = 1f;
    }

    [Serializable]
    public class LadderRewardSettings
    {
        public int ladderId = 0;
        public float coinsPerMeter = 1f;
    }

    public class ExampleCharacterController : MonoBehaviour, ICharacterController
    {
        [Header("Обязательные ссылки")]
        public KinematicCharacterMotor Motor;
        public Transform MeshRoot;
        public Transform CameraFollowPoint;
        public Animator CharacterAnimator;

        [Header("Настройки движения")]
        public float MaxStableMoveSpeed = 10f;
        public float StableMovementSharpness = 15f;
        public float OrientationSharpness = 10f;
        public OrientationMethod OrientationMethod = OrientationMethod.TowardsCamera;
        public float MaxAirMoveSpeed = 15f;
        public float AirAccelerationSpeed = 15f;
        public float Drag = 0.1f;

        [Header("Настройки прыжка")]
        public bool AllowJumpingWhenSliding = false;
        public float JumpUpSpeed = 10f;
        public float JumpScalableForwardSpeed = 10f;
        public float JumpPreGroundingGraceTime = 0f;
        public float JumpPostGroundingGraceTime = 0f;

        [Header("Настройки земли")]
        public List<Collider> IgnoredColliders = new List<Collider>();
        public BonusOrientationMethod BonusOrientationMethod = BonusOrientationMethod.None;
        public float BonusOrientationSharpness = 10f;
        public Vector3 Gravity = new Vector3(0, -30f, 0);
        public float CrouchedCapsuleHeight = 1f;

        [Header("Система батута")]
        public TrampolineSettings TrampolineSettings;

        [Header("Система монет")]
        public CoinSettings CoinSettings;
        public double CoinsCollected { get; private set; }

        [Header("Анимация получения монет")]
        public GameObject CoinFlyPrefab;
        public Canvas CoinFlyCanvas;
        public RectTransform CoinCounterTransform;
        public float CoinFlyDuration = 0.7f;
        public float CoinFlyDelay = 0.08f;
        public float CoinArcHeightMultiplier = 0.45f;
        public int MaxCoinsInAnimation = 5;
        public Vector2 CoinSpriteSize = new Vector2(40, 40);

        private Queue<GameObject> _coinPool = new Queue<GameObject>();
        private const int COIN_POOL_SIZE = 15;

        [Header("Система лестницы")]
        public ClimbingSettings ClimbingSettings;
        public LadderZone CurrentLadder { get; private set; }
        private Vector3 _ladderMountPosition;
        private bool _isClimbing = false;
        private bool _allowClimbingDown = false;

        private float _ladderCurrentT;
        private float _ladderStartT;
        private Vector3 _ladderDirection;
        private Vector3 _ladderNormal;
        private float _ladderLength;

        [Header("Настройки награды за лестницы")]
        public List<LadderRewardSettings> ladderRewards = new List<LadderRewardSettings>();

        [Header("Временные элементы для лестницы")]
        public TextMeshProUGUI TempClimbingCoinsText;
        public Button LadderJumpButton;

        [Header("Автоподъём (больше не используется)")]
        public Toggle AutoClimbToggle;

        [Header("Быстрое падение с лестницы")]
        public float fastFallGravityMultiplier = 5f;

        [Header("Кнопка остановки полета")]
        public GameObject stopFlightButton;
        public float stopFlightSpeedReduction = 0.5f;

        [Header("Настройки курсора")]
        public KeyCode cursorLockKey = KeyCode.Tab;
        public bool cursorLocked = true;
        private bool _cursorStateChangedThisFrame = false;

        [Header("Камера")]
        public ExampleCharacterCamera CameraController;

        [Header("Визуальная обратная связь")]
        public Renderer CharacterRenderer;
        public Color BaseColor = Color.white;
        public Color MaxBoostColor = Color.red;

        [Header("Mobile UI Elements")]
        public Joystick MobileJoystick;
        public GameObject JumpButton;
        public TextMeshProUGUI PCTextHint;

        [Header("Экономика")]
        public float baseCoinsPerMeter = 1f;

        private PetSystem _petSystem;
        private float _climbingStartHeight = 0f;
        private float _totalClimbedHeight = 0f;
        private float _tempClimbingCoins = 0f;
        private bool _uiOrAdIsOpen = false;
        private bool _savedCursorLockedState = true;
        private bool _isMoving;
        private float _distanceTraveled = 0f;
        private Vector3 _trampolineDirection;
        private bool _controlEnabled = true;
        private Vector3 _moveInputVector;
        private Vector3 _lookInputVector;
        private bool _jumpRequested = false;
        private bool _jumpConsumed = false;
        private bool _jumpedThisFrame = false;
        private float _timeSinceJumpRequested = Mathf.Infinity;
        private float _timeSinceLastAbleToJump = 0f;
        private Vector3 _internalVelocityAdd = Vector3.zero;
        private bool _isFastFalling = false;
        private Coroutine _rewardDisplayCoroutine;
        private Coroutine _climbingCoroutine;
        private Coroutine _waitForLandingCoroutine;
        private bool _isJumpingOffLadder = false;

        private float _currentClimbNormalizedTime = 0f;
        private bool _isMovingOnLadder = false;
        private bool _autoClimbEnabled = false;

        private Quaternion _targetLadderJumpRotation;
        private bool _isRotatingFromLadderJump = false;
        private float _ladderJumpRotationTime = 0f;
        private Vector3 _ladderJumpDirection;

        // Эффекты спуска
        private float _descentCurrentSpeedMultiplier = 1f;
        private float _descentStartT = 1f;
        private Vector3 _descentOscillationOffset = Vector3.zero;
        private float _descentCurrentYaw = 0f;
        private float _descentCurrentRoll = 0f;
        private float _descentTime = 0f;

        private LadderZone _currentAscentLadder;

        public float mouseSensitivity = 1f;
        private Material _characterMaterial;

        public CharacterState CurrentCharacterState { get; private set; }
        private float _wingsSpeedMultiplier = 1f;

        public void SetCurrentAscentLadder(LadderZone ladder) => _currentAscentLadder = ladder;
        public LadderZone GetCurrentAscentLadder() => _currentAscentLadder;

        private void Awake()
        {
            TransitionToState(CharacterState.Default);
            Motor.CharacterController = this;

            if (CharacterAnimator == null)
                CharacterAnimator = GetComponentInChildren<Animator>();

            if (CharacterRenderer != null)
            {
                _characterMaterial = CharacterRenderer.material;
                _characterMaterial.color = BaseColor;
            }

            if (stopFlightButton != null)
                stopFlightButton.SetActive(false);

            if (CoinSettings.RewardText != null)
                CoinSettings.RewardText.gameObject.SetActive(false);

            _petSystem = FindObjectOfType<PetSystem>();
            InitializeUIElements();
            InitializeCoinPool();
            UpdateCursorState();
            YG2.onPauseGame += OnGamePauseOrResume;
            UpdateUIForDeviceType();
        }

        private void InitializeUIElements()
        {
            if (TempClimbingCoinsText != null)
                TempClimbingCoinsText.gameObject.SetActive(false);

            if (LadderJumpButton != null)
            {
                LadderJumpButton.gameObject.SetActive(false);
                LadderJumpButton.onClick.RemoveAllListeners();
                LadderJumpButton.onClick.AddListener(OnLadderJumpButtonClick);
            }

            if (AutoClimbToggle != null)
            {
                AutoClimbToggle.gameObject.SetActive(false);
                AutoClimbToggle.onValueChanged.RemoveAllListeners();
            }
        }

        private void InitializeCoinPool()
        {
            if (!Application.isPlaying) return;
            if (CoinFlyPrefab == null || CoinFlyCanvas == null)
            {
                Debug.LogWarning("[Coin] Не настроены параметры анимации монет. Анимация отключена.");
                return;
            }

            for (int i = 0; i < COIN_POOL_SIZE; i++)
            {
                GameObject coin = Instantiate(CoinFlyPrefab, CoinFlyCanvas.transform);
                coin.name = $"CoinFly_{i}";
                coin.SetActive(false);
                RectTransform rt = coin.GetComponent<RectTransform>();
                if (rt != null) rt.sizeDelta = CoinSpriteSize;
                _coinPool.Enqueue(coin);
            }
        }

        private void AnimateCoinCollection(long amount, Vector3? sourcePosition = null)
        {
            if (!Application.isPlaying || amount <= 0 || CoinFlyPrefab == null || CoinFlyCanvas == null || CoinCounterTransform == null)
                return;

            int coinsToAnimate = Mathf.Min((int)amount, MaxCoinsInAnimation);

            Vector2 startPos;
            if (sourcePosition.HasValue && Camera.main != null)
            {
                Vector3 screenPoint = Camera.main.WorldToScreenPoint(sourcePosition.Value);
                startPos = new Vector2(screenPoint.x, screenPoint.y);
            }
            else
            {
                startPos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            }

            Vector2 canvasStartPos;
            Camera conversionCamera = (CoinFlyCanvas.renderMode != RenderMode.ScreenSpaceOverlay) ? CoinFlyCanvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)CoinFlyCanvas.transform,
                startPos,
                conversionCamera,
                out canvasStartPos))
            {
                canvasStartPos = Vector2.zero;
                Debug.LogWarning("[Coin] Не удалось конвертировать позицию источника");
            }

            Vector2 endPos = CoinCounterTransform.position;
            Vector2 endPosInCanvas;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)CoinFlyCanvas.transform,
                endPos,
                conversionCamera,
                out endPosInCanvas))
            {
                endPosInCanvas = Vector2.zero;
                Debug.LogWarning("[Coin] Не удалось конвертировать позицию счётчика");
            }

            Vector2 midPoint = (canvasStartPos + endPosInCanvas) * 0.5f;
            float distance = Vector2.Distance(canvasStartPos, endPosInCanvas);
            Vector2 controlPoint = midPoint + Vector2.up * (distance * CoinArcHeightMultiplier);

            StartCoroutine(AnimateCoinsSequentially(coinsToAnimate, canvasStartPos, controlPoint, endPosInCanvas));
        }

        private IEnumerator AnimateCoinsSequentially(int coinCount, Vector2 startPos, Vector2 controlPoint, Vector2 endPos)
        {
            for (int i = 0; i < coinCount; i++)
            {
                Vector2 offsetControlPoint = controlPoint + new Vector2(
                    UnityEngine.Random.Range(-30f, 30f),
                    UnityEngine.Random.Range(-20f, 40f)
                );

                GameObject coinObject = GetCoinFromPool();
                if (coinObject == null) continue;

                CoinFlyAnimation animation = coinObject.GetComponent<CoinFlyAnimation>();
                if (animation != null)
                {
                    float duration = CoinFlyDuration * UnityEngine.Random.Range(0.9f, 1.1f);
                    animation.StartAnimation(
                        startPos,
                        offsetControlPoint,
                        endPos,
                        duration,
                        () => ReturnCoinToPool(coinObject)
                    );
                }

                if (i < coinCount - 1)
                    yield return new WaitForSecondsRealtime(CoinFlyDelay);
            }
        }

        private GameObject GetCoinFromPool()
        {
            if (_coinPool.Count > 0)
                return _coinPool.Dequeue();
            else
            {
                GameObject newCoin = Instantiate(CoinFlyPrefab, CoinFlyCanvas.transform);
                newCoin.name = $"CoinFly_Dynamic_{Time.frameCount}";
                newCoin.GetComponent<RectTransform>().sizeDelta = CoinSpriteSize;
                Debug.LogWarning("[Coin] Пул исчерпан! Создан динамический объект.");
                return newCoin;
            }
        }

        private void ReturnCoinToPool(GameObject coin)
        {
            if (coin != null)
            {
                coin.SetActive(false);
                _coinPool.Enqueue(coin);
            }
        }

        private void OnDestroy()
        {
            if (_characterMaterial != null) Destroy(_characterMaterial);
            YG2.onPauseGame -= OnGamePauseOrResume;
            if (LadderJumpButton != null) LadderJumpButton.onClick.RemoveAllListeners();
            if (_climbingCoroutine != null) StopCoroutine(_climbingCoroutine);
            if (_waitForLandingCoroutine != null) StopCoroutine(_waitForLandingCoroutine);
        }

        private void OnApplicationPause(bool pauseStatus) { }
        private void OnGamePauseOrResume(bool isPaused) => Time.timeScale = isPaused ? 0f : 1f;

        private void UpdateUIForDeviceType()
        {
            bool isMobile = YG2.envir.isMobile;
            bool isDesktop = YG2.envir.isDesktop || !isMobile;
            if (MobileJoystick != null) MobileJoystick.gameObject.SetActive(isMobile);
            if (JumpButton != null) JumpButton.SetActive(isMobile);
            if (PCTextHint != null) PCTextHint.gameObject.SetActive(isDesktop);
        }

        public void OnUIOrAdOpened()
        {
            if (YG2.envir.isMobile) return;
            if (!_uiOrAdIsOpen)
            {
                _savedCursorLockedState = cursorLocked;
                SetCursorLocked(false);
                _uiOrAdIsOpen = true;
            }
        }

        public void OnUIOrAdClosed()
        {
            if (YG2.envir.isMobile) return;
            if (_uiOrAdIsOpen)
            {
                SetCursorLocked(_savedCursorLockedState);
                _uiOrAdIsOpen = false;
            }
        }

        private void HandleJumpInput()
        {
            if (!_controlEnabled && CurrentCharacterState != CharacterState.Climbing)
                return;

            if (CurrentCharacterState == CharacterState.Climbing && ClimbingSettings.AllowJumpOff)
            {
                if (_allowClimbingDown) return;
                JumpOffLadder();
                return;
            }

            _timeSinceJumpRequested = 0f;
            _jumpRequested = true;

            if (CharacterAnimator != null && Motor.GroundingStatus.IsStableOnGround)
                CharacterAnimator.SetTrigger("JumpTrigger");
        }

        public void RequestJump() => HandleJumpInput();

        private void JumpOffLadder()
        {
            if (CurrentLadder != null && CurrentLadder.ConnectedDescentLadder != null)
            {
                SwitchToDescentLadder(CurrentLadder.ConnectedDescentLadder);
                return;
            }

            if (CharacterAnimator != null)
                CharacterAnimator.SetBool("IsJumpingOffLadder", true);

            _ladderJumpDirection = _ladderNormal;
            _ladderJumpDirection.y = 0;
            _ladderJumpDirection.Normalize();

            _targetLadderJumpRotation = Quaternion.LookRotation(_ladderJumpDirection, Vector3.up);
            _isRotatingFromLadderJump = true;
            _ladderJumpRotationTime = 0f;

            Debug.Log($"[Ladder] Прыжок с лестницы. Направление: {_ladderJumpDirection}");

            StartCoroutine(DelayedExitLadder());
        }

        private void SwitchToDescentLadder(LadderZone descentLadder)
        {
            if (descentLadder == null) return;
            Debug.Log("[Ladder] Переход на спусковую горку");
            
            LadderZone oldLadder = CurrentLadder;
            
            AddClimbingCoinsToPermanent();
            HideClimbingUIElements();
            float currentT = _ladderCurrentT;
            Motor.SetGroundSolvingActivation(true);
            Motor.SetMovementCollisionsSolvingActivation(true);
            CurrentLadder = descentLadder;
            _allowClimbingDown = descentLadder.AllowClimbingDown;
            InitializeClimbingDataForLadder(descentLadder, currentT);
            
            Vector3 bottom = descentLadder.GetBottomPoint().position;
            Vector3 pointOnLine = bottom + _ladderDirection * (_ladderCurrentT * _ladderLength);
            Vector3 mountOffsetWorld = descentLadder.transform.TransformVector(descentLadder.GetMountOffset());
            Vector3 newPosition = pointOnLine + mountOffsetWorld;
            Motor.SetPosition(newPosition);
            
            Quaternion targetRotation = Quaternion.LookRotation(-_ladderDirection, _ladderNormal);
            if (MeshRoot != null)
                MeshRoot.rotation = targetRotation;
            
            if (CurrentCharacterState != CharacterState.Climbing)
                TransitionToState(CharacterState.Climbing);
            else
            {
                if (CharacterAnimator != null)
                {
                    CharacterAnimator.Play("Climbing");
                    CharacterAnimator.SetBool("IsGrounded", false);
                    CharacterAnimator.SetBool("IsClimbing", true);
                }
            }
            
            _isJumpingOffLadder = false;
            _jumpRequested = false;
            _descentCurrentYaw = 0f;
            _descentCurrentRoll = 0f;
            _descentCurrentSpeedMultiplier = ClimbingSettings.descentStartSpeedMultiplier;
            _descentTime = 0f;
            
            ApplyWingsMultiplier();
            ApplyLadderRewardSettings();
            _totalClimbedHeight = 0f;
            _tempClimbingCoins = 0f;
            UpdateTempCoinsDisplay();

            oldLadder?.OnPlayerExitedLadder();
            descentLadder.ForceHideGates();
        }

        private IEnumerator DelayedExitLadder()
        {
            yield return new WaitForSeconds(0.1f);
            ExitLadder(true);
        }

        private void ExitLadder(bool jumpedOff, bool completedClimb = false)
        {
            if (CurrentCharacterState != CharacterState.Climbing) return;

            if (!completedClimb || jumpedOff)
            {
                SetCurrentAscentLadder(null);
            }

            HideClimbingUIElements();
            AddClimbingCoinsToPermanent();

            LadderZone ladderRef = CurrentLadder;
            CurrentLadder = null;
            _isClimbing = false;
            _allowClimbingDown = false;

            Motor.SetGroundSolvingActivation(true);
            Motor.SetMovementCollisionsSolvingActivation(true);

            if (jumpedOff)
            {
                DisableControl();
                _isJumpingOffLadder = true;
                _isFastFalling = true;

                if (_ladderJumpDirection == Vector3.zero)
                {
                    _ladderJumpDirection = -transform.forward;
                    _ladderJumpDirection.y = 0;
                    _ladderJumpDirection.Normalize();
                }

                Vector3 currentPos = Motor.TransientPosition;
                Vector3 targetPosition = currentPos + _ladderJumpDirection * 5f;
                targetPosition.y = currentPos.y;
                Motor.SetPosition(targetPosition);

                float jumpForce = 8f;
                AddVelocity(Vector3.up * jumpForce);

                Motor.ForceUnground();
                TransitionToState(CharacterState.Default);

                if (ladderRef != null)
                {
                    ApplyWingsMultiplier();
                }

                if (_waitForLandingCoroutine != null)
                    StopCoroutine(_waitForLandingCoroutine);
                _waitForLandingCoroutine = StartCoroutine(WaitForLandingAfterJumpOff());

                if (CharacterAnimator != null)
                {
                    CharacterAnimator.SetBool("IsGrounded", false);
                    CharacterAnimator.SetBool("IsFastFalling", true);
                    CharacterAnimator.SetBool("IsJumpingOffLadder", true);
                    CharacterAnimator.SetBool("IsClimbing", false);
                }
                ladderRef?.OnPlayerExitedLadder();
            }
            else
            {
                TransitionToState(CharacterState.Default);
                if (CharacterAnimator != null)
                {
                    CharacterAnimator.SetBool("IsClimbing", false);
                    CharacterAnimator.SetBool("IsJumpingOffLadder", false);
                    CharacterAnimator.SetBool("IsGrounded", Motor.GroundingStatus.IsStableOnGround);
                }
                ladderRef?.OnPlayerExitedLadder();
            }
        }

        private void CompleteDescent()
        {
            if (CurrentCharacterState != CharacterState.Climbing) return;

            HideClimbingUIElements();
            AddClimbingCoinsToPermanent();

            LadderZone ladderRef = CurrentLadder;
            if (ladderRef == null) return;

            float pushDistance = ladderRef.JumpOffHorizontalDistance;
            float pushTime = 0.3f;
            float pushSpeed = pushDistance / pushTime;

            Vector3 pushDirection = _ladderNormal;
            pushDirection.y = 0.2f;
            pushDirection.Normalize();
            Vector3 pushVelocity = pushDirection * pushSpeed;
            AddVelocity(pushVelocity);

            CurrentLadder = null;
            _isClimbing = false;
            _allowClimbingDown = false;

            Motor.SetGroundSolvingActivation(true);
            Motor.SetMovementCollisionsSolvingActivation(true);
            Motor.ForceUnground();
            TransitionToState(CharacterState.Default);

            ApplyWingsMultiplier();

            if (CharacterAnimator != null)
            {
                CharacterAnimator.SetBool("IsClimbing", false);
                CharacterAnimator.SetBool("IsGrounded", Motor.GroundingStatus.IsStableOnGround);
            }

            ladderRef.OnPlayerExitedLadder();
        }

        private IEnumerator WaitForLandingAfterJumpOff()
        {
            while (!Motor.GroundingStatus.IsStableOnGround)
                yield return null;

            _isJumpingOffLadder = false;
            _isFastFalling = false;
            _isRotatingFromLadderJump = false;
            _ladderJumpDirection = Vector3.zero;
            EnableControl();

            if (CharacterAnimator != null)
            {
                CharacterAnimator.SetBool("IsGrounded", true);
                CharacterAnimator.SetBool("IsFastFalling", false);
                CharacterAnimator.SetBool("IsJumpingOffLadder", false);
                CharacterAnimator.SetBool("IsClimbing", false);
            }

            _waitForLandingCoroutine = null;
        }

        public void StartClimbingFromTop(LadderZone ladder)
        {
            if (CurrentCharacterState == CharacterState.Climbing) return;

            SetCurrentAscentLadder(null);

            SetupClimbingState(ladder, true);
            _climbingStartHeight = Motor.TransientPosition.y;
            _totalClimbedHeight = 0f;
            _tempClimbingCoins = 0f;

            InitializeClimbingDataFromTop(ladder);
        }

        private void SetupClimbingState(LadderZone ladder, bool allowDown)
        {
            _allowClimbingDown = allowDown;
            DisableControl();
            Motor.SetGroundSolvingActivation(false);
            Motor.SetMovementCollisionsSolvingActivation(false);
            CurrentLadder = ladder;
            TransitionToState(CharacterState.Climbing);
            ApplyLadderRewardSettings();
        }

        private void InitializeClimbingDataFromTop(LadderZone ladder)
        {
            if (ladder == null) return;

            var bottom = ladder.GetBottomPoint().position;
            var top = ladder.GetTopPoint().position;

            _ladderDirection = (top - bottom).normalized;
            _ladderLength = Vector3.Distance(bottom, top);
            _ladderNormal = ladder.transform.forward;

            _ladderCurrentT = 1f;
            _ladderStartT = 1f;

            Vector3 pointOnLine = top;
            Vector3 mountOffsetWorld = ladder.transform.TransformVector(ladder.GetMountOffset());
            _ladderMountPosition = pointOnLine + mountOffsetWorld;

            if (_climbingCoroutine != null) StopCoroutine(_climbingCoroutine);
            _climbingCoroutine = StartCoroutine(SnapToLadderPosition());
        }

        public void StartClimbing(LadderZone ladder, bool allowDown = false)
        {
            if (CurrentCharacterState == CharacterState.Climbing) return;

            if (!allowDown)
            {
                SetCurrentAscentLadder(ladder);
            }

            SetupClimbingState(ladder, allowDown);
            _climbingStartHeight = Motor.TransientPosition.y;
            _totalClimbedHeight = 0f;
            _tempClimbingCoins = 0f;

            InitializeClimbingData();
        }

        public void StopClimbing()
        {
            if (CurrentCharacterState != CharacterState.Climbing) return;
            ExitLadder(false);
        }

        private void ApplyLadderRewardSettings()
        {
            if (CurrentLadder == null) return;
            int currentLadderId = CurrentLadder.LadderId;
            float reward = GetCoinsPerMeterForLadder(currentLadderId);
            Debug.Log($"[LadderReward] Применена награда для лестницы ID={currentLadderId}: {reward} монет/метр");
        }

        private float GetCoinsPerMeterForLadder(int ladderId)
        {
            foreach (var reward in ladderRewards)
                if (reward.ladderId == ladderId)
                    return reward.coinsPerMeter;

            Debug.LogWarning($"[LadderReward] Не найдена награда для лестницы ID={ladderId}. Используется базовая награда {baseCoinsPerMeter} монет/метр.");
            return baseCoinsPerMeter;
        }

        private void CalculateClimbingCoins()
        {
            if (_totalClimbedHeight > 0 && CurrentLadder != null)
            {
                float petMultiplier = _petSystem != null ? _petSystem.GetCurrentRocketMultiplier() : 1f;
                float coinsPerMeter = GetCoinsPerMeterForLadder(CurrentLadder.LadderId);
                float vipMultiplier = CurrentLadder.IsVIP ? CurrentLadder.CoinsMultiplier : 1f;
                float newCoins = _totalClimbedHeight * coinsPerMeter * petMultiplier * vipMultiplier;

                if (newCoins > _tempClimbingCoins)
                {
                    _tempClimbingCoins = newCoins;
                    UpdateTempCoinsDisplay();
                }
            }
        }

        private void UpdateTempCoinsDisplay()
        {
            if (TempClimbingCoinsText != null)
            {
                if (_tempClimbingCoins > 0)
                {
                    TempClimbingCoinsText.text = $"+{FormatNumber((long)_tempClimbingCoins)}";
                    TempClimbingCoinsText.gameObject.SetActive(true);
                }
                else
                {
                    TempClimbingCoinsText.gameObject.SetActive(false);
                }
            }
        }

        private void AddClimbingCoinsToPermanent()
        {
            if (_tempClimbingCoins > 0)
            {
                long coinsToAdd = (long)_tempClimbingCoins;
                AddCoins(coinsToAdd);

                if (CoinSettings.RewardText != null)
                {
                    if (_rewardDisplayCoroutine != null) StopCoroutine(_rewardDisplayCoroutine);
                    _rewardDisplayCoroutine = StartCoroutine(ShowClimbingRewardCoroutine(coinsToAdd));
                }

                _tempClimbingCoins = 0f;
                _totalClimbedHeight = 0f;
            }
        }

        private IEnumerator ShowClimbingRewardCoroutine(long rewardAmount)
        {
            CoinSettings.RewardText.text = $"+{FormatNumber(rewardAmount)}";
            CoinSettings.RewardText.gameObject.SetActive(true);
            yield return new WaitForSeconds(2f);
            CoinSettings.RewardText.gameObject.SetActive(false);
        }

        private void HoldLadderPosition()
        {
            if (CurrentLadder == null) return;

            Vector3 bottom = CurrentLadder.GetBottomPoint().position;
            Vector3 pointOnLine = bottom + _ladderDirection * (_ladderCurrentT * _ladderLength);
            Vector3 mountOffsetWorld = CurrentLadder.transform.TransformVector(CurrentLadder.GetMountOffset());
            Vector3 newPosition = pointOnLine + mountOffsetWorld;
            Motor.SetPosition(newPosition);

            if (CharacterAnimator != null)
            {
                CharacterAnimator.SetFloat("ClimbSpeed", 0f);
                CharacterAnimator.SetBool("IsClimbing", true);
                CharacterAnimator.SetBool("IsGrounded", false);
            }
        }

        private void OnLadderJumpButtonClick()
        {
            if (CurrentCharacterState == CharacterState.Climbing && ClimbingSettings.AllowJumpOff)
                JumpOffLadder();
        }

        private void Update()
        {
            UpdateCoinCounter();

            if (CameraController != null)
                CameraController.UpdateWithInput(Time.deltaTime);

            if (CurrentCharacterState == CharacterState.Climbing)
            {
                if (Input.GetKeyDown(KeyCode.Space))
                    HandleJumpInput();

                if (_isMovingOnLadder && CharacterAnimator != null)
                {
                    AnimatorStateInfo stateInfo = CharacterAnimator.GetCurrentAnimatorStateInfo(0);
                    if (stateInfo.IsName("Climbing"))
                        _currentClimbNormalizedTime = stateInfo.normalizedTime;
                }
                HandleClimbingInput();
            }
            else
            {
                if (Input.GetKeyDown(KeyCode.Space))
                    HandleJumpInput();
            }

            UpdateFastFallAnimation();
            UpdateStopFlightButton();
            _cursorStateChangedThisFrame = false;
        }

        private void LateUpdate()
        {
            if (!_cursorStateChangedThisFrame)
                UpdateCursorState();
        }

        public void UpdateCursorState()
        {
            if (YG2.envir.isMobile)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                if (UnityEngine.EventSystems.EventSystem.current != null)
                    UnityEngine.EventSystems.EventSystem.current.sendNavigationEvents = false;
                return;
            }

            if (_uiOrAdIsOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                if (UnityEngine.EventSystems.EventSystem.current != null)
                    UnityEngine.EventSystems.EventSystem.current.sendNavigationEvents = true;
                return;
            }

            Cursor.lockState = cursorLocked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !cursorLocked;

            if (UnityEngine.EventSystems.EventSystem.current != null)
            {
                if (cursorLocked)
                {
                    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
                    UnityEngine.EventSystems.EventSystem.current.sendNavigationEvents = false;
                }
                else
                {
                    UnityEngine.EventSystems.EventSystem.current.sendNavigationEvents = true;
                }
            }
        }

        private void UpdateFastFallAnimation()
        {
            if (CharacterAnimator != null)
                CharacterAnimator.SetBool("IsFastFalling", _isFastFalling);
        }

        private void UpdateStopFlightButton()
        {
            if (stopFlightButton != null)
                stopFlightButton.SetActive(false);
        }

        public int SilverCoins => YG2.saves.silverCoins;
        public void SetSilverCoins(int amount)
        {
            if (YG2.saves.silverCoins == amount) return;
            YG2.saves.silverCoins = amount;
            YG2.SaveProgress();
        }
        public void AddSilverCoins(int amount)
        {
            if (amount <= 0) return;
            YG2.saves.silverCoins += amount;
            YG2.SaveProgress();
        }
        public bool TrySpendSilverCoins(int amount)
        {
            if (YG2.saves.silverCoins >= amount)
            {
                YG2.saves.silverCoins -= amount;
                YG2.SaveProgress();
                return true;
            }
            return false;
        }

        private string FormatNumber(double number)
        {
            if (number >= 1e12) return (number / 1e12).ToString("F1") + "T";
            if (number >= 1e9) return (number / 1e9).ToString("F1") + "B";
            if (number >= 1e6) return (number / 1e6).ToString("F1") + "M";
            if (number >= 1e3) return (number / 1e3).ToString("F1") + "K";
            return ((long)number).ToString();
        }

        public bool SpendCoins(double amount)
        {
            if (HasEnoughCoins(amount))
            {
                CoinsCollected -= amount;
                UpdateCoinCounter();
                return true;
            }
            return false;
        }

        public void UpdateCoinCounter()
        {
            if (CoinSettings.CoinCounterText != null)
                CoinSettings.CoinCounterText.text = FormatNumber(CoinsCollected);
        }

        public bool HasEnoughCoins(double amount) => CoinsCollected >= amount;

        public void AddCoins(long amount)
        {
            if (amount <= 0) return;
            CoinsCollected += amount;
            UpdateCoinCounter();
            AnimateCoinCollection(amount);
            if (CoinSettings.CoinEffect != null)
            {
                ParticleSystem effect = Instantiate(CoinSettings.CoinEffect, transform.position, Quaternion.identity);
                Destroy(effect.gameObject, effect.main.duration);
            }
            if (CoinSettings.CoinSound != null)
                AudioSource.PlayClipAtPoint(CoinSettings.CoinSound, transform.position);
        }

        private void OnLanded()
        {
            if (CharacterAnimator != null)
            {
                CharacterAnimator.SetBool("IsGrounded", true);
                CharacterAnimator.SetBool("IsFastFalling", false);
            }
            if (CurrentCharacterState == CharacterState.JumpingFromTrampoline)
            {
                EnableControl();
                TransitionToState(CharacterState.Default);
            }
        }

        private void OnLeaveStableGround()
        {
            if (CharacterAnimator != null)
                CharacterAnimator.SetBool("IsGrounded", false);
        }

        public void TransitionToState(CharacterState newState)
        {
            CharacterState tmpInitialState = CurrentCharacterState;
            OnStateExit(tmpInitialState, newState);
            CurrentCharacterState = newState;
            OnStateEnter(newState, tmpInitialState);
        }

        public void OnStateEnter(CharacterState state, CharacterState fromState)
        {
            switch (state)
            {
                case CharacterState.Default:
                    _isFastFalling = false;
                    _isClimbing = false;
                    EnableControl();
                    HideClimbingUIElements();
                    break;
                case CharacterState.OnTrampoline:
                    _trampolineDirection = Motor.CharacterForward;
                    _distanceTraveled = 0f;
                    DisableControl();
                    if (CharacterAnimator != null && TrampolineSettings.RunningAnimation != null)
                        CharacterAnimator.Play(TrampolineSettings.RunningAnimation.name);
                    break;
                case CharacterState.JumpingFromTrampoline:
                    break;
                case CharacterState.Climbing:
                    StartClimbingEnter();
                    break;
            }
        }

        private void StartClimbingEnter()
        {
            if (CurrentLadder == null) return;

            _isClimbing = true;
            DisableControl();

            Motor.SetGroundSolvingActivation(false);
            Motor.SetMovementCollisionsSolvingActivation(false);

            InitializeClimbingData();

            Vector3 bottom = CurrentLadder.GetBottomPoint().position;
            Vector3 pointOnLine = bottom + _ladderDirection * (_ladderCurrentT * _ladderLength);
            Vector3 mountOffsetWorld = CurrentLadder.transform.TransformVector(CurrentLadder.GetMountOffset());
            _ladderMountPosition = pointOnLine + mountOffsetWorld;

            if (_climbingCoroutine != null) StopCoroutine(_climbingCoroutine);
            _climbingCoroutine = StartCoroutine(SnapToLadderPosition());

            if (!_allowClimbingDown)
                ShowClimbingUIElements();
            else
                HideClimbingUIElements();

            _currentClimbNormalizedTime = 0f;
            if (CharacterAnimator != null)
            {
                CharacterAnimator.Play("Climbing");
                CharacterAnimator.SetBool("IsGrounded", false);
            }

            ApplyWingsMultiplier();
            ApplyLadderRewardSettings();

            // Инициализация для спуска
            if (_allowClimbingDown)
            {
                _descentTime = 0f;
                _descentCurrentSpeedMultiplier = ClimbingSettings.descentStartSpeedMultiplier;
            }
        }

        private void InitializeClimbingData()
        {
            if (CurrentLadder == null) return;
            float t = CurrentLadder.GetProjection(Motor.TransientPosition);
            InitializeClimbingDataForLadder(CurrentLadder, t);
        }

        private void InitializeClimbingDataForLadder(LadderZone ladder, float normalizedT)
        {
            if (ladder == null) return;

            var bottom = ladder.GetBottomPoint().position;
            var top = ladder.GetTopPoint().position;

            _ladderDirection = (top - bottom).normalized;
            _ladderLength = Vector3.Distance(bottom, top);
            _ladderNormal = ladder.transform.forward;

            _ladderCurrentT = Mathf.Clamp01(normalizedT);
            _ladderStartT = _ladderCurrentT;
        }

        private void ShowClimbingUIElements()
        {
            if (_allowClimbingDown) return;
            if (LadderJumpButton != null) LadderJumpButton.gameObject.SetActive(true);
            if (TempClimbingCoinsText != null) TempClimbingCoinsText.gameObject.SetActive(true);
        }

        private void HideClimbingUIElements()
        {
            if (LadderJumpButton != null) LadderJumpButton.gameObject.SetActive(false);
            if (TempClimbingCoinsText != null) TempClimbingCoinsText.gameObject.SetActive(false);
        }

        private IEnumerator SnapToLadderPosition()
        {
            Vector3 startPos = Motor.TransientPosition;
            Vector3 targetPos = _ladderMountPosition;
            float elapsed = 0f;
            float duration = 0.3f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float easeT = 1f - Mathf.Pow(1f - t, 3f);
                Vector3 newPos = Vector3.Lerp(startPos, targetPos, easeT);
                Motor.SetPosition(newPos);
                yield return null;
            }
            Motor.SetPosition(targetPos);
        }

        public void OnStateExit(CharacterState state, CharacterState toState)
        {
            switch (state)
            {
                case CharacterState.Climbing:
                    _isClimbing = false;
                    _allowClimbingDown = false;
                    _descentCurrentSpeedMultiplier = 1f;
                    _descentOscillationOffset = Vector3.zero;
                    _descentCurrentYaw = 0f;
                    _descentCurrentRoll = 0f;
                    _descentTime = 0f;
                    if (_climbingCoroutine != null)
                    {
                        StopCoroutine(_climbingCoroutine);
                        _climbingCoroutine = null;
                    }
                    if (CharacterAnimator != null)
                    {
                        CharacterAnimator.SetBool("IsClimbing", false);
                        CharacterAnimator.SetBool("IsJumpingOffLadder", false);
                        CharacterAnimator.SetFloat("ClimbSpeed", 0f);
                    }
                    ApplyWingsMultiplier();
                    break;
            }
        }

        public void DisableControl() => _controlEnabled = false;
        public void EnableControl() => _controlEnabled = true;

        public void SetWingsSpeedMultiplier(float multiplier) => _wingsSpeedMultiplier = Mathf.Max(1f, multiplier);

        public void SetAutoClimbEnabled(bool enabled) { }

        private void HandleClimbingInput()
        {
            if (CurrentCharacterState != CharacterState.Climbing || CurrentLadder == null) return;

            float verticalInput = _allowClimbingDown ? -1f : 1f;
            _isMovingOnLadder = true;
            PerformClimb(verticalInput);
        }

        private void PerformClimb(float verticalInput)
        {
            if (CurrentLadder == null) return;

            if (_allowClimbingDown && !_isMovingOnLadder && verticalInput < 0)
            {
                _descentStartT = _ladderCurrentT;
                _descentCurrentSpeedMultiplier = ClimbingSettings.descentStartSpeedMultiplier;
                _descentTime = 0f;
            }

            float movement;

            if (_allowClimbingDown && verticalInput < 0)
            {
                // Линейное увеличение множителя от start до max за время descentAccelerationTime
                float t = Mathf.Clamp01(_descentTime / ClimbingSettings.descentAccelerationTime);
                _descentCurrentSpeedMultiplier = Mathf.Lerp(ClimbingSettings.descentStartSpeedMultiplier, ClimbingSettings.descentMaxSpeedMultiplier, t);
                movement = verticalInput * ClimbingSettings.ClimbSpeed * _descentCurrentSpeedMultiplier * Time.deltaTime;
                _descentTime += Time.deltaTime;
            }
            else
            {
                movement = verticalInput * ClimbingSettings.ClimbSpeed * _wingsSpeedMultiplier * Time.deltaTime;
            }

            float deltaT = movement / _ladderLength;
            float newT = _ladderCurrentT + deltaT;

            bool reachedTop = false, reachedBottom = false;
            if (newT > 1f)
            {
                newT = 1f;
                reachedTop = true;
            }
            else if (newT < 0f)
            {
                newT = 0f;
                reachedBottom = true;
            }

            if (verticalInput > 0)
            {
                float climbedThisFrame = Mathf.Abs((newT - _ladderCurrentT) * _ladderLength);
                _totalClimbedHeight += climbedThisFrame;
                CalculateClimbingCoins();
            }

            _ladderCurrentT = newT;

            Vector3 bottom = CurrentLadder.GetBottomPoint().position;
            Vector3 pointOnLine = bottom + _ladderDirection * (_ladderCurrentT * _ladderLength);
            Vector3 mountOffsetWorld = CurrentLadder.transform.TransformVector(CurrentLadder.GetMountOffset());
            Vector3 newPosition = pointOnLine + mountOffsetWorld;

            // Синусоида (виляние) – зависит только от времени, а не от скорости
            if (_allowClimbingDown && verticalInput < 0 && ClimbingSettings.descentOscillationAmplitude > 0)
            {
                Vector3 lateralAxis = Vector3.Cross(_ladderDirection, _ladderNormal).normalized;
                float t = Mathf.Clamp01(_descentTime / ClimbingSettings.descentAccelerationTime);
                float amplitudeFactor = t;
                float frequency = ClimbingSettings.descentOscillationFrequency * Mathf.Lerp(ClimbingSettings.descentStartFrequencyMultiplier, 1f, t);
                float phase = Time.time * frequency * ClimbingSettings.descentRotationSpeedFactor;
                float sinVal = Mathf.Sin(phase);
                float cosVal = Mathf.Cos(phase);

                float oscillation = sinVal * ClimbingSettings.descentOscillationAmplitude * amplitudeFactor;
                oscillation = Mathf.Clamp(oscillation, -ClimbingSettings.descentOscillationMaxOffset, ClimbingSettings.descentOscillationMaxOffset);
                _descentOscillationOffset = lateralAxis * oscillation;
                newPosition += _descentOscillationOffset;

                _descentCurrentYaw = cosVal * ClimbingSettings.descentRotationAmplitude * amplitudeFactor;
                _descentCurrentRoll = -sinVal * ClimbingSettings.descentTiltAmplitude * amplitudeFactor;
            }
            else
            {
                _descentOscillationOffset = Vector3.zero;
                _descentCurrentYaw = 0f;
                _descentCurrentRoll = 0f;
            }

            Motor.SetPosition(newPosition);

            if (reachedTop)
            {
                ExitLadder(false, true);
                return;
            }

            if (reachedBottom)
            {
                if (_allowClimbingDown)
                {
                    CameraController?.HitDownShake(ClimbingSettings.descentCameraShakeDuration, ClimbingSettings.descentCameraShakeDownDistance);
                    CompleteDescent();
                }
                else
                {
                    ExitLadder(false, false);
                }
                return;
            }

            float animationSpeed = Mathf.Abs(verticalInput);
            if (_allowClimbingDown)
                animationSpeed *= _descentCurrentSpeedMultiplier;
            else
                animationSpeed *= _wingsSpeedMultiplier;

            animationSpeed = Mathf.Min(animationSpeed, ClimbingSettings.MaxClimbAnimationSpeed);
            if (CharacterAnimator != null)
            {
                CharacterAnimator.SetFloat("ClimbSpeed", animationSpeed);
                CharacterAnimator.SetBool("IsClimbing", true);
                CharacterAnimator.SetBool("IsGrounded", false);
            }
        }

        private void ApplyWingsMultiplier()
        {
            var wings = FindFirstObjectByType<WingsSystem>();
            if (wings != null)
            {
                if (wings.equippedWingIndex >= 0 && wings.equippedWingIndex < wings.wingLevels.Count)
                {
                    var wing = wings.wingLevels[wings.equippedWingIndex];
                    if (CurrentLadder is LadderZone currentLadder &&
                        wing.requiredLadderId == currentLadder.LadderId)
                    {
                        _wingsSpeedMultiplier = wing.speedMultiplier;
                    }
                    else
                    {
                        _wingsSpeedMultiplier = 1f;
                    }
                }
                else
                {
                    _wingsSpeedMultiplier = 1f;
                }
            }
        }

        public float GetWingsSpeedMultiplier() => _wingsSpeedMultiplier;

        private void SetCameraDescentMode(bool enabled) { }
        private void UpdateCameraRoll() { }

        public void BeforeCharacterUpdate(float deltaTime) { }

        public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            if (_isRotatingFromLadderJump)
            {
                _ladderJumpRotationTime += deltaTime;
                float t = Mathf.Clamp01(_ladderJumpRotationTime / 0.3f);
                currentRotation = Quaternion.Slerp(currentRotation, _targetLadderJumpRotation, t);
                if (MeshRoot != null) MeshRoot.rotation = currentRotation;
                return;
            }

            if (CurrentCharacterState == CharacterState.Climbing && CurrentLadder != null)
            {
                Vector3 forward = _allowClimbingDown ? -_ladderDirection : _ladderDirection;
                Vector3 up = _ladderNormal;
                Quaternion targetRotation = Quaternion.LookRotation(forward, up);

                if (_allowClimbingDown && (_descentCurrentYaw != 0f || _descentCurrentRoll != 0f))
                {
                    Quaternion yawRot = Quaternion.AngleAxis(_descentCurrentYaw, targetRotation * Vector3.up);
                    Quaternion rollRot = Quaternion.AngleAxis(_descentCurrentRoll, targetRotation * Vector3.forward);
                    targetRotation = yawRot * rollRot * targetRotation;
                }

                currentRotation = Quaternion.Slerp(currentRotation, targetRotation,
                    1 - Mathf.Exp(-OrientationSharpness * deltaTime));
                if (MeshRoot != null)
                {
                    MeshRoot.rotation = currentRotation;
                }
                return;
            }

            if (_lookInputVector.sqrMagnitude > 0f && OrientationSharpness > 0f)
            {
                Vector3 smoothedLookInputDirection = Vector3.Slerp(
                    Motor.CharacterForward,
                    _lookInputVector,
                    1 - Mathf.Exp(-OrientationSharpness * deltaTime)).normalized;
                currentRotation = Quaternion.LookRotation(smoothedLookInputDirection, Motor.CharacterUp);
                if (MeshRoot != null) MeshRoot.rotation = currentRotation;
            }

            Vector3 currentUp = currentRotation * Vector3.up;

            switch (BonusOrientationMethod)
            {
                case BonusOrientationMethod.TowardsGravity:
                    {
                        Vector3 gravityDirection = Vector3.Slerp(
                            currentUp,
                            -Gravity.normalized,
                            1 - Mathf.Exp(-BonusOrientationSharpness * deltaTime));
                        currentRotation = Quaternion.FromToRotation(currentUp, gravityDirection) * currentRotation;
                        if (MeshRoot != null) MeshRoot.rotation = currentRotation;
                    }
                    break;

                case BonusOrientationMethod.TowardsGroundSlopeAndGravity:
                    if (Motor.GroundingStatus.IsStableOnGround)
                    {
                        Vector3 initialCharacterBottom = Motor.TransientPosition + (currentUp * Motor.Capsule.radius);
                        Vector3 groundNormalDirection = Vector3.Slerp(
                            Motor.CharacterUp,
                            Motor.GroundingStatus.GroundNormal,
                            1 - Mathf.Exp(-BonusOrientationSharpness * deltaTime));
                        currentRotation = Quaternion.FromToRotation(currentUp, groundNormalDirection) * currentRotation;
                        Motor.SetTransientPosition(initialCharacterBottom + (currentRotation * Vector3.down * Motor.Capsule.radius));
                        if (MeshRoot != null) MeshRoot.rotation = currentRotation;
                    }
                    else
                    {
                        Vector3 airGravityDirection = Vector3.Slerp(
                            currentUp,
                            -Gravity.normalized,
                            1 - Mathf.Exp(-BonusOrientationSharpness * deltaTime));
                        currentRotation = Quaternion.FromToRotation(currentUp, airGravityDirection) * currentRotation;
                        if (MeshRoot != null) MeshRoot.rotation = currentRotation;
                    }
                    break;
            }
        }

        public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            switch (CurrentCharacterState)
            {
                case CharacterState.OnTrampoline:
                    {
                        float moveDistance = TrampolineSettings.RunSpeed * deltaTime;
                        Motor.SetPosition(Motor.TransientPosition + _trampolineDirection * moveDistance);
                        _distanceTraveled += moveDistance;

                        if (_distanceTraveled >= TrampolineSettings.TrampolineLength)
                        {
                            float jumpForce = TrampolineSettings.BaseJumpForce;
                            Motor.ForceUnground();
                            currentVelocity = _trampolineDirection * TrampolineSettings.RunSpeed + Vector3.up * jumpForce;
                            TransitionToState(CharacterState.JumpingFromTrampoline);
                            Invoke("EnableControl", 0.5f);
                        }
                    }
                    break;

                case CharacterState.JumpingFromTrampoline:
                    currentVelocity += Gravity * deltaTime;
                    currentVelocity *= (1f / (1f + (Drag * deltaTime)));
                    if (currentVelocity.y < -5f) _isFastFalling = true;
                    break;

                case CharacterState.Climbing:
                    currentVelocity = Vector3.zero;
                    break;

                default:
                    if (Motor.GroundingStatus.IsStableOnGround)
                    {
                        float currentVelocityMagnitude = currentVelocity.magnitude;
                        Vector3 effectiveGroundNormal = Motor.GroundingStatus.GroundNormal;

                        currentVelocity = Motor.GetDirectionTangentToSurface(currentVelocity, effectiveGroundNormal) * currentVelocityMagnitude;

                        Vector3 inputRight = Vector3.Cross(_moveInputVector, Motor.CharacterUp);
                        Vector3 reorientedInput = Vector3.Cross(effectiveGroundNormal, inputRight).normalized * _moveInputVector.magnitude;
                        Vector3 targetMovementVelocity = reorientedInput * MaxStableMoveSpeed;

                        currentVelocity = Vector3.Lerp(
                            currentVelocity,
                            targetMovementVelocity,
                            1f - Mathf.Exp(-StableMovementSharpness * deltaTime));
                    }
                    else
                    {
                        if (!_isJumpingOffLadder)
                        {
                            if (_moveInputVector.sqrMagnitude > 0f)
                            {
                                Vector3 addedVelocity = _moveInputVector * AirAccelerationSpeed * deltaTime;

                                Vector3 currentVelocityOnInputsPlane = Vector3.ProjectOnPlane(currentVelocity, Motor.CharacterUp);
                                if (currentVelocityOnInputsPlane.magnitude < MaxAirMoveSpeed)
                                {
                                    Vector3 newTotal = Vector3.ClampMagnitude(currentVelocityOnInputsPlane + addedVelocity, MaxAirMoveSpeed);
                                    addedVelocity = newTotal - currentVelocityOnInputsPlane;
                                }
                                else if (Vector3.Dot(currentVelocityOnInputsPlane, addedVelocity) > 0f)
                                {
                                    addedVelocity = Vector3.ProjectOnPlane(addedVelocity, currentVelocityOnInputsPlane.normalized);
                                }

                                currentVelocity += addedVelocity;
                            }
                        }
                        else
                        {
                            currentVelocity = new Vector3(0f, currentVelocity.y, 0f);
                        }

                        Vector3 effectiveGravity = Gravity;
                        if (_isJumpingOffLadder)
                        {
                            effectiveGravity = Gravity * fastFallGravityMultiplier;
                            _isFastFalling = true;
                        }

                        currentVelocity += effectiveGravity * deltaTime;
                        currentVelocity *= (1f / (1f + (Drag * deltaTime)));

                        if (currentVelocity.y < -5f) _isFastFalling = true;
                        else _isFastFalling = false;
                    }

                    if (_internalVelocityAdd.sqrMagnitude > 0f)
                    {
                        currentVelocity += _internalVelocityAdd;
                        _internalVelocityAdd = Vector3.zero;
                    }

                    if (_jumpRequested)
                    {
                        if (!_jumpConsumed && ((AllowJumpingWhenSliding ? Motor.GroundingStatus.FoundAnyGround : Motor.GroundingStatus.IsStableOnGround) ||
                            _timeSinceLastAbleToJump <= JumpPostGroundingGraceTime))
                        {
                            Vector3 jumpDirection = Motor.CharacterUp;
                            if (Motor.GroundingStatus.FoundAnyGround && !Motor.GroundingStatus.IsStableOnGround)
                                jumpDirection = Motor.GroundingStatus.GroundNormal;

                            Motor.ForceUnground();
                            currentVelocity += (jumpDirection * JumpUpSpeed) - Vector3.Project(currentVelocity, Motor.CharacterUp);
                            currentVelocity += (_moveInputVector * JumpScalableForwardSpeed);

                            _jumpRequested = false;
                            _jumpConsumed = true;
                        }
                    }
                    break;
            }
        }

        public void AfterCharacterUpdate(float deltaTime)
        {
            _jumpedThisFrame = false;
            _timeSinceJumpRequested += deltaTime;

            if (AllowJumpingWhenSliding ? Motor.GroundingStatus.FoundAnyGround : Motor.GroundingStatus.IsStableOnGround)
            {
                if (!_jumpedThisFrame) _jumpConsumed = false;
                _timeSinceLastAbleToJump = 0f;
            }
            else
            {
                _timeSinceLastAbleToJump += deltaTime;
            }
        }

        public void PostGroundingUpdate(float deltaTime)
        {
            if (Motor.GroundingStatus.IsStableOnGround && !Motor.LastGroundingStatus.IsStableOnGround)
                OnLanded();
            else if (!Motor.GroundingStatus.IsStableOnGround && Motor.LastGroundingStatus.IsStableOnGround)
                OnLeaveStableGround();
        }

        public bool IsColliderValidForCollisions(Collider coll) => !IgnoredColliders.Contains(coll);

        public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) { }
        public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) { }
        public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
            Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport) { }
        public void OnDiscreteCollisionDetected(Collider hitCollider) { }

        public void SetInputs(ref PlayerCharacterInputs inputs)
        {
            if (!_controlEnabled || _isJumpingOffLadder)
            {
                inputs.MoveAxisForward = 0f;
                inputs.MoveAxisRight = 0f;
                return;
            }

            if (CurrentCharacterState == CharacterState.Climbing)
            {
                inputs.MoveAxisForward = 0f;
                inputs.MoveAxisRight = 0f;
                return;
            }

            float moveHorizontal = inputs.MoveAxisRight;
            float moveVertical = inputs.MoveAxisForward;

            if (MobileJoystick != null)
            {
                float joyH = MobileJoystick.Horizontal;
                float joyV = MobileJoystick.Vertical;
                if (joyH != 0f || joyV != 0f)
                {
                    moveHorizontal = joyH;
                    moveVertical = joyV;
                }
            }

            Vector3 moveInputVector = Vector3.ClampMagnitude(new Vector3(moveHorizontal, 0f, moveVertical), 1f);

            Vector3 cameraPlanarDirection = Vector3.ProjectOnPlane(inputs.CameraRotation * Vector3.forward, Motor.CharacterUp).normalized;
            if (cameraPlanarDirection.sqrMagnitude == 0f)
                cameraPlanarDirection = Vector3.ProjectOnPlane(inputs.CameraRotation * Vector3.up, Motor.CharacterUp).normalized;

            Quaternion cameraPlanarRotation = Quaternion.LookRotation(cameraPlanarDirection, Motor.CharacterUp);
            _moveInputVector = cameraPlanarRotation * moveInputVector;

            switch (OrientationMethod)
            {
                case OrientationMethod.TowardsCamera: _lookInputVector = cameraPlanarDirection; break;
                case OrientationMethod.TowardsMovement: _lookInputVector = _moveInputVector.normalized; break;
                case OrientationMethod.None: _lookInputVector = Vector3.zero; break;
            }

            _isMoving = moveInputVector.magnitude > 0.1f;

            if (CharacterAnimator != null)
            {
                CharacterAnimator.SetBool("IsRunning", _isMoving);
                CharacterAnimator.SetBool("IsGrounded", Motor.GroundingStatus.IsStableOnGround);
            }

            if (inputs.JumpDown) HandleJumpInput();
        }

        public void SetInputs(ref AICharacterInputs inputs)
        {
            _moveInputVector = inputs.MoveVector;
            _lookInputVector = inputs.LookVector;
        }

        public void AddVelocity(Vector3 velocity) => _internalVelocityAdd += velocity;
        public void SetCoins(double coins) { CoinsCollected = coins; UpdateCoinCounter(); }
        public void SetCursorLocked(bool locked) { cursorLocked = locked; UpdateCursorState(); }
        public KinematicCharacterMotor GetMotor() => Motor;

        public void SpendCoinsForPet(double amount)
        {
            if (SpendCoins(amount))
            {
                if (CoinSettings.CoinEffect != null)
                {
                    ParticleSystem effect = Instantiate(CoinSettings.CoinEffect, transform.position, Quaternion.identity);
                    Destroy(effect.gameObject, effect.main.duration);
                }
                if (CoinSettings.CoinSound != null)
                    AudioSource.PlayClipAtPoint(CoinSettings.CoinSound, transform.position);
            }
            else
                Debug.LogWarning("Недостаточно монет для покупки!");
        }
    }
}