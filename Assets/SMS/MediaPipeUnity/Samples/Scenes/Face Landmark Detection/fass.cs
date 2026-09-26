using System;
using System.Collections.Generic;
using System.Text;
using Mediapipe.Tasks.Vision.FaceLandmarker;
using Mediapipe.Unity.Sample.FaceLandmarkDetection;
using UnityEngine;
using InterviewDb; // InterviewDbManager 접근용 네임스페이스

public class fass : MonoBehaviour
{
    [Header("CSV Logger 연결")]
    [Tooltip("유니티 인스펙터 창에서 CustomPathCSVLogger 오브젝트를 여기에 드래그 앤 드롭 하세요.")]
    public CustomPathCSVLogger csvLogger;

    [Header("MediaPipe Runner 연결")]
    [Tooltip("씬에 있는 FaceLandmarkerRunner 오브젝트를 여기에 드래그하세요.")]
    public FaceLandmarkerRunner runner;

    // InterviewDbManager 참조: 인스펙터 연결 없이 Start()에서 자동으로 찾아 캐싱합니다.
    private InterviewDbManager dbManager;

    // ─────────────────────────────────────────────────────────
    // 랜드마크 개수 상수
    // ─────────────────────────────────────────────────────────
    private const int FACE_LANDMARK_COUNT = 468;   // 기본 얼굴 메쉬
    private const int IRIS_LANDMARK_COUNT = 478;   // 얼굴 메쉬(468) + 홍채(10) - MediaPipe refine_landmarks 옵션 필요

    // 홍채/눈 랜드마크 인덱스 (refine_landmarks = true 일 때 유효)
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
    // 캘리브레이션 (무표정/정면 기준값) - 수동 입력
    // ─────────────────────────────────────────────────────────
    [Header("캘리브레이션 (무표정 기준값) - 수동 입력")]
    [Tooltip("무표정 상태에서 측정한 입 높이/너비 비율. 인스펙터에서 직접 값을 입력/조정하세요.")]
    public float neutralSmileRatio = 0.022f;

    [Tooltip("무표정 상태에서 측정한 눈 뜬 정도/얼굴너비 비율. 인스펙터에서 직접 값을 입력/조정하세요.")]
    public float neutralSurpriseRatio = 0.060f;

    [Tooltip("무표정 상태에서 측정한 눈썹 사이 거리/얼굴너비 비율. 인스펙터에서 직접 값을 입력/조정하세요.")]
    public float neutralAngryRatio = 0.212f;

    [Header("캘리브레이션 (정면 응시 기준값) - 수동 입력")]
    [Tooltip("카메라를 정면으로 응시할 때의 눈동자(홍채) 좌우 위치 비율(0=눈 안쪽, 1=눈 바깥쪽). 캘리브레이션 후 값을 조정하세요.")]
    public float neutralGazeHorizontalRatio = 0.5f;

    [Tooltip("카메라를 정면으로 응시할 때의 눈동자(홍채) 상하 위치 비율(0=윗눈꺼풀, 1=아랫눈꺼풀). 캘리브레이션 후 값을 조정하세요.")]
    public float neutralGazeVerticalRatio = 0.5f;

    [Tooltip("시선 이탈 정도를 점수로 환산할 때 곱해지는 민감도. 값이 클수록 작은 눈동자 이동에도 점수가 크게 떨어집니다.")]
    public float gazeSensitivity = 18f;

    private List<Vector3> latestLandmarks = null;

    [Header("얼굴 각도 제한 (Head Pose Gate) - 수동 입력")]
    [Tooltip("체크하면 얼굴이 특정 각도 이상 돌아갔을 때 표정/시선 분석(점수 계산)을 건너뜁니다. 각도 점수 자체는 게이트와 무관하게 계속 측정됩니다.")]
    public bool enableAngleGate = true;

    [Tooltip("좌우 회전(Yaw) 허용 한계. 이 값에 가까워질수록 얼굴 각도 점수가 0점에 가까워집니다.")]
    public float maxYawRatio = 0.80f;

