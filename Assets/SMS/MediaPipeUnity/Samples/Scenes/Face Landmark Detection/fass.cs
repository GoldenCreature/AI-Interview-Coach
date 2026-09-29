using System;
using System.Collections.Generic;
using System.Text;
using Mediapipe.Tasks.Vision.FaceLandmarker;
using Mediapipe.Unity.Sample.FaceLandmarkDetection;
using UnityEngine;
using InterviewDb; // InterviewDbManager 접근용 네임스페이스

/// <summary>
/// [최적화 요약]
/// 1) MediaPipe 콜백(HandleResult)은 "누적"만 수행 (문자열 생성/평가 없음, 힙 할당 0)
/// 2) 평가(EvaluateAll)와 문자열 생성은 메인 스레드 Update에서 일정 주기(기본 0.5초)로만 실행
///    - 새 프레임이 없으면 건너뜀, 인스펙터 실시간 표시가 필요 없으면 완전히 끔
/// 3) 랜드마크 변환용 List<Vector3>를 매 프레임 new 하지 않고 버퍼 재사용
/// 4) StringBuilder를 하나만 만들어 Clear() 후 재사용
/// 5) 얼굴 너비/높이 등 중복 계산을 프레임당 1회로 축소
/// 6) latest* 필드 쓰기를 메인 스레드로 이동 (콜백 스레드와의 경합 제거)
///
/// [정확도 개선 요약]
/// 7) 랜드마크 x, z에 영상 가로세로비를 곱해 등방 좌표로 변환 (해상도/비율에 따른 비율 왜곡 제거)
/// 8) 미소 지표를 "입 벌림"이 아닌 입꼬리 너비 + 입꼬리 상승량으로 교체
/// 9) 깜빡임 프레임을 놀람/시선 누적에서 제외
/// 10) Yaw/Pitch를 3D 얼굴 축(귀-귀, 이마-턱)으로 각도(도)로 추정하고, 캘리브레이션 때의 자세를 0°로 기준화
/// </summary>
public class fass : MonoBehaviour
{
    [Header("CSV Logger 연결")]
    [Tooltip("유니티 인스펙터 창에서 CustomPathCSVLogger 오브젝트를 여기에 드래그 앤 드롭 하세요.")]
    public CustomPathCSVLogger csvLogger;

    [Header("MediaPipe Runner 연결")]
    [Tooltip("씬에 있는 FaceLandmarkerRunner 오브젝트를 여기에 드래그하세요.")]
    public FaceLandmarkerRunner runner;

    // InterviewDbManager 참조: 인스펙터 연결 없이 자동으로 찾아 캐싱합니다.
    private InterviewDbManager dbManager;

    // ─────────────────────────────────────────────────────────
    // 성능 설정
    // ─────────────────────────────────────────────────────────
    [Header("성능 설정")]
    [Tooltip("체크하면 일정 주기마다 latest* 필드(인스펙터 표시/UI용)를 갱신합니다. 빌드에서 실시간 표시가 필요 없으면 끄세요. (면접 종료 시 최종 값은 항상 갱신됩니다)")]
    public bool enableLiveInspectorUpdate = true;

    [Tooltip("latest* 필드 갱신 주기(초). 값이 클수록 CPU/GC 부담이 줄어듭니다.")]
    [Min(0.05f)]
    public float liveUpdateInterval = 0.5f;

    private float nextLiveUpdateTime;
    private volatile bool hasNewFrameSinceLastEval;

    // ─────────────────────────────────────────────────────────
    // 자동 캘리브레이션 설정
    // ─────────────────────────────────────────────────────────
    [Header("자동 캘리브레이션 (시작 직후 무표정 + 정면 응시 측정)")]
    [Tooltip("체크하면 스크립트가 활성화될 때마다 사용자의 무표정/정면 응시 기준값을 자동 측정해 아래 캘리브레이션 값에 덮어씁니다. 끄면 인스펙터에 입력한 값을 그대로 사용합니다.")]
    public bool enableAutoCalibration = true;

    [Tooltip("캘리브레이션 측정 시간(초). 얼굴이 처음 정상 인식된 시점부터 계산합니다.")]
    [Min(1f)]
    public float calibrationDuration = 3f;

    [Tooltip("측정 시간이 지나도 이 프레임 수 이상 모이지 않으면 충분히 모일 때까지 계속 측정합니다.")]
    [Min(10)]
    public int minCalibrationFrames = 30;

    [Tooltip("캘리브레이션 중 좌우 회전(Yaw)이 이 각도(°)를 넘는 프레임은 제외합니다. 카메라를 정면으로 보고 있는 프레임만 기준값으로 쓰기 위함입니다.")]
    public float calibrationMaxYawDegrees = 15f;

    [Header("영상 크기 (가로세로비 보정)")]
    [Tooltip("MediaPipe에 입력되는 영상의 가로 픽셀 수. 랜드마크 좌표는 가로/세로가 각각 0~1로 정규화되어 있어 비율 보정에 필요합니다.")]
    public int imageWidth = 1280;

    [Tooltip("MediaPipe에 입력되는 영상의 세로 픽셀 수. (모바일 등에서 영상이 90° 회전되어 들어오면 가로/세로를 바꿔 입력하세요)")]
    public int imageHeight = 720;

    [Header("표정 민감도 튜닝")]
    [Tooltip("미소 지표가 무표정 기준보다 커진 만큼에 곱해지는 값. 자연스러운 미소가 2~3점 정도 나오도록 조정하세요.")]
    public float smileGain = 35f;

    [Tooltip("눈 뜬 정도가 기준보다 커진 만큼에 곱해지는 값.")]
    public float surpriseGain = 60f;

    [Tooltip("미간(눈썹 사이 거리)이 기준보다 좁아진 만큼에 곱해지는 값.")]
    public float angryGain = 42f;

    [Tooltip("눈 뜬 정도가 (무표정 기준값 x 이 비율)보다 작으면 깜빡임으로 보고 놀람/시선 계산에서 제외합니다.")]
    [Range(0.2f, 0.9f)]
    public float blinkThresholdRatio = 0.6f;

    // ─────────────────────────────────────────────────────────
    // 랜드마크 개수 상수
    // ─────────────────────────────────────────────────────────
    private const int FACE_LANDMARK_COUNT = 468;   // 기본 얼굴 메쉬
    private const int IRIS_LANDMARK_COUNT = 478;   // 얼굴 메쉬(468) + 홍채(10)

    // 홍채/눈 랜드마크 인덱스
    private const int LEFT_IRIS_CENTER = 468;
    private const int LEFT_EYE_INNER = 133;
    private const int LEFT_EYE_OUTER = 33;
    private const int LEFT_EYE_UPPER = 159;
    private const int LEFT_EYE_LOWER = 145;

    private const int RIGHT_IRIS_CENTER = 473;
    private const int RIGHT_EYE_INNER = 362;
    private const int RIGHT_EYE_OUTER = 263;
    private const int RIGHT_EYE_UPPER = 386;
    private const int RIGHT_EYE_LOWER = 374;

