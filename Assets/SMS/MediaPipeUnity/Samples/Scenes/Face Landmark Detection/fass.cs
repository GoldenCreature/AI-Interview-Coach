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

    [Header("얼굴 각도 제한 (Head Pose Gate) - 수동 입력")]
    [Tooltip("체크하면 얼굴이 특정 각도 이상 돌아갔을 때 표정/시선 분석(점수 계산)을 건너뜁니다. 각도 점수 자체는 게이트와 무관하게 계속 측정됩니다.")]
    public bool enableAngleGate = true;

    [Tooltip("좌우 회전(Yaw) 허용 한계. 이 값에 가까워질수록 얼굴 각도 점수가 0점에 가까워집니다.")]
    public float maxYawRatio = 0.80f;

    [Tooltip("상하 회전(Pitch) 허용 한계. 이 값에 가까워질수록 얼굴 각도 점수가 0점에 가까워집니다.")]
    public float maxPitchRatio = 0.80f;

    [Tooltip("각도 초과로 표정/시선 분석이 멈춘 상태인지")]
    public bool isFaceTooAngled = false;

    // ─────────────────────────────────────────────────────────
    // 누적 통계 (면접 시작 ~ 종료 전체 구간)
    // 이동평균(30프레임 버퍼)은 사용하지 않고, 프레임마다 합계/개수를 누적해 평균을 냅니다.
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
    /// </summary>
    public List<EvaluationArea> GetAttitudeAreas()
    {
        return new List<EvaluationArea> { latestFaceExpressionArea, latestGazeArea, latestAngleArea };
    }

    void OnEnable()
    {
        // 새 면접 시작 시 이전 데이터가 섞이지 않도록 초기화
        ResetStatistics();

        if (runner != null)
            runner.OnResultOutput += HandleResult;
        else
            Debug.LogWarning("[fass] runner가 인스펙터에 연결되지 않았습니다.");

        // InterviewManager의 면접 종료 요청 이벤트 구독
        // 면접이 끝나면 HandleInterviewEnded()가 자동으로 실행됨
        HJS.InterviewManager.OnInterviewEndRequested += HandleInterviewEnded;
    }

    void Start()
    {
        // 메인 스레드(Start)에서 한 번만 안전하게 찾아 캐싱합니다.
        dbManager = InterviewDbManager.Instance;
        if (dbManager == null)
            Debug.LogWarning("[fass] InterviewDbManager를 씬에서 찾지 못했습니다. DB 저장이 스킵됩니다.");
    }

    void OnDisable()
    {
        if (runner != null)
            runner.OnResultOutput -= HandleResult;

        // 씬 전환 시 오브젝트 파괴 전 구독 해제
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

    public void OnFaceLandmarksDetected(List<Vector3> landmarks)
    {
        if (landmarks == null || landmarks.Count < FACE_LANDMARK_COUNT)
        {
            return;
        }

        // ── 1) 얼굴 각도(Yaw/Pitch)는 게이트 여부와 무관하게 항상 측정합니다.
        float yawRatio = GetRawYawRatio(landmarks);
        float pitchRatio = GetRawPitchRatio(landmarks);
        float frameAngleScore = CalculateAngleScore(yawRatio, pitchRatio);

        bool tooAngled = enableAngleGate && IsFaceTooAngled(yawRatio, pitchRatio, out _);
        isFaceTooAngled = tooAngled;

        // ── 2) 프레임 값을 누적합니다. 각도가 심하면 표정/시선은 신뢰도가 낮으므로 건너뜁니다.
        bool needIrisWarning = false;

        lock (statsLock)
        {
            yawAcc.Add(yawRatio);
            pitchAcc.Add(pitchRatio);
            angleAcc.Add(frameAngleScore);

            if (!tooAngled)
            {
                smileAcc.Add(CalculateSmile(landmarks));
                surpriseAcc.Add(CalculateSurprise(landmarks));
                angryAcc.Add(CalculateAngry(landmarks));

                if (landmarks.Count >= IRIS_LANDMARK_COUNT)
                {
                    gazeAcc.Add(CalculateGazeScore(landmarks));
                }
                else if (!warnedNoIris)
                {
                    warnedNoIris = true;
                    needIrisWarning = true;
                }
            }
        }

        if (needIrisWarning)
        {
            Debug.LogWarning("[fass] 홍채(iris) 랜드마크가 감지되지 않았습니다. 478개 랜드마크를 출력하는 모델이 로드됐는지 확인하세요.");
        }

        // ── 3) Inspector 실시간 표시용 업데이트 (DB 저장 없음, 개발자 확인용)
        // 최종 계산 및 DB 저장은 면접 종료 시 CalculateFinalScoreAndSave()에서 처리
        Snapshot snap = TakeSnapshot();
        if (!snap.hasFace && !snap.hasAngle) return;

        EvaluationResult eval = EvaluateAll(snap);

        latestFaceExpressionArea = eval.faceArea;
        latestGazeArea = eval.gazeArea;
        latestAngleArea = eval.angleArea;

        latestAttitudeScore = eval.attitudeScore;
        latestGrade = eval.grade;
        latestEvaluationScore = latestAttitudeScore;

        latestEvaluationSummary = eval.summary;
        latestEvaluationDetail = eval.detail;
        latestImprovementNotes = eval.notes;
    }

    // ─────────────────────────────────────────────────────────
    // 스냅샷 / 종합 평가
    // ─────────────────────────────────────────────────────────
    private struct Snapshot
    {
        public float smile, surprise, angry, gaze, angle, yaw, pitch;
        public bool hasFace;   // 표정 데이터가 하나라도 있는지 (각도 게이트에 계속 걸리면 false)
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
                hasFace = smileAcc.count > 0,
                hasAngle = angleAcc.count > 0
            };
        }
    }

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
            // 각도 게이트 때문에 표정이 한 프레임도 측정되지 않은 경우: 0점이 아니라 "측정 불가"로 처리
            result.grade = ExpressionGrade.Average;
            faceSummary = "측정 불가";
            faceDetail = "얼굴이 정면에서 크게 벗어나 표정을 측정하지 못했습니다.";
            faceNotes = "";
            faceScore = 0f;
        }

        // 시선 / 각도
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

        // 태도 점수: 표정 / 시선 / 얼굴각도 3개 영역 점수의 평균 (표정은 항상 포함)
        // 표정이 측정 불가였던 경우 faceRounded는 0점으로 들어갑니다.
        result.attitudeScore = RoundScore((faceRounded + gazeRounded + angleRounded) / 3f);

        result.summary = $"[표정] {faceSummary} / [시선] {gazeSummary} / [얼굴각도] {angleSummary}";
        result.detail = $"표정: {faceDetail}\n시선: {gazeDetail}\n얼굴각도: {angleDetail}";
        result.notes = FormatNotesByArea(faceNotes, gazeNotes, angleNotes);

        return result;
    }

    /// <summary>
    /// InterviewDbManager에 태도 점수 및 피드백 전송
    /// (Start()에서 캐싱해둔 dbManager를 사용)
    /// </summary>
    private void SaveToDatabase(int score, string adviceText, string summaryText)
    {
        if (dbManager == null)
        {
            Debug.LogWarning("[fass] dbManager 캐싱 실패로 DB 저장을 건너뜁니다.");
            return;
        }

        bool success = dbManager.SaveFaceEvaluation(
            sessionId: dbManager.CurrentSessionId,
            scoreAttitude: score,
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

    /// <summary>
    /// 0~5 범위로 제한한 뒤 정수로 반올림합니다. (2.5 → 3, 3.5 → 4처럼 항상 반올림)
    /// </summary>
    private int RoundScore(float v)
    {
        return Mathf.Clamp(Mathf.FloorToInt(v + 0.5f), 0, 5);
    }

    /// <summary>
    /// 개선사항(notes)을 EvaluationDetail과 동일한 형식(영역별 라벨 + 줄바꿈)으로 합칩니다.
    /// 해당 영역에 특이사항이 없으면 부드러운 유지 문구로 표기합니다.
    /// </summary>
    private string FormatNotesByArea(string faceNotes, string gazeNotes, string angleNotes)
    {
        string Format(string label, string notes)
        {
            string trimmed = notes?.Trim();
            return string.IsNullOrEmpty(trimmed) ? $"{label}: 현재 상태가 좋으니, 지금의 느낌을 최대한 유지해주세요." : $"{label}: {trimmed}";
        }

        return $"{Format("표정", faceNotes)}\n{Format("시선", gazeNotes)}\n{Format("얼굴각도", angleNotes)}";
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
        float score = (ratio - neutralSmileRatio) * 24f;
        return Mathf.Clamp(score, 0f, 5f);
    }

    private float CalculateSurprise(List<Vector3> landmarks)
    {
        float ratio = GetRawSurpriseRatio(landmarks);
        float score = (ratio - neutralSurpriseRatio) * 35f;
        return Mathf.Clamp(score, 0f, 5f);
    }

    private float CalculateAngry(List<Vector3> landmarks)
    {
        float ratio = GetRawAngryRatio(landmarks);
        float score = (neutralAngryRatio - ratio) * 42f;
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
    /// 표정(미소/놀람/찡그림 조합) 평가.
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
    // 면접 시작부터 종료까지 누적된 전체 구간의 평균으로 최종 점수를 산출
    // -----------------------------------------------
    public void CalculateFinalScoreAndSave()
    {
        Snapshot snap = TakeSnapshot();

        // 측정 데이터가 전혀 없으면 카메라 미연결 또는 얼굴 미감지
        if (!snap.hasFace && !snap.hasAngle)
        {
            Debug.LogWarning("[fass] 측정 데이터 없음 → 카메라 미연결 메시지 저장");

            const string noDataSummary = "카메라 미연결 또는 얼굴이 감지되지 않아 태도 점수를 측정할 수 없습니다.";
            const string noDataAdvice = "면접 시 카메라를 연결하고 얼굴이 화면에 잘 보이도록 위치를 조정해주세요.";

            SaveToDatabase(score: 0, adviceText: noDataAdvice, summaryText: noDataSummary);

            if (csvLogger != null)
            {
                csvLogger.SaveScoreToCSV(DateTime.Now, 0f, noDataSummary, noDataAdvice);
            }
            return;
        }

        EvaluationResult eval = EvaluateAll(snap);

        // 태도 총점 = 표정/시선/얼굴각도 int 점수 평균을 반올림한 값 (0~5점)
        int attitudeScore = eval.attitudeScore;

        // Inspector에서 확인 가능하도록 퍼블릭 필드에 반영
        latestFaceExpressionArea = eval.faceArea;
        latestGazeArea = eval.gazeArea;
        latestAngleArea = eval.angleArea;
        latestAttitudeScore = attitudeScore;
        latestGrade = eval.grade;
        latestEvaluationScore = attitudeScore;
        latestEvaluationSummary = eval.summary;
        latestEvaluationDetail = eval.detail;
        latestImprovementNotes = eval.notes;

        Debug.Log($"[fass] 최종 태도 점수: {attitudeScore}/5 (표정 프레임 {snap.hasFace}, 누적 각도 프레임 {angleAcc.count})");

        SaveToDatabase(attitudeScore, eval.notes, eval.detail);

        // 개발자 확인용 CSV 기록 (인스펙터에 연결된 경우에만 동작)
        if (csvLogger != null)
        {
            csvLogger.SaveScoreToCSV(DateTime.Now, attitudeScore, eval.detail, eval.notes);
        }
    }
}