    [Tooltip("상하 회전(Pitch) 허용 한계. 이 값에 가까워질수록 얼굴 각도 점수가 0점에 가까워집니다.")]
    public float maxPitchRatio = 0.80f;

    [Tooltip("각도 초과로 표정/시선 분석이 멈춘 상태인지")]
    public bool isFaceTooAngled = false;

    [Header("노이즈 완화 (이동평균)")]
    [Tooltip("저장 시점에 사용할 최근 프레임 점수의 개수.")]
    public int smoothingWindowSize = 30;

    // 표정 하위 지표 버퍼
    private readonly Queue<float> smileBuffer = new Queue<float>();
    private readonly Queue<float> surpriseBuffer = new Queue<float>();
    private readonly Queue<float> angryBuffer = new Queue<float>();

    // 시선(눈동자) 점수 버퍼
    private readonly Queue<float> gazeBuffer = new Queue<float>();

    // 얼굴 각도 점수 및 원자료(raw ratio) 버퍼
    private readonly Queue<float> angleScoreBuffer = new Queue<float>();
    private readonly Queue<float> yawRatioBuffer = new Queue<float>();
    private readonly Queue<float> pitchRatioBuffer = new Queue<float>();

    private float smileTotal = 0f;
    private float surpriseTotal = 0f;
    private float angryTotal = 0f;
    private int smileCount = 0;
    private int surpriseCount = 0;
    private int angryCount = 0;

    public float SmileTotal => smileTotal;
    public float SmileAverage => smileCount > 0 ? smileTotal / smileCount : 0f;
    public int SmileCount => smileCount;

    public float SurpriseTotal => surpriseTotal;
    public float SurpriseAverage => surpriseCount > 0 ? surpriseTotal / surpriseCount : 0f;
    public int SurpriseCount => surpriseCount;

    public float AngryTotal => angryTotal;
    public float AngryAverage => angryCount > 0 ? angryTotal / angryCount : 0f;
    public int AngryCount => angryCount;

    public void ResetStatistics()
    {
        smileTotal = 0f;
        surpriseTotal = 0f;
        angryTotal = 0f;
        smileCount = 0;
        surpriseCount = 0;
        angryCount = 0;
    }