    private bool warnedNoIris = false;

    // ─────────────────────────────────────────────────────────
    // 재사용 버퍼 (프레임마다 new 하지 않기 위함)
    // ─────────────────────────────────────────────────────────
    // HandleResult(MediaPipe 콜백 스레드)에서만 사용
    private readonly List<Vector3> landmarkBuffer = new List<Vector3>(IRIS_LANDMARK_COUNT);

    // 평가 문자열 생성용 (메인 스레드에서만 사용)
    private readonly StringBuilder sharedSb = new StringBuilder(256);

    // ─────────────────────────────────────────────────────────
    // 캘리브레이션 (무표정/정면 기준값) - 수동 입력
    // ─────────────────────────────────────────────────────────
    [Header("캘리브레이션 (무표정 기준값) - 자동 측정 시 덮어써짐 / 꺼져 있으면 수동 입력값 사용")]
    [Tooltip("무표정 상태의 미소 지표((입꼬리 너비 + 입꼬리 상승량) / 얼굴 너비). 자동 캘리브레이션이 꺼져 있을 때 사용되는 값입니다.")]
    public float neutralSmileRatio = 0.36f;

    [Tooltip("무표정 상태에서 측정한 눈 뜬 정도/얼굴너비 비율. 인스펙터에서 직접 값을 입력/조정하세요.")]
    public float neutralSurpriseRatio = 0.034f;

    [Tooltip("무표정 상태에서 측정한 눈썹 사이 거리/얼굴너비 비율. 인스펙터에서 직접 값을 입력/조정하세요.")]
    public float neutralAngryRatio = 0.212f;

    [Header("캘리브레이션 (정면 응시 기준값) - 자동 측정 시 덮어써짐 / 꺼져 있으면 수동 입력값 사용")]
    [Tooltip("카메라를 정면으로 응시할 때의 눈동자(홍채) 좌우 위치 비율(0=눈 안쪽, 1=눈 바깥쪽). 캘리브레이션 후 값을 조정하세요.")]
    public float neutralGazeHorizontalRatio = 0.5f;

    [Tooltip("카메라를 정면으로 응시할 때의 눈동자(홍채) 상하 위치 비율(0=윗눈꺼풀, 1=아랫눈꺼풀). 캘리브레이션 후 값을 조정하세요.")]
    public float neutralGazeVerticalRatio = 0.5f;

    [Tooltip("시선 이탈 정도를 점수로 환산할 때 곱해지는 민감도. 값이 클수록 작은 눈동자 이동에도 점수가 크게 떨어집니다.")]
    public float gazeSensitivity = 18f;

    [Header("얼굴 각도 제한 (Head Pose Gate) - 수동 입력")]
    [Tooltip("체크하면 얼굴이 특정 각도 이상 돌아갔을 때 표정/시선 분석(점수 계산)을 건너뜁니다. 각도 점수 자체는 게이트와 무관하게 계속 측정됩니다.")]
    public bool enableAngleGate = true;

    [Tooltip("좌우 회전(Yaw) 허용 한계(°). 캘리브레이션 때의 정면 자세 기준이며, 이 값에 가까워질수록 얼굴 각도 점수가 0점에 가까워집니다.")]
    public float maxYawDegrees = 30f;

    [Tooltip("상하 회전(Pitch) 허용 한계(°). 캘리브레이션 때의 정면 자세 기준이며, 이 값에 가까워질수록 얼굴 각도 점수가 0점에 가까워집니다.")]
    public float maxPitchDegrees = 25f;

    [Tooltip("정면 자세 기준 Yaw(°). 자동 캘리브레이션이 채워 넣습니다. (꺼져 있으면 0 그대로)")]
    public float neutralYawDegrees = 0f;

    [Tooltip("정면 자세 기준 Pitch(°). 자동 캘리브레이션이 채워 넣습니다. 이마-턱 축의 구조적 기울기와 카메라 높이 차이를 상쇄합니다.")]
    public float neutralPitchDegrees = 0f;

    [Tooltip("각도 초과로 표정/시선 분석이 멈춘 상태인지")]
    public bool isFaceTooAngled = false;

    // ─────────────────────────────────────────────────────────
    // 누적 통계 (면접 시작 ~ 종료 전체 구간)
    // ─────────────────────────────────────────────────────────
    private class Accumulator
    {
        public float total;
        public int count;

        public void Add(float v)
        {
            total += v;
            count++;
        }

        public float Average => count > 0 ? total / count : 0f;

        public void Reset()
        {
            total = 0f;
            count = 0;
        }
    }

    private readonly Accumulator smileAcc = new Accumulator();
    private readonly Accumulator surpriseAcc = new Accumulator();
    private readonly Accumulator angryAcc = new Accumulator();
    private readonly Accumulator gazeAcc = new Accumulator();
    private readonly Accumulator angleAcc = new Accumulator();
    private readonly Accumulator yawAcc = new Accumulator();
    private readonly Accumulator pitchAcc = new Accumulator();

    // MediaPipe 콜백 스레드 ↔ 메인 스레드 동시 접근 방지용
    private readonly object statsLock = new object();

    public float SmileTotal { get { lock (statsLock) return smileAcc.total; } }
    public float SmileAverage { get { lock (statsLock) return smileAcc.Average; } }
    public int SmileCount { get { lock (statsLock) return smileAcc.count; } }

    public float SurpriseTotal { get { lock (statsLock) return surpriseAcc.total; } }
    public float SurpriseAverage { get { lock (statsLock) return surpriseAcc.Average; } }
    public int SurpriseCount { get { lock (statsLock) return surpriseAcc.count; } }

    public float AngryTotal { get { lock (statsLock) return angryAcc.total; } }
    public float AngryAverage { get { lock (statsLock) return angryAcc.Average; } }
    public int AngryCount { get { lock (statsLock) return angryAcc.count; } }

    /// <summary>
    /// 모든 누적 통계를 초기화합니다. 새 면접이 시작될 때 호출됩니다.
    /// </summary>
    public void ResetStatistics()
    {
        lock (statsLock)
        {
            smileAcc.Reset();
            surpriseAcc.Reset();
            angryAcc.Reset();
            gazeAcc.Reset();
            angleAcc.Reset();
            yawAcc.Reset();
            pitchAcc.Reset();
        }
        warnedNoIris = false;
        hasNewFrameSinceLastEval = false;
    }

    // ─────────────────────────────────────────────────────────
    // 표정 평가 시스템 (하위 호환 유지용 - 전체 "태도" 결과를 담음)
    // ─────────────────────────────────────────────────────────
    [Header("표정 평가 시스템")]
    public ExpressionGrade latestGrade;
    public string latestEvaluationSummary = "";
    public string latestEvaluationDetail = "";
    public string latestImprovementNotes = "";
    public int latestEvaluationScore = 0;

    public enum ExpressionGrade
    {
        Excellent,  // 매우 안정적
        Good,       // 안정적
        Average,    // 보통
        NeedsWork,  // 긴장 감지
        Poor        // 불안정
    }

    // ─────────────────────────────────────────────────────────
    // 태도 영역 3분할 결과 (표정 / 시선 / 얼굴각도)
    // ─────────────────────────────────────────────────────────
    [Serializable]
    public struct EvaluationArea
    {
        public string areaName;    // 평가 영역명
        public int score;          // 0~5, 정수
        public string result;      // 평가 결과 (문장)
        public string improvement; // 개선 사항 (문장, 여러 줄 가능)
    }

    [Header("태도 하위 영역 결과 (표정 / 시선 / 얼굴각도)")]
    public EvaluationArea latestFaceExpressionArea;
    public EvaluationArea latestGazeArea;
    public EvaluationArea latestAngleArea;

    [Tooltip("표정, 시선, 얼굴각도 3개 영역 점수를 합산 후 0~5점으로 정규화한 최종 태도 점수")]
    public int latestAttitudeScore = 0;

    /// <summary>
    /// UI 테이블(평가 영역 / 평가 결과 / 개선 사항) 바인딩용 - 표정/시선/얼굴각도 3개 행을 반환합니다.
    /// (호출 시 List 1개가 생성되므로 매 프레임 호출은 피하고, 갱신이 필요할 때만 호출하세요.)
    /// </summary>
    public List<EvaluationArea> GetAttitudeAreas()
    {
        return new List<EvaluationArea> { latestFaceExpressionArea, latestGazeArea, latestAngleArea };
    }

    void OnEnable()
    {
        // 새 면접 시작 시 이전 데이터가 섞이지 않도록 초기화
        ResetStatistics();
        nextLiveUpdateTime = 0f;
        RefreshImageAspect();

        // 면접 시작 직후 3초간 기준값 자동 측정
        if (enableAutoCalibration)
            StartCalibration();
        else
            calibrating = false;

        if (runner != null)
            runner.OnResultOutput += HandleResult;
        else
            Debug.LogWarning("[fass] runner가 인스펙터에 연결되지 않았습니다.");

        HJS.InterviewManager.OnInterviewEndRequested += HandleInterviewEnded;
    }

    void Start()
    {
        dbManager = InterviewDbManager.Instance;
        if (dbManager == null)
            Debug.LogWarning("[fass] InterviewDbManager를 씬에서 찾지 못했습니다. 저장 시점에 다시 시도합니다.");
    }

    void OnDisable()
    {
        calibrating = false;
        calibrationTimer.Stop();

        if (runner != null)
            runner.OnResultOutput -= HandleResult;

        HJS.InterviewManager.OnInterviewEndRequested -= HandleInterviewEnded;
    }

    // ─────────────────────────────────────────────────────────
    // 메인 스레드: 일정 주기로만 평가 + 문자열 생성
    // (기존에는 MediaPipe 콜백에서 매 프레임 실행되던 부분)
    // ─────────────────────────────────────────────────────────
    void Update()
    {
        // 캘리브레이션 완료 알림은 메인 스레드에서 처리 (로그/이벤트를 안전하게 호출)
        if (calibrationCompletedFlag)
        {
            calibrationCompletedFlag = false;
            Debug.Log($"[fass] 캘리브레이션 완료 → 무표정(미소 {neutralSmileRatio:F3}, 눈 {neutralSurpriseRatio:F3}, 눈썹 {neutralAngryRatio:F3}) / 정면 응시(가로 {neutralGazeHorizontalRatio:F3}, 세로 {neutralGazeVerticalRatio:F3}) / 정면 자세(Yaw {neutralYawDegrees:F1}°, Pitch {neutralPitchDegrees:F1}°)");
            OnCalibrationCompleted?.Invoke();
        }

        if (!enableLiveInspectorUpdate) return;
        if (Time.unscaledTime < nextLiveUpdateTime) return;
        if (!hasNewFrameSinceLastEval) return; // 새 데이터가 없으면 재평가 불필요

        nextLiveUpdateTime = Time.unscaledTime + liveUpdateInterval;
        hasNewFrameSinceLastEval = false;

        Snapshot snap = TakeSnapshot();
        if (!snap.hasFace && !snap.hasAngle) return;

        ApplyEvaluation(EvaluateAll(snap));
    }

    private void ApplyEvaluation(EvaluationResult eval)
    {
        latestFaceExpressionArea = eval.faceArea;
        latestGazeArea = eval.gazeArea;
        latestAngleArea = eval.angleArea;

        latestAttitudeScore = eval.attitudeScore;
        latestGrade = eval.grade;
        latestEvaluationScore = eval.attitudeScore;

        latestEvaluationSummary = eval.summary;
        latestEvaluationDetail = eval.detail;
        latestImprovementNotes = eval.notes;
    }

    // ─────────────────────────────────────────────────────────
    // MediaPipe 콜백 (백그라운드 스레드) - 할당 없는 hot path
    // ─────────────────────────────────────────────────────────
    private void HandleResult(FaceLandmarkerResult result)
    {
        if (result.faceLandmarks == null || result.faceLandmarks.Count == 0)
            return;

        var src = result.faceLandmarks[0].landmarks;
        int n = src.Count;

        // MediaPipe의 x,y,z는 이미지 너비/높이로 각각 정규화된 값(z는 x와 같은 스케일)입니다.
        // 그대로 거리를 재면 16:9 같은 비율에서 가로 방향이 축소되어 계산이 왜곡되므로,
        // x, z에 가로세로비를 곱해 "높이 = 1 단위"의 등방(isotropic) 좌표로 바꿔서 저장합니다.
        float aspect = imageAspect;

        // 매 프레임 new List 대신 버퍼 재사용 (Clear는 capacity 유지)
        landmarkBuffer.Clear();
        for (int i = 0; i < n; i++)
        {
            var lm = src[i];
            landmarkBuffer.Add(new Vector3(lm.x * aspect, lm.y, lm.z * aspect));
        }

        OnFaceLandmarksDetected(landmarkBuffer);
    }