    // ─────────────────────────────────────────────────────────
    // 표정 평가 시스템 (하위 호환 유지용 - 전체 "태도" 결과를 담음)
    // ─────────────────────────────────────────────────────────
    [Header("표정 평가 시스템")]
    public ExpressionGrade latestGrade;
    public string latestEvaluationSummary = "";
    public string latestEvaluationDetail = "";
    public string latestImprovementNotes = "";
    public float latestEvaluationScore = 0f;

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
        public float score;        // 0~5, 소수 첫째자리
        public string result;      // 평가 결과 (문장)
        public string improvement; // 개선 사항 (문장, 여러 줄 가능)
    }

    [Header("태도 하위 영역 결과 (표정 / 시선 / 얼굴각도)")]
    public EvaluationArea latestFaceExpressionArea;
    public EvaluationArea latestGazeArea;
    public EvaluationArea latestAngleArea;

    [Tooltip("표정, 시선, 얼굴각도 3개 영역 점수를 합산 후 0~5점으로 정규화한 최종 태도 점수")]
    public float latestAttitudeScore = 0f;

    /// <summary>
    /// UI 테이블(평가 영역 / 평가 결과 / 개선 사항) 바인딩용 - 표정/시선/얼굴각도 3개 행을 반환합니다.
    /// </summary>
    public List<EvaluationArea> GetAttitudeAreas()
    {
        return new List<EvaluationArea> { latestFaceExpressionArea, latestGazeArea, latestAngleArea };
    }

    void OnEnable()
    {
        if (runner != null)
            runner.OnResultOutput += HandleResult;
        else
            Debug.LogWarning("[fass] runner가 인스펙터에 연결되지 않았습니다.");

        // InterviewManager의 면접 종료 요청 이벤트 구독
        // 면접이 끝나면 HandleInterviewEnded()가 자동으로 실행됨
        // OnInterviewEndRequested는 멀티캐스트 이벤트라
        // GeminiManager, Play.cs와 함께 동시에 구독 가능
        HJS.InterviewManager.OnInterviewEndRequested += HandleInterviewEnded;
    }

    void Start()
    {
        // 메인 스레드(Start)에서 한 번만 안전하게 찾아 캐싱합니다.
        // 이후 MediaPipe 콜백 스레드에서는 이 캐싱된 참조만 사용하고,
        // InterviewDbManager.Instance(FindObjectOfType)는 다시 호출하지 않습니다.
        dbManager = InterviewDbManager.Instance;
        if (dbManager == null)
            Debug.LogWarning("[fass] InterviewDbManager를 씬에서 찾지 못했습니다. DB 저장이 스킵됩니다.");
    }

    void OnDisable()
    {
        if (runner != null)
            runner.OnResultOutput -= HandleResult;

        // 씬 전환 시 오브젝트 파괴 전 구독 해제
        // 해제 안 하면 다음 면접에서 중복 실행될 수 있음
        HJS.InterviewManager.OnInterviewEndRequested -= HandleInterviewEnded;
    }

    private void HandleResult(FaceLandmarkerResult result)
    {
        if (result.faceLandmarks == null || result.faceLandmarks.Count == 0)
        {
            return;
        }

        var faceLandmarks = result.faceLandmarks[0];
        var landmarksList = new List<Vector3>(faceLandmarks.landmarks.Count);

        foreach (var lm in faceLandmarks.landmarks)
        {
            landmarksList.Add(new Vector3(lm.x, lm.y, lm.z));
        }

        OnFaceLandmarksDetected(landmarksList);
    }

    // 더 이상 사용 안 함(주석처리 했으니 확인 요망 검증이후 삭제)
    /*
    [Header("저장 간격")]
    [Tooltip("점수를 저장할 최소 간격(분 단위)")]
    public float logIntervalMinutes = 3f;

    private DateTime lastLoggedTime = DateTime.MinValue;
    */

    public void OnFaceLandmarksDetected(List<Vector3> landmarks)
    {
        latestLandmarks = landmarks;

        if (landmarks == null || landmarks.Count < FACE_LANDMARK_COUNT)
        {
            return;
        }

        // ── 1) 얼굴 각도(Yaw/Pitch)는 게이트 여부와 무관하게 항상 측정합니다.
        //     (고개를 자주 돌리는 습관 자체가 태도 평가 대상이므로, 게이트가 걸려도 각도 측정은 계속되어야 합니다.)
        float yawRatio = GetRawYawRatio(landmarks);
        float pitchRatio = GetRawPitchRatio(landmarks);
        float frameAngleScore = CalculateAngleScore(yawRatio, pitchRatio);

        PushToBuffer(yawRatioBuffer, yawRatio);
        PushToBuffer(pitchRatioBuffer, pitchRatio);
        PushToBuffer(angleScoreBuffer, frameAngleScore);

        bool tooAngled = enableAngleGate && IsFaceTooAngled(yawRatio, pitchRatio, out string angleReason);
        isFaceTooAngled = tooAngled;

        // ── 2) 얼굴 각도가 너무 심하면 표정/시선 측정은 신뢰도가 낮으므로 이번 프레임은 건너뜁니다.
        //     (단, 위에서 각도 점수 자체는 이미 반영되었습니다.)
        if (!tooAngled)
        {
            float frameSmile = CalculateSmile(landmarks);
            float frameSurprise = CalculateSurprise(landmarks);
            float frameAngry = CalculateAngry(landmarks);

            PushToBuffer(smileBuffer, frameSmile);
            PushToBuffer(surpriseBuffer, frameSurprise);
            PushToBuffer(angryBuffer, frameAngry);

            if (landmarks.Count >= IRIS_LANDMARK_COUNT)
            {
                float frameGaze = CalculateGazeScore(landmarks);
                PushToBuffer(gazeBuffer, frameGaze);
            }
            else if (!warnedNoIris)
            {
                warnedNoIris = true;
                Debug.LogWarning("[fass] 홍채(iris) 랜드마크가 감지되지 않았습니다. MediaPipe FaceLandmarker의 refine_landmarks(478개 랜드마크) 옵션을 켜야 시선(눈동자) 평가가 동작합니다.");
            }
        }

        // ── 3) Inspector 실시간 표시용 업데이트
        // DB 저장 없이 개발자 확인용으로만 사용
        // 최종 계산 및 DB 저장은 면접 종료 시 CalculateFinalScoreAndSave()에서 처리
        if (smileBuffer.Count == 0 && angleScoreBuffer.Count == 0) return;

        float tmpSmile = Average(smileBuffer);
        float tmpSurprise = Average(surpriseBuffer);
        float tmpAngry = Average(angryBuffer);
        float tmpGaze = gazeBuffer.Count > 0 ? Average(gazeBuffer) : 5f;
        float tmpAngle = Average(angleScoreBuffer);
        float tmpYaw = Average(yawRatioBuffer);
        float tmpPitch = Average(pitchRatioBuffer);

        var (tmpGrade, tmpFaceSummary, tmpFaceDetail, tmpFaceNotes, tmpFaceScore) =
            EvaluateFaceExpression(tmpSmile, tmpSurprise, tmpAngry);
        var (tmpGazeSummary, tmpGazeDetail, tmpGazeNotes) =
            EvaluateGaze(tmpGaze);
        var (tmpAngleSummary, tmpAngleDetail, tmpAngleNotes) =
            EvaluateAngle(tmpAngle, tmpYaw, tmpPitch);

        float tmpFaceRounded = Round1(tmpFaceScore);
        float tmpGazeRounded = Round1(tmpGaze);
        float tmpAngleRounded = Round1(tmpAngle);

        latestFaceExpressionArea = new EvaluationArea
        {
            areaName = "표정",
            score = tmpFaceRounded,
            result = tmpFaceDetail,
            improvement = tmpFaceNotes
        };
        latestGazeArea = new EvaluationArea
        {
            areaName = "시선(눈동자)",
            score = tmpGazeRounded,
            result = tmpGazeDetail,
            improvement = tmpGazeNotes
        };
        latestAngleArea = new EvaluationArea
        {
            areaName = "얼굴 각도",
            score = tmpAngleRounded,
            result = tmpAngleDetail,
            improvement = tmpAngleNotes
        };

        latestAttitudeScore = Round1((tmpFaceRounded + tmpGazeRounded + tmpAngleRounded) / 3f);
        latestGrade = tmpGrade;
        latestEvaluationScore = latestAttitudeScore;

        string tmpSummary = $"[표정] {tmpFaceSummary} / [시선] {tmpGazeSummary} / [얼굴각도] {tmpAngleSummary}";
        string tmpDetail = $"표정: {tmpFaceDetail}\n시선: {tmpGazeDetail}\n얼굴각도: {tmpAngleDetail}";
        string tmpNotes = tmpFaceNotes + tmpGazeNotes + tmpAngleNotes;

        latestEvaluationSummary = tmpSummary;
        latestEvaluationDetail = tmpDetail;
        latestImprovementNotes = tmpNotes;
    }

    /// <summary>
    /// InterviewDbManager에 태도 점수 및 피드백 전송
    /// (Start()에서 캐싱해둔 dbManager를 사용, 콜백 스레드에서 Instance를 다시 호출하지 않음)
    /// </summary>
    private void SaveToDatabase(float score, string adviceText, string summaryText)
    {
        if (dbManager == null)
        {
            Debug.LogWarning("[fass] dbManager 캐싱 실패로 DB 저장을 건너뜁니다.");
            return;
        }

        // currentSessionId 자동 반영 (-1 지정)
        bool success = dbManager.SaveFaceEvaluation(
            sessionId: InterviewDbManager.Instance.CurrentSessionId,
            scoreAttitude: (int)score,
            adviceAttitudeText: adviceText,
            evalAttitudeText: summaryText
        );

        if (success)
        {
            Debug.Log($"[fass] DB 저장 성공: 태도점수({score}/5)");
        }
        else
        {
            Debug.LogWarning("[fass] DB 저장 스킵 또는 실패: 활성화된 면접 세션이 없습니다.");
        }
    }

    private void PushToBuffer(Queue<float> buffer, float value)
    {
        buffer.Enqueue(value);
        int maxSize = Mathf.Max(1, smoothingWindowSize);
        while (buffer.Count > maxSize)
        {
            buffer.Dequeue();
        }
    }

    private float Average(Queue<float> buffer)
    {
        if (buffer.Count == 0) return 0f;
        float sum = 0f;
        foreach (float v in buffer) sum += v;
        return sum / buffer.Count;
    }

    private float Round1(float v)
    {
        return Mathf.Round(Mathf.Clamp(v, 0f, 5f) * 10f) / 10f;
    }

    // ─────────────────────────────────────────────────────────
    // 원자료(raw ratio) 계산
    // ─────────────────────────────────────────────────────────
    private float GetRawSmileRatio(List<Vector3> landmarks)
    {
        float mouthHeight = Vector3.Distance(landmarks[13], landmarks[14]);
        float faceWidth = Vector3.Distance(landmarks[234], landmarks[454]);
        return mouthHeight / (faceWidth > 0 ? faceWidth : 1f);
    }

    private float GetRawSurpriseRatio(List<Vector3> landmarks)
    {
        float leftEyeOpen = Vector3.Distance(landmarks[159], landmarks[145]);
        float rightEyeOpen = Vector3.Distance(landmarks[386], landmarks[374]);
        float avgEyeOpen = (leftEyeOpen + rightEyeOpen) / 2f;
        float faceWidth = Vector3.Distance(landmarks[234], landmarks[454]);
        return avgEyeOpen / (faceWidth > 0 ? faceWidth : 1f);
    }

    private float GetRawAngryRatio(List<Vector3> landmarks)
    {
        float eyebrowDist = Vector3.Distance(landmarks[55], landmarks[285]);
        float faceWidth = Vector3.Distance(landmarks[234], landmarks[454]);
        return eyebrowDist / (faceWidth > 0 ? faceWidth : 1f);
    }

    private float GetRawYawRatio(List<Vector3> landmarks)
    {
        float faceWidth = Vector3.Distance(landmarks[234], landmarks[454]);
        float leftEarZ = landmarks[234].z;
        float rightEarZ = landmarks[454].z;
        return Mathf.Abs(leftEarZ - rightEarZ) / (faceWidth > 0 ? faceWidth : 1f);
    }

    private float GetRawPitchRatio(List<Vector3> landmarks)
    {
        float faceHeight = Vector3.Distance(landmarks[10], landmarks[152]);
        float foreheadZ = landmarks[10].z;
        float chinZ = landmarks[152].z;
        return Mathf.Abs(foreheadZ - chinZ) / (faceHeight > 0 ? faceHeight : 1f);
    }

    /// <summary>
    /// 한쪽 눈의 홍채(눈동자) 좌우 위치 비율. 0 = 눈 안쪽 끝, 1 = 눈 바깥쪽 끝.
    /// </summary>
    private float HorizontalIrisRatio(Vector3 iris, Vector3 innerCorner, Vector3 outerCorner)
    {
        float eyeWidth = Vector3.Distance(innerCorner, outerCorner);
        if (eyeWidth <= 0f) return 0.5f;
        float d = Vector3.Distance(iris, innerCorner);
        return d / eyeWidth;
    }

    /// <summary>
    /// 한쪽 눈의 홍채(눈동자) 상하 위치 비율. 0 = 윗눈꺼풀, 1 = 아랫눈꺼풀.
    /// </summary>
    private float VerticalIrisRatio(Vector3 iris, Vector3 upperLid, Vector3 lowerLid)
    {
        float eyeHeight = Vector3.Distance(upperLid, lowerLid);
        if (eyeHeight <= 0f) return 0.5f;
        float d = Vector3.Distance(iris, upperLid);
        return d / eyeHeight;
    }

    private float GetRawGazeHorizontalRatio(List<Vector3> landmarks)
    {
        float leftRatio = HorizontalIrisRatio(landmarks[LEFT_IRIS_CENTER], landmarks[LEFT_EYE_INNER], landmarks[LEFT_EYE_OUTER]);
        float rightRatio = HorizontalIrisRatio(landmarks[RIGHT_IRIS_CENTER], landmarks[RIGHT_EYE_INNER], landmarks[RIGHT_EYE_OUTER]);
        return (leftRatio + rightRatio) / 2f;
    }

    private float GetRawGazeVerticalRatio(List<Vector3> landmarks)
    {
        float leftRatio = VerticalIrisRatio(landmarks[LEFT_IRIS_CENTER], landmarks[LEFT_EYE_UPPER], landmarks[LEFT_EYE_LOWER]);
        float rightRatio = VerticalIrisRatio(landmarks[RIGHT_IRIS_CENTER], landmarks[RIGHT_EYE_UPPER], landmarks[RIGHT_EYE_LOWER]);
        return (leftRatio + rightRatio) / 2f;
    }

    private bool IsFaceTooAngled(float yawRatio, float pitchRatio, out string reason)
    {
        if (yawRatio > maxYawRatio)
        {
            reason = $"좌우 회전 {yawRatio:F2} (허용 {maxYawRatio:F2} 초과)";
            return true;
        }

        if (pitchRatio > maxPitchRatio)
        {
            reason = $"상하 회전 {pitchRatio:F2} (허용 {maxPitchRatio:F2} 초과)";
            return true;
        }

        reason = "";
        return false;
    }

    // ─────────────────────────────────────────────────────────
    // 영역별 점수(0~5) 계산
    // ─────────────────────────────────────────────────────────
    private float CalculateSmile(List<Vector3> landmarks)
    {
        float ratio = GetRawSmileRatio(landmarks);
        float score = (ratio - neutralSmileRatio) * 24f; // 완화: 35 → 24
        return Mathf.Clamp(score, 0f, 5f);
    }

    private float CalculateSurprise(List<Vector3> landmarks)
    {
        float ratio = GetRawSurpriseRatio(landmarks);
        float score = (ratio - neutralSurpriseRatio) * 35f; // 완화: 50 → 35
        return Mathf.Clamp(score, 0f, 5f);
    }

    private float CalculateAngry(List<Vector3> landmarks)
    {
        float ratio = GetRawAngryRatio(landmarks);
        float score = (neutralAngryRatio - ratio) * 42f; // 완화: 60 → 42
        return Mathf.Clamp(score, 0f, 5f);
    }

    /// <summary>
    /// 눈동자가 정면(캘리브레이션된 중앙 위치)에서 얼마나 벗어났는지를 0~5점으로 환산합니다.
    /// 5점 = 정면 응시, 0점 = 시선이 크게 이탈.
    /// </summary>
    private float CalculateGazeScore(List<Vector3> landmarks)
    {
        float h = GetRawGazeHorizontalRatio(landmarks);
        float v = GetRawGazeVerticalRatio(landmarks);

        float deviation = Mathf.Abs(h - neutralGazeHorizontalRatio) + Mathf.Abs(v - neutralGazeVerticalRatio);
        float score = 5f - deviation * gazeSensitivity;
        return Mathf.Clamp(score, 0f, 5f);
    }

    /// <summary>
    /// 얼굴 각도(Yaw/Pitch)가 허용 한계 대비 얼마나 정면에 가까운지를 0~5점으로 환산합니다.
    /// 5점 = 완전 정면, 0점 = 허용 한계(maxYawRatio/maxPitchRatio) 도달.
    /// </summary>
    private float CalculateAngleScore(float yawRatio, float pitchRatio)
    {
        float normalizedYaw = maxYawRatio > 0f ? Mathf.Clamp01(yawRatio / maxYawRatio) : 0f;
        float normalizedPitch = maxPitchRatio > 0f ? Mathf.Clamp01(pitchRatio / maxPitchRatio) : 0f;
        float combined = (normalizedYaw + normalizedPitch) / 2f;
        float score = 5f * (1f - combined);
        return Mathf.Clamp(score, 0f, 5f);
    }

    // ─────────────────────────────────────────────────────────
    // 영역별 평가 결과 / 개선사항 생성
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// 표정(미소/놀람/찡그림 조합) 평가. 기존 로직을 그대로 사용합니다.
    /// </summary>
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
        float normalizedScore = (totalScore - normMin) / (normMax - normMin) * 5f;
        normalizedScore = Mathf.Clamp(normalizedScore, 0f, 5f);

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

        var improvementNotes = new StringBuilder();

        if (angry >= 2.5f)
            improvementNotes.AppendLine("- 미간/눈썹에 긴장이 감지됩니다. 질문을 들을 때 표정을 편하게 풀어보세요.");
        if (surprise >= 3f)
            improvementNotes.AppendLine("- 예상 밖 반응이 자주 감지됩니다. 답변 전 잠깐의 여유를 가져보세요.");
        if (smile < 0.5f)
            improvementNotes.AppendLine("- 표정이 다소 경직되어 있습니다. 자연스러운 미소를 시도해보세요.");
        if (smile > 4f)
            improvementNotes.AppendLine("- 미소가 다소 과도하게 유지되고 있습니다. 상황에 맞는 톤 조절이 필요할 수 있습니다.");

        return (grade, summary, detail, improvementNotes.ToString(), normalizedScore);
    }

    /// <summary>
    /// 시선(눈동자가 정면을 향하고 있는지) 평가.
    /// </summary>
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

        var notes = new StringBuilder();
        if (gazeScore < 3.5f)
            notes.AppendLine("- 시선이 카메라에서 벗어나는 경우가 있습니다. 답변 중에도 카메라 렌즈를 바라보는 연습을 해보세요.");
        if (gazeScore < 2.0f)
            notes.AppendLine("- 생각을 정리할 때 시선을 위/아래로 피하기보다, 잠깐 멈춘 뒤 카메라를 다시 응시하는 습관을 들여보세요.");

        return (summary, detail, notes.ToString());
    }

    /// <summary>
    /// 얼굴 각도(고개가 얼마나 돌아가 있는지) 평가.
    /// </summary>
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

        var notes = new StringBuilder();
        if (angleScore < 3.5f)
            notes.AppendLine("- 고개 방향이 흔들립니다. 카메라를 정면으로 응시하도록 자세를 교정해보세요.");
        if (angleScore < 2.0f)
            notes.AppendLine("- 답변 중 고개가 자주 돌아갑니다. 모니터나 카메라 위치를 눈높이에 맞추면 자연스럽게 정면을 유지하기 쉽습니다.");

        return (summary, detail, notes.ToString());
    }

    // -----------------------------------------------
    // 면접 종료 이벤트 수신 시 자동 호출
    // OnInterviewEndRequested 이벤트 구독 함수
    // GeminiManager의 비동기 평가 요청보다 먼저 동기로 실행됨
    // → 씬 전환 전 DB 저장 보장
    // -----------------------------------------------
    private void HandleInterviewEnded(HJS.InterviewResultData resultData)
    {
        Debug.Log("[fass] 면접 종료 감지 → 최종 태도 점수 계산 시작");
        CalculateFinalScoreAndSave();
    }

    // -----------------------------------------------
    // 면접 종료 시 최종 태도 점수 계산 + DB 저장
    // 기존 OnFaceLandmarksDetected()는 3분 간격으로만 저장하기 때문에
    // 면접 종료 시점에 즉시 계산하는 별도 메서드가 필요함
    // 현재까지 수집된 버퍼 데이터를 기반으로 최종 점수 산출
    // -----------------------------------------------
    public void CalculateFinalScoreAndSave()
    {
        // 버퍼에 데이터가 없으면 측정이 안 된 것
        // → 저장 스킵 (빈 데이터 DB 저장 방지)
        if (smileBuffer.Count == 0 && angleScoreBuffer.Count == 0)
        {
            Debug.LogWarning("[fass] 측정 데이터 없음 → 카메라 미연결 메시지 저장");

            // 카메라 미연결 또는 얼굴 미감지 시
            // 태도 점수 0점 + 안내 메시지 DB 저장
            // → Result 씬에서 사용자가 확인 가능
            SaveToDatabase(
                score: 0,
                adviceText: "면접 시 카메라를 연결하고 얼굴이 화면에 잘 보이도록 위치를 조정해주세요.",
                summaryText: "카메라 미연결 또는 얼굴이 감지되지 않아 태도 점수를 측정할 수 없습니다."
            );
            return;
        }

        // 현재까지 수집된 버퍼의 평균값으로 최종 점수 계산
        float smileScore = Average(smileBuffer);
        float surpriseScore = Average(surpriseBuffer);
        float angryScore = Average(angryBuffer);

        // 홍채 미검출 시 시선 점수는 기본값 5점 처리
        float gazeScoreAvg = gazeBuffer.Count > 0 ? Average(gazeBuffer) : 5f;
        float angleScoreAvg = Average(angleScoreBuffer);
        float avgYaw = Average(yawRatioBuffer);
        float avgPitch = Average(pitchRatioBuffer);

        // 3개 영역 평가 실행
        // 표정: 미소/놀람/찡그림 조합으로 판단
        // 시선: 눈동자가 카메라 정면을 얼마나 응시했는지
        // 각도: 고개가 얼마나 정면을 유지했는지
        var (faceGrade, faceSummary, faceDetail, faceNotes, faceScore) =
            EvaluateFaceExpression(smileScore, surpriseScore, angryScore);
        var (gazeSummary, gazeDetail, gazeNotes) =
            EvaluateGaze(gazeScoreAvg);
        var (angleSummary, angleDetail, angleNotes) =
            EvaluateAngle(angleScoreAvg, avgYaw, avgPitch);

        // 소수 첫째자리로 반올림
        float faceScoreRounded = Round1(faceScore);
        float gazeScoreRounded = Round1(gazeScoreAvg);
        float angleScoreRounded = Round1(angleScoreAvg);

        // 태도 총점 = 3개 영역 평균 후 정수로 반올림 (각 0~5점)
        int attitudeScore = Mathf.Clamp(
            Mathf.RoundToInt(
                (faceScoreRounded + gazeScoreRounded + angleScoreRounded) / 3f),
            0, 5);

        // Inspector에서 실시간 확인 가능하도록 퍼블릭 필드에 반영
        latestAttitudeScore = (float)attitudeScore;
        latestGrade = faceGrade;
        latestEvaluationScore = (float)attitudeScore;

        // 결과 문자열 조합
        string combinedSummary = $"[표정] {faceSummary} / [시선] {gazeSummary} / [얼굴각도] {angleSummary}";
        string combinedDetail = $"표정: {faceDetail}\n시선: {gazeDetail}\n얼굴각도: {angleDetail}";
        string combinedNotes = faceNotes + gazeNotes + angleNotes;

        latestEvaluationSummary = combinedSummary;
        latestEvaluationDetail = combinedDetail;
        latestImprovementNotes = combinedNotes;

        Debug.Log($"[fass] 최종 태도 점수: {attitudeScore}/5");

        // InterviewDbManager를 통해 DB에 최종 저장
        // sessionId: -1 → 현재 활성 세션 자동 반영
        SaveToDatabase((float)attitudeScore, combinedNotes, combinedSummary);
    }
}