    /// <summary>
    /// 프레임 누적 전용. 평가/문자열 생성은 하지 않습니다.
    /// 주의 1: 전달받은 리스트는 호출이 끝난 뒤 보관하지 마세요 (버퍼가 재사용됩니다).
    /// 주의 2: 좌표는 가로세로비 보정(x, z에 aspect 곱함)이 적용된 값이어야 합니다.
    ///         외부에서 직접 호출한다면 같은 방식으로 변환한 리스트를 넘겨주세요.
    /// </summary>
    public void OnFaceLandmarksDetected(List<Vector3> landmarks)
    {
        if (landmarks == null || landmarks.Count < FACE_LANDMARK_COUNT)
            return;

        // 프레임 전체에서 공통으로 쓰는 값은 1회만 계산
        float faceWidth = SafeDistance(landmarks[234], landmarks[454]);

        // ── 1) 머리 자세(도 단위, 부호 있음) 추정 - 게이트 여부와 무관하게 항상 측정
        ComputeHeadPose(landmarks, out float rawYaw, out float rawPitch);

        // ── 캘리브레이션 구간: 기준값 샘플만 수집하고, 점수 누적은 하지 않습니다.
        if (calibrating)
        {
            CollectCalibrationSample(landmarks, faceWidth, rawYaw, rawPitch);
            return;
        }

        // 캘리브레이션 때의 "정면 자세"와 얼마나 다른지(절댓값)
        float yawDelta = Mathf.Abs(rawYaw - neutralYawDegrees);
        float pitchDelta = Mathf.Abs(rawPitch - neutralPitchDegrees);
        float frameAngleScore = CalculateAngleScore(yawDelta, pitchDelta);

        bool tooAngled = enableAngleGate && IsFaceTooAngled(yawDelta, pitchDelta);
        isFaceTooAngled = tooAngled;

        // ── 2) 계산은 lock 밖에서 끝내고, lock 안에서는 더하기만 수행
        bool hasExpression = !tooAngled;
        bool irisAvailable = landmarks.Count >= IRIS_LANDMARK_COUNT;
        bool eyesOpen = true;
        float smile = 0f, surprise = 0f, angry = 0f, gaze = 0f;

        if (hasExpression)
        {
            smile = CalculateSmile(landmarks, faceWidth);
            angry = CalculateAngry(landmarks, faceWidth);

            // 깜빡이는 프레임은 눈 관련 지표(놀람/시선)에서 제외:
            // 눈이 감기면 홍채 위치 비율이 불안정해져 "시선 이탈"로 잘못 잡히기 때문
            float eyeOpenRatio = GetRawSurpriseRatio(landmarks, faceWidth);
            eyesOpen = eyeOpenRatio >= neutralSurpriseRatio * blinkThresholdRatio;

            if (eyesOpen)
            {
                surprise = CalculateSurprise(eyeOpenRatio);
                if (irisAvailable) gaze = CalculateGazeScore(landmarks);
            }
        }

        bool needIrisWarning = false;

        lock (statsLock)
        {
            yawAcc.Add(yawDelta);
            pitchAcc.Add(pitchDelta);
            angleAcc.Add(frameAngleScore);

            if (hasExpression)
            {
                smileAcc.Add(smile);
                angryAcc.Add(angry);

                if (eyesOpen)
                {
                    surpriseAcc.Add(surprise);
                    if (irisAvailable) gazeAcc.Add(gaze);
                }

                if (!irisAvailable && !warnedNoIris)
                {
                    warnedNoIris = true;
                    needIrisWarning = true;
                }
            }
        }

        if (needIrisWarning)
            Debug.LogWarning("[fass] 홍채(iris) 랜드마크가 감지되지 않았습니다. 478개 랜드마크를 출력하는 모델이 로드됐는지 확인하세요.");

        hasNewFrameSinceLastEval = true;
    }

    // ─────────────────────────────────────────────────────────
    // 자동 캘리브레이션 (무표정 + 정면 응시 기준값)
    // - 얼굴이 정상 인식된 첫 프레임부터 calibrationDuration초 동안 원시 비율을 수집
    // - 평균이 아닌 "중앙값"을 사용 → 깜빡임/말하기/순간 움직임 같은 이상치에 강함
    // - 샘플 배열은 미리 할당해 두고 재사용 (수집 중 힙 할당 없음)
    // ─────────────────────────────────────────────────────────
    private const int CAL_MAX_SAMPLES = 600; // 60fps x 10초까지 수용

    private readonly float[] calSmile = new float[CAL_MAX_SAMPLES];
    private readonly float[] calSurprise = new float[CAL_MAX_SAMPLES];
    private readonly float[] calAngry = new float[CAL_MAX_SAMPLES];
    private readonly float[] calGazeH = new float[CAL_MAX_SAMPLES];
    private readonly float[] calGazeV = new float[CAL_MAX_SAMPLES];
    private readonly float[] calYaw = new float[CAL_MAX_SAMPLES];
    private readonly float[] calPitch = new float[CAL_MAX_SAMPLES];
    private int calCount;
    private int calGazeCount;

    // MediaPipe 콜백 스레드에서 시간을 재야 하므로 Time.time 대신 Stopwatch 사용
    private readonly System.Diagnostics.Stopwatch calibrationTimer = new System.Diagnostics.Stopwatch();

    private volatile bool calibrating;
    private volatile bool calibrated;
    private volatile bool calibrationCompletedFlag;

    /// <summary>캘리브레이션 시작 시 호출 (메인 스레드). UI가 늦게 구독할 수 있으므로 IsCalibrating 폴링도 가능합니다.</summary>
    public event Action OnCalibrationStarted;

    /// <summary>캘리브레이션 완료 시 호출 (메인 스레드, Update에서 발생).</summary>
    public event Action OnCalibrationCompleted;

    public bool IsCalibrating => calibrating;
    public bool IsCalibrated => calibrated;

    /// <summary>0~1. UI 프로그레스바용. 얼굴이 아직 인식되지 않았다면 0입니다.</summary>
    public float CalibrationProgress
    {
        get
        {
            if (calibrated) return 1f;
            if (!calibrationTimer.IsRunning) return 0f;
            return Mathf.Clamp01((float)(calibrationTimer.Elapsed.TotalSeconds / calibrationDuration));
        }
    }

    /// <summary>UI 카운트다운용 남은 시간(초).</summary>
    public float CalibrationRemainingSeconds
    {
        get
        {
            if (!calibrating) return 0f;
            if (!calibrationTimer.IsRunning) return calibrationDuration;
            return Mathf.Max(0f, calibrationDuration - (float)calibrationTimer.Elapsed.TotalSeconds);
        }
    }

    /// <summary>캘리브레이션을 (다시) 시작합니다. "재측정" 버튼 등에서 호출할 수 있습니다. (메인 스레드에서 호출)</summary>
    public void StartCalibration()
    {
        lock (statsLock)
        {
            calCount = 0;
            calGazeCount = 0;
        }

        calibrationTimer.Reset();
        calibrated = false;
        calibrationCompletedFlag = false;
        calibrating = true;

        Debug.Log($"[fass] 캘리브레이션 시작: 카메라를 정면으로 보고 무표정을 유지해주세요 ({calibrationDuration:F0}초)");
        OnCalibrationStarted?.Invoke();
    }

    // MediaPipe 콜백 스레드에서 호출됩니다.
    private void CollectCalibrationSample(List<Vector3> landmarks, float faceWidth, float rawYaw, float rawPitch)
    {
        // 카메라를 정면으로 보고 있지 않은 프레임은 제외 (이 경우 타이머도 시작하지 않음)
        // Yaw는 정면일 때 0° 근처라 엄격하게, Pitch는 카메라 높이/얼굴 구조에 따라 기준이 달라져 느슨하게 확인
        if (Mathf.Abs(rawYaw) > calibrationMaxYawDegrees || Mathf.Abs(rawPitch) > 45f) return;

        if (!calibrationTimer.IsRunning) calibrationTimer.Start();

        float smile = GetRawSmileRatio(landmarks, faceWidth);
        float surprise = GetRawSurpriseRatio(landmarks, faceWidth);
        float angry = GetRawAngryRatio(landmarks, faceWidth);

        bool hasIris = landmarks.Count >= IRIS_LANDMARK_COUNT;
        float gazeH = 0f, gazeV = 0f;
        if (hasIris)
        {
            gazeH = GetRawGazeHorizontalRatio(landmarks);
            gazeV = GetRawGazeVerticalRatio(landmarks);
        }

        lock (statsLock)
        {
            if (!calibrating) return; // 수집 도중 비활성화된 경우

            if (calCount < CAL_MAX_SAMPLES)
            {
                calSmile[calCount] = smile;
                calSurprise[calCount] = surprise;
                calAngry[calCount] = angry;
                calYaw[calCount] = rawYaw;
                calPitch[calCount] = rawPitch;
                calCount++;

                if (hasIris)
                {
                    calGazeH[calGazeCount] = gazeH;
                    calGazeV[calGazeCount] = gazeV;
                    calGazeCount++;
                }
            }

            int requiredFrames = Math.Min(minCalibrationFrames, CAL_MAX_SAMPLES);
            bool timeReached = calibrationTimer.Elapsed.TotalSeconds >= calibrationDuration;
            bool bufferFull = calCount >= CAL_MAX_SAMPLES;

            if ((timeReached && calCount >= requiredFrames) || bufferFull)
                FinishCalibrationLocked();
        }
    }

    // statsLock 안에서 호출됩니다.
    private void FinishCalibrationLocked()
    {
        float smile = Median(calSmile, calCount);
        float surprise = Median(calSurprise, calCount);
        float angry = Median(calAngry, calCount);

        // 유효하지 않은 값(NaN 등)이면 인스펙터에 입력된 기존 값을 그대로 유지
        if (IsValidRatio(smile)) neutralSmileRatio = smile;
        if (IsValidRatio(surprise)) neutralSurpriseRatio = surprise;
        if (IsValidRatio(angry)) neutralAngryRatio = angry;

        // 정면 자세 기준(부호 있는 각도): 카메라 높이/얼굴 구조에 따른 고정 오프셋을 제거하는 용도
        float yaw = Median(calYaw, calCount);
        float pitch = Median(calPitch, calCount);
        if (IsFiniteValue(yaw)) neutralYawDegrees = yaw;
        if (IsFiniteValue(pitch)) neutralPitchDegrees = pitch;

        // 홍채 랜드마크가 충분히 수집된 경우에만 시선 기준값 갱신
        if (calGazeCount >= 10)
        {
            float gazeH = Median(calGazeH, calGazeCount);
            float gazeV = Median(calGazeV, calGazeCount);
            if (IsValidRatio(gazeH)) neutralGazeHorizontalRatio = gazeH;
            if (IsValidRatio(gazeV)) neutralGazeVerticalRatio = gazeV;
        }

        calibrationTimer.Stop();
        calibrating = false;
        calibrated = true;
        calibrationCompletedFlag = true; // 로그/이벤트는 메인 스레드(Update)에서 처리
        hasNewFrameSinceLastEval = false;
    }

    private static bool IsFiniteValue(float v)
    {
        return !float.IsNaN(v) && !float.IsInfinity(v);
    }

    private static bool IsValidRatio(float v)
    {
        return !float.IsNaN(v) && !float.IsInfinity(v) && v >= 0f;
    }

    // 배열을 제자리 정렬 후 중앙값 반환 (할당 없음)
    private static float Median(float[] arr, int count)
    {
        if (count <= 0) return float.NaN;
        Array.Sort(arr, 0, count);
        int mid = count / 2;
        return (count % 2 == 1) ? arr[mid] : (arr[mid - 1] + arr[mid]) * 0.5f;
    }

    // ─────────────────────────────────────────────────────────
    // 영상 비율 / 머리 자세
    // ─────────────────────────────────────────────────────────
    // MediaPipe 콜백 스레드에서 읽으므로 float 필드로 캐싱 (float 읽기/쓰기는 원자적)
    private float imageAspect = 16f / 9f;

    private void RefreshImageAspect()
    {
        imageAspect = (imageWidth > 0 && imageHeight > 0) ? (float)imageWidth / imageHeight : 1f;
    }

    /// <summary>
    /// 실제 입력 영상 크기를 알게 되면 호출하세요. (예: 카메라 소스 준비 완료 후 텍스처 크기)
    /// </summary>
    public void SetImageSize(int width, int height)
    {
        imageWidth = width;
        imageHeight = height;
        RefreshImageAspect();
    }

    void OnValidate()
    {
        RefreshImageAspect();
    }

    /// <summary>
    /// 3D 얼굴 축으로 머리 회전각(도)을 추정합니다. (부호 있음, 정면 = 0° 근처)
    /// - X축: 귀-귀(234→454), Y축: 이마-턱(10→152). 두 축의 외적이 얼굴이 바라보는 방향(법선)입니다.
    /// - Yaw = atan2(n.x, n.z), Pitch = atan2(n.y, sqrt(n.x² + n.z²)) 로 서로 간섭 없이 분리합니다.
    /// - 이전 방식(z 차이/너비 비율)과 달리 각도 단위라 임계값이 직관적이며, 축 정규화로 스케일 영향이 적습니다.
    /// 부호 규약은 영상 반전 여부에 따라 달라질 수 있으나, 캘리브레이션의 neutral 값과의 "차이"만 쓰므로 무관합니다.
    /// </summary>
    private static void ComputeHeadPose(List<Vector3> lm, out float yawDeg, out float pitchDeg)
    {
        Vector3 xAxis = lm[454] - lm[234];
        Vector3 yAxis = lm[152] - lm[10];
        Vector3 n = Vector3.Cross(xAxis, yAxis);
        if (n.z < 0f) n = -n; // 법선이 항상 같은 방향(+z)을 향하도록 정규화

        yawDeg = Mathf.Atan2(n.x, n.z) * Mathf.Rad2Deg;
        pitchDeg = Mathf.Atan2(n.y, Mathf.Sqrt(n.x * n.x + n.z * n.z)) * Mathf.Rad2Deg;
    }

    private static float SafeDistance(Vector3 a, Vector3 b)
    {
        float d = Vector3.Distance(a, b);
        return d > 0f ? d : 1f; // 기존 로직과 동일: 0이면 1로 대체 (0 나눗셈 방지)
    }

    // ─────────────────────────────────────────────────────────
    // 스냅샷 / 종합 평가
    // ─────────────────────────────────────────────────────────
    private struct Snapshot
    {
        public float smile, surprise, angry, gaze, angle, yaw, pitch;
        public int faceFrames, gazeFrames, angleFrames;
        public bool hasFace;   // 표정 데이터가 하나라도 있는지
        public bool hasAngle;  // 각도 데이터가 하나라도 있는지
    }

    private struct EvaluationResult
    {
        public EvaluationArea faceArea, gazeArea, angleArea;
        public ExpressionGrade grade;
        public string summary, detail, notes;
        public int attitudeScore; // 3개 영역 점수 평균 (반올림, 0~5)
    }

    private Snapshot TakeSnapshot()
    {
        lock (statsLock)
        {
            return new Snapshot
            {
                smile = smileAcc.Average,
                surprise = surpriseAcc.Average,
                angry = angryAcc.Average,
                // 홍채 미검출 시 시선 점수는 기본값 5점 처리
                gaze = gazeAcc.count > 0 ? gazeAcc.Average : 5f,
                angle = angleAcc.Average,
                yaw = yawAcc.Average,
                pitch = pitchAcc.Average,
                faceFrames = smileAcc.count,
                gazeFrames = gazeAcc.count,
                angleFrames = angleAcc.count,
                hasFace = smileAcc.count > 0,
                hasAngle = angleAcc.count > 0
            };
        }
    }

    // 메인 스레드에서만 호출할 것 (sharedSb 사용)
    private EvaluationResult EvaluateAll(Snapshot s)
    {
        var result = new EvaluationResult();

        // 표정
        string faceSummary, faceDetail, faceNotes;
        float faceScore;
        if (s.hasFace)
        {
            (result.grade, faceSummary, faceDetail, faceNotes, faceScore) =
                EvaluateFaceExpression(s.smile, s.surprise, s.angry);
        }
        else
        {
            result.grade = ExpressionGrade.Average;
            faceSummary = "측정 불가";
            faceDetail = "얼굴이 정면에서 크게 벗어나 표정을 측정하지 못했습니다.";
            faceNotes = "";
            faceScore = 0f;
        }

        var (gazeSummary, gazeDetail, gazeNotes) = EvaluateGaze(s.gaze);
        var (angleSummary, angleDetail, angleNotes) = EvaluateAngle(s.angle, s.yaw, s.pitch);

        int faceRounded = RoundScore(faceScore);
        int gazeRounded = RoundScore(s.gaze);
        int angleRounded = RoundScore(s.angle);

        result.faceArea = new EvaluationArea
        {
            areaName = "표정",
            score = faceRounded,
            result = faceDetail,
            improvement = faceNotes
        };
        result.gazeArea = new EvaluationArea
        {
            areaName = "시선(눈동자)",
            score = gazeRounded,
            result = gazeDetail,
            improvement = gazeNotes
        };
        result.angleArea = new EvaluationArea
        {
            areaName = "얼굴 각도",
            score = angleRounded,
            result = angleDetail,
            improvement = angleNotes
        };

        result.attitudeScore = RoundScore((faceRounded + gazeRounded + angleRounded) / 3f);

        // 조합 문자열은 sharedSb로 만들어 중간 string 생성을 줄임
        sharedSb.Clear();
        sharedSb.Append("[표정] ").Append(faceSummary)
                .Append(" / [시선] ").Append(gazeSummary)
                .Append(" / [얼굴각도] ").Append(angleSummary);
        result.summary = sharedSb.ToString();

        sharedSb.Clear();
        sharedSb.Append("표정: ").Append(faceDetail).Append('\n')
                .Append("시선: ").Append(gazeDetail).Append('\n')
                .Append("얼굴각도: ").Append(angleDetail);
        result.detail = sharedSb.ToString();

        result.notes = FormatNotesByArea(faceNotes, gazeNotes, angleNotes);

        return result;
    }

    /// <summary>
    /// InterviewDbManager에 태도 점수 및 피드백 전송
    /// </summary>
    private void SaveToDatabase(int score, string adviceText, string summaryText)
    {
        // Start() 시점에 싱글톤이 없었을 경우를 대비한 지연 조회
        if (dbManager == null) dbManager = InterviewDbManager.Instance;

        if (dbManager == null)
        {
            Debug.LogWarning("[fass] InterviewDbManager를 찾지 못해 DB 저장을 건너뜁니다.");
            return;
        }

        bool success = dbManager.SaveFaceEvaluation(
            sessionId: dbManager.CurrentSessionId,
            scoreAttitude: score,
            adviceAttitudeText: adviceText,
            evalAttitudeText: summaryText
        );

        if (success)
            Debug.Log($"[fass] DB 저장 성공: 태도점수({score}/5)");
        else
            Debug.LogWarning("[fass] DB 저장 스킵 또는 실패: 활성화된 면접 세션이 없습니다.");
    }

    /// <summary>
    /// 0~5 범위로 제한한 뒤 정수로 반올림합니다. (2.5 → 3, 3.5 → 4처럼 항상 반올림)
    /// </summary>
    private int RoundScore(float v)
    {
        return Mathf.Clamp(Mathf.FloorToInt(v + 0.5f), 0, 5);
    }

    private const string KeepMessage = "현재 상태가 좋으니, 지금의 느낌을 최대한 유지해주세요.";

    /// <summary>
    /// 개선사항(notes)을 영역별 라벨 + 줄바꿈 형식으로 합칩니다. (sharedSb 재사용, 로컬 함수/클로저 없음)
    /// </summary>
    private string FormatNotesByArea(string faceNotes, string gazeNotes, string angleNotes)
    {
        sharedSb.Clear();
        AppendAreaNote("표정", faceNotes);
        sharedSb.Append('\n');
        AppendAreaNote("시선", gazeNotes);
        sharedSb.Append('\n');
        AppendAreaNote("얼굴각도", angleNotes);
        return sharedSb.ToString();
    }

    private void AppendAreaNote(string label, string notes)
    {
        sharedSb.Append(label).Append(": ");
        string trimmed = notes?.Trim();
        sharedSb.Append(string.IsNullOrEmpty(trimmed) ? KeepMessage : trimmed);
    }

    // ─────────────────────────────────────────────────────────
    // 원자료(raw ratio) 계산 - faceWidth는 프레임당 1회만 계산해서 전달
    // ─────────────────────────────────────────────────────────
    /// <summary>
    /// 미소 지표 = (입꼬리 간 너비 + 입꼬리 상승량) / 얼굴 너비
    /// - 기존 지표(안쪽 입술 상하 간격)는 "입 벌림"이라 말할 때마다 미소로 잡혔습니다.
    /// - 이제는 입꼬리(61, 291)가 옆으로 벌어지고 위로 올라가는 정도를 봅니다.
    /// - 상승량은 이마-턱 축 기준으로 재므로 고개가 기울어져도(roll) 영향이 적습니다.
    /// </summary>
    private float GetRawSmileRatio(List<Vector3> landmarks, float faceWidth)
    {
        Vector3 leftCorner = landmarks[61];
        Vector3 rightCorner = landmarks[291];
        float mouthWidth = Vector3.Distance(leftCorner, rightCorner);

        Vector3 lipCenter = (landmarks[13] + landmarks[14]) * 0.5f;
        Vector3 cornerCenter = (leftCorner + rightCorner) * 0.5f;

        Vector3 down = landmarks[152] - landmarks[10];
        float downLen = down.magnitude;
        float lift = downLen > 0f ? Vector3.Dot(lipCenter - cornerCenter, down / downLen) : 0f;

        return (mouthWidth + lift) / faceWidth;
    }

    private float GetRawSurpriseRatio(List<Vector3> landmarks, float faceWidth)
    {
        float leftEyeOpen = Vector3.Distance(landmarks[LEFT_EYE_UPPER], landmarks[LEFT_EYE_LOWER]);
        float rightEyeOpen = Vector3.Distance(landmarks[RIGHT_EYE_UPPER], landmarks[RIGHT_EYE_LOWER]);
        return ((leftEyeOpen + rightEyeOpen) * 0.5f) / faceWidth;
    }

    private float GetRawAngryRatio(List<Vector3> landmarks, float faceWidth)
    {
        return Vector3.Distance(landmarks[55], landmarks[285]) / faceWidth;
    }

    private float HorizontalIrisRatio(Vector3 iris, Vector3 innerCorner, Vector3 outerCorner)
    {
        float eyeWidth = Vector3.Distance(innerCorner, outerCorner);
        if (eyeWidth <= 0f) return 0.5f;
        return Vector3.Distance(iris, innerCorner) / eyeWidth;
    }

    private float VerticalIrisRatio(Vector3 iris, Vector3 upperLid, Vector3 lowerLid)
    {
        float eyeHeight = Vector3.Distance(upperLid, lowerLid);
        if (eyeHeight <= 0f) return 0.5f;
        return Vector3.Distance(iris, upperLid) / eyeHeight;
    }

    private float GetRawGazeHorizontalRatio(List<Vector3> landmarks)
    {
        float leftRatio = HorizontalIrisRatio(landmarks[LEFT_IRIS_CENTER], landmarks[LEFT_EYE_INNER], landmarks[LEFT_EYE_OUTER]);
        float rightRatio = HorizontalIrisRatio(landmarks[RIGHT_IRIS_CENTER], landmarks[RIGHT_EYE_INNER], landmarks[RIGHT_EYE_OUTER]);
        return (leftRatio + rightRatio) * 0.5f;
    }

    private float GetRawGazeVerticalRatio(List<Vector3> landmarks)
    {
        float leftRatio = VerticalIrisRatio(landmarks[LEFT_IRIS_CENTER], landmarks[LEFT_EYE_UPPER], landmarks[LEFT_EYE_LOWER]);
        float rightRatio = VerticalIrisRatio(landmarks[RIGHT_IRIS_CENTER], landmarks[RIGHT_EYE_UPPER], landmarks[RIGHT_EYE_LOWER]);
        return (leftRatio + rightRatio) * 0.5f;
    }

    // out string reason 제거: 매 프레임 호출되는데 사용하지 않았고, 사용 시 문자열 보간으로 할당이 발생함
    // yawDelta/pitchDelta: 캘리브레이션 정면 자세 대비 편차(°, 절댓값)
    private bool IsFaceTooAngled(float yawDelta, float pitchDelta)
    {
        return yawDelta > maxYawDegrees || pitchDelta > maxPitchDegrees;
    }

    // ─────────────────────────────────────────────────────────
    // 영역별 점수(0~5) 계산
    // ─────────────────────────────────────────────────────────
    private float CalculateSmile(List<Vector3> landmarks, float faceWidth)
    {
        float ratio = GetRawSmileRatio(landmarks, faceWidth);
        return Mathf.Clamp((ratio - neutralSmileRatio) * smileGain, 0f, 5f);
    }

    // eyeOpenRatio: GetRawSurpriseRatio 결과(이미 프레임에서 계산됨)를 재사용
    private float CalculateSurprise(float eyeOpenRatio)
    {
        return Mathf.Clamp((eyeOpenRatio - neutralSurpriseRatio) * surpriseGain, 0f, 5f);
    }

    private float CalculateAngry(List<Vector3> landmarks, float faceWidth)
    {
        float ratio = GetRawAngryRatio(landmarks, faceWidth);
        return Mathf.Clamp((neutralAngryRatio - ratio) * angryGain, 0f, 5f);
    }

    /// <summary>
    /// 5점 = 정면 응시, 0점 = 시선이 크게 이탈.
    /// </summary>
    private float CalculateGazeScore(List<Vector3> landmarks)
    {
        float h = GetRawGazeHorizontalRatio(landmarks);
        float v = GetRawGazeVerticalRatio(landmarks);

        float deviation = Mathf.Abs(h - neutralGazeHorizontalRatio) + Mathf.Abs(v - neutralGazeVerticalRatio);
        return Mathf.Clamp(5f - deviation * gazeSensitivity, 0f, 5f);
    }

    /// <summary>
    /// 5점 = 캘리브레이션 때의 정면 자세, 0점 = 허용 한계(maxYawDegrees/maxPitchDegrees) 도달.
    /// </summary>
    private float CalculateAngleScore(float yawDelta, float pitchDelta)
    {
        float normalizedYaw = maxYawDegrees > 0f ? Mathf.Clamp01(yawDelta / maxYawDegrees) : 0f;
        float normalizedPitch = maxPitchDegrees > 0f ? Mathf.Clamp01(pitchDelta / maxPitchDegrees) : 0f;
        float combined = (normalizedYaw + normalizedPitch) * 0.5f;
        return Mathf.Clamp(5f * (1f - combined), 0f, 5f);
    }

    // ─────────────────────────────────────────────────────────
    // 영역별 평가 결과 / 개선사항 생성 (메인 스레드 전용, sharedSb 재사용)
    // 문구는 모두 문자열 리터럴이므로 추가 할당이 없고, notes만 ToString()으로 1회 생성됩니다.
    // ─────────────────────────────────────────────────────────
    private (ExpressionGrade grade, string summary, string detail, string improvementNotes, float normalizedScore) EvaluateFaceExpression(
        float smile, float surprise, float angry)
    {
        float angryPenalty = angry * 1.5f;
        float surprisePenalty = surprise * 0.8f;

        float smileBonus;
        if (smile < 1.5f)
            smileBonus = smile * 0.6f;
        else if (smile <= 3f)
            smileBonus = 1f + (smile - 1.5f) * 1f;
        else
            smileBonus = 2.5f - (smile - 3f) * 0.5f;

        float totalScore = smileBonus - angryPenalty - surprisePenalty;

        const float normMin = -1.5f;
        const float normMax = 1.5f;
        float normalizedScore = Mathf.Clamp((totalScore - normMin) / (normMax - normMin) * 5f, 0f, 5f);

        ExpressionGrade grade;
        string summary;
        string detail;

        if (totalScore >= 1.5f)
        {
            grade = ExpressionGrade.Excellent;
            summary = "매우 안정적";
            detail = "침착하고 신뢰감 있는 표정을 유지하고 있습니다.";
        }
        else if (totalScore >= 0.5f)
        {
            grade = ExpressionGrade.Good;
            summary = "안정적";
            detail = "전반적으로 무난하고 안정된 표정입니다.";
        }
        else if (totalScore >= -0.5f)
        {
            grade = ExpressionGrade.Average;
            summary = "보통";
            detail = "특별한 문제는 없으나, 조금 더 여유 있는 인상을 위해 표정을 다듬어보세요.";
        }
        else if (totalScore >= -1.5f)
        {
            grade = ExpressionGrade.NeedsWork;
            summary = "긴장 감지";
            detail = "긴장하거나 동요하는 표정이 감지되었습니다.";
        }
        else
        {
            grade = ExpressionGrade.Poor;
            summary = "불안정";
            detail = "면접 태도에 부정적으로 작용할 수 있는 표정 변화가 감지되었습니다.";
        }

        sharedSb.Clear();

        if (angry >= 2.5f)
            sharedSb.AppendLine("- 미간/눈썹에 긴장이 감지됩니다. 질문을 들을 때 표정을 편하게 풀어보세요.");
        if (surprise >= 3f)
            sharedSb.AppendLine("- 예상 밖 반응이 자주 감지됩니다. 답변 전 잠깐의 여유를 가져보세요.");
        if (smile < 0.5f)
            sharedSb.AppendLine("- 표정이 다소 경직되어 있습니다. 자연스러운 미소를 시도해보세요.");
        if (smile > 4f)
            sharedSb.AppendLine("- 미소가 다소 과도하게 유지되고 있습니다. 상황에 맞는 톤 조절이 필요할 수 있습니다.");

        return (grade, summary, detail, sharedSb.ToString(), normalizedScore);
    }

    private (string summary, string detail, string improvementNotes) EvaluateGaze(float gazeScore)
    {
        string summary;
        string detail;

        if (gazeScore >= 4.0f)
        {
            summary = "정면 응시 매우 우수";
            detail = "눈동자가 카메라를 정확히 향하고 있어 신뢰감을 줍니다.";
        }
        else if (gazeScore >= 3.0f)
        {
            summary = "정면 응시 양호";
            detail = "대체로 카메라를 잘 응시하고 있습니다.";
        }
        else if (gazeScore >= 2.0f)
        {
            summary = "시선 흔들림 보통";
            detail = "간헐적으로 시선이 카메라에서 벗어나는 모습이 감지되었습니다.";
        }
        else if (gazeScore >= 1.0f)
        {
            summary = "시선 이탈 잦음";
            detail = "시선이 자주 카메라가 아닌 다른 곳을 향합니다.";
        }
        else
        {
            summary = "시선 불안정";
            detail = "카메라를 회피하는 경향이 강하게 감지되었습니다.";
        }

        sharedSb.Clear();
        if (gazeScore < 3.5f)
            sharedSb.AppendLine("- 시선이 카메라에서 벗어나는 경우가 있습니다. 답변 중에도 카메라 렌즈를 바라보는 연습을 해보세요.");
        if (gazeScore < 2.0f)
            sharedSb.AppendLine("- 생각을 정리할 때 시선을 위/아래로 피하기보다, 잠깐 멈춘 뒤 카메라를 다시 응시하는 습관을 들여보세요.");

        return (summary, detail, sharedSb.ToString());
    }

    private (string summary, string detail, string improvementNotes) EvaluateAngle(float angleScore, float avgYaw, float avgPitch)
    {
        string summary;
        string detail;

        if (angleScore >= 4.0f)
        {
            summary = "정면 유지 매우 우수";
            detail = "고개를 정면으로 안정적으로 유지하고 있습니다.";
        }
        else if (angleScore >= 3.0f)
        {
            summary = "정면 유지 양호";
            detail = "약간의 각도 변화는 있으나 전반적으로 정면을 잘 유지합니다.";
        }
        else if (angleScore >= 2.0f)
        {
            summary = "각도 변화 보통";
            detail = "고개를 돌리거나 기울이는 모습이 다소 감지되었습니다.";
        }
        else if (angleScore >= 1.0f)
        {
            summary = "각도 이탈 잦음";
            detail = "고개가 자주 옆으로 돌아가거나 기울어져 있습니다.";
        }
        else
        {
            summary = "정면 이탈 심함";
            detail = "얼굴이 카메라 정면에서 크게 벗어나 있는 경우가 많습니다.";
        }

        sharedSb.Clear();
        if (angleScore < 3.5f)
            sharedSb.AppendLine("- 고개 방향이 흔들립니다. 카메라를 정면으로 응시하도록 자세를 교정해보세요.");
        if (angleScore < 2.0f)
            sharedSb.AppendLine("- 답변 중 고개가 자주 돌아갑니다. 모니터나 카메라 위치를 눈높이에 맞추면 자연스럽게 정면을 유지하기 쉽습니다.");

        return (summary, detail, sharedSb.ToString());
    }

    // -----------------------------------------------
    // 면접 종료 이벤트 수신 시 자동 호출 (메인 스레드에서 호출된다고 가정)
    // -----------------------------------------------
    private void HandleInterviewEnded(HJS.InterviewResultData resultData)
    {
        Debug.Log("[fass] 면접 종료 감지 → 최종 태도 점수 계산 시작");
        CalculateFinalScoreAndSave();
    }

    // -----------------------------------------------
    // 면접 종료 시 최종 태도 점수 계산 + DB 저장 (전체 1회만 실행)
    // -----------------------------------------------
    public void CalculateFinalScoreAndSave()
    {
        Snapshot snap = TakeSnapshot();

        if (!snap.hasFace && !snap.hasAngle)
        {
            Debug.LogWarning("[fass] 측정 데이터 없음 → 카메라 미연결 메시지 저장");

            const string noDataSummary = "카메라 미연결 또는 얼굴이 감지되지 않아 태도 점수를 측정할 수 없습니다.";
            const string noDataAdvice = "면접 시 카메라를 연결하고 얼굴이 화면에 잘 보이도록 위치를 조정해주세요.";

            SaveToDatabase(score: 0, adviceText: noDataAdvice, summaryText: noDataSummary);

            if (csvLogger != null)
                csvLogger.SaveScoreToCSV(DateTime.Now, 0f, noDataSummary, noDataAdvice);
            return;
        }

        EvaluationResult eval = EvaluateAll(snap);
        ApplyEvaluation(eval);

        Debug.Log($"[fass] 최종 태도 점수: {eval.attitudeScore}/5 (표정 프레임 {snap.faceFrames}, 시선 프레임 {snap.gazeFrames}, 각도 프레임 {snap.angleFrames})");

        SaveToDatabase(eval.attitudeScore, eval.notes, eval.detail);

        if (csvLogger != null)
            csvLogger.SaveScoreToCSV(DateTime.Now, eval.attitudeScore, eval.detail, eval.notes);
    }
}