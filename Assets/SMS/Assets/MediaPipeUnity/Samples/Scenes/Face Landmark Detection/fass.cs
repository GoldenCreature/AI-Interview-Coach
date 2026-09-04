using System.Collections.Generic;
using Mediapipe.Tasks.Vision.FaceLandmarker;
using Mediapipe.Unity.Sample.FaceLandmarkDetection;
using UnityEngine;

public class fass : MonoBehaviour
{
    [Header("CSV Logger 연결")]
    [Tooltip("유니티 인스펙터 창에서 CustomPathCSVLogger 오브젝트를 여기에 드래그 앤 드롭 하세요.")]
    public CustomPathCSVLogger csvLogger;

    [Header("DB Logger 연결")]
    [Tooltip("유니티 인스펙터 창에서 FaceScoreDBLogger 오브젝트를 여기에 드래그 앤 드롭 하세요.")]
    public FaceScoreDBLogger dbLogger;

    [Header("MediaPipe Runner 연결")]
    [Tooltip("씬에 있는 FaceLandmarkerRunner 오브젝트를 여기에 드래그하세요.")]
    public FaceLandmarkerRunner runner;

    [Header("캘리브레이션 (무표정 기준값)")]
    [Tooltip("무표정 상태에서 측정한 입 높이/너비 비율. 실측 후 조정하세요.")]
    public float neutralSmileRatio = 0.03f;

    [Tooltip("무표정 상태에서 측정한 눈 뜬 정도/얼굴너비 비율. 실측 후 조정하세요.")]
    public float neutralSurpriseRatio = 0.05f;

    [Tooltip("무표정 상태에서 측정한 눈썹 사이 거리/얼굴너비 비율. 실측 후 조정하세요.")]
    public float neutralAngryRatio = 0.23f;

    [Header("자동 캘리브레이션")]
    [Tooltip("캘리브레이션에 사용할 시간(초). 이 시간 동안 무표정을 유지해야 합니다.")]
    public float calibrationDuration = 3f;

    [Tooltip("캘리브레이션 시작 키. Play 모드에서 이 키를 누르면 자동 측정이 시작됩니다.")]
    public KeyCode calibrationKey = KeyCode.Space;

    private bool isCalibrating = false;
    private List<Vector3> latestLandmarks = null;

    [Header("얼굴 각도 제한 (Head Pose Gate)")]
    [Tooltip("체크하면 얼굴이 특정 각도 이상 돌아갔을 때 분석(점수 계산/저장)을 건너뜁니다.")]
    public bool enableAngleGate = true;

    [Tooltip("좌우 회전(Yaw) 허용 한계. 양쪽 귀(234/454)의 z값 차이를 얼굴 너비로 나눈 비율입니다. " +
             "값이 작을수록 더 엄격(조금만 돌아가도 멈춤), 클수록 관대합니다. " +
             "직접 입력하거나 아래 각도 임계값 캘리브레이션으로 자동 측정할 수 있습니다.")]
    public float maxYawRatio = 0.80f;

    [Tooltip("상하 회전(Pitch) 허용 한계. 이마(10)/턱(152)의 z값 차이를 얼굴 높이로 나눈 비율입니다. " +
             "값이 작을수록 더 엄격, 클수록 관대합니다. " +
             "직접 입력하거나 아래 각도 임계값 캘리브레이션으로 자동 측정할 수 있습니다.")]
    public float maxPitchRatio = 0.80f;

    [Tooltip("각도 초과로 분석이 멈춘 상태인지 (UI 표시 등에서 참조 가능)")]
    public bool isFaceTooAngled = false;

    [Header("각도 임계값 자동 캘리브레이션")]
    [Tooltip("무표정 캘리브레이션 직후 자동으로 이어서 각도 임계값 캘리브레이션까지 진행합니다. " +
             "즉, calibrationKey(기본 Space) 한 번으로 '무표정 유지 → 한계각도로 고개 돌려 유지' 순서가 자동 진행됩니다.")]
    public KeyCode angleThresholdCalibrationKey = KeyCode.Tab;

    [Tooltip("각도 임계값 캘리브레이션에 사용할 시간(초). 이 시간 동안 원하는 한계 각도를 유지해주세요.")]
    public float angleThresholdCalibrationDuration = 3f;

    [Tooltip("무표정 캘리브레이션이 끝난 뒤, 한계 각도로 고개를 돌릴 시간을 주기 위한 대기시간(초).")]
    public float angleTransitionDelay = 2f;

    [Tooltip("자동 측정된 값에 곱하는 안전 여유율. 1.0이면 측정된 최대값 그대로, " +
             "0.9면 측정값보다 10% 더 엄격하게(살짝 여유를 두고) 설정합니다.")]
    [Range(0.5f, 1f)]
    public float angleThresholdSafetyMargin = 1f;

    private bool isCalibratingAngleThreshold = false;
    private bool isCalibratingSequence = false;

    [Header("노이즈 완화 (이동평균)")]
    [Tooltip("저장 시점에 사용할 최근 프레임 점수의 개수. 클수록 부드럽지만 반응이 느려짐.")]
    public int smoothingWindowSize = 30;

    // 매 프레임 계산된 점수를 담아두는 슬라이딩 윈도우
    private readonly Queue<float> smileBuffer = new Queue<float>();
    private readonly Queue<float> surpriseBuffer = new Queue<float>();
    private readonly Queue<float> angryBuffer = new Queue<float>();

    // 감정별 누적 통계 (저장이 일어날 때마다 누적됨)
    // 합계(Total) / 저장 횟수(Count) / 평균(Average = Total / Count)
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

    // 세션을 새로 시작할 때 누적 통계를 초기화하고 싶으면 외부(UI 버튼 등)에서 호출
    public void ResetStatistics()
    {
        smileTotal = 0f;
        surpriseTotal = 0f;
        angryTotal = 0f;
        smileCount = 0;
        surpriseCount = 0;
        angryCount = 0;
    }

    // ===================================================================
    // 면접용 표정 종합 평가 시스템
    // ===================================================================
    [Header("표정 평가 시스템")]
    [Tooltip("가장 최근 평가 등급 (UI 표시 등에서 참조 가능)")]
    public ExpressionGrade latestGrade;

    [Tooltip("가장 최근 평가 한줄 요약")]
    public string latestEvaluationSummary = "";

    [Tooltip("가장 최근 평가 상세 코멘트")]
    public string latestEvaluationDetail = "";

    [Tooltip("가장 최근 개선사항 (감정별 구체적인 코멘트, 총평과는 별도)")]
    public string latestImprovementNotes = "";

    [Tooltip("가장 최근 평가 점수 (0~5점 만점으로 정규화된 값)")]
    public float latestEvaluationScore = 0f;

    public enum ExpressionGrade
    {
        Excellent,  // 매우 안정적
        Good,       // 안정적
        Average,    // 보통
        NeedsWork,  // 긴장 감지
        Poor        // 불안정
    }
    // ===================================================================

    void OnEnable()
    {
        if (runner != null)
            runner.OnResultOutput += HandleResult;
        else
            Debug.LogWarning("[fass] runner가 인스펙터에 연결되지 않았습니다.");
    }

    void OnDisable()
    {
        if (runner != null)
            runner.OnResultOutput -= HandleResult;
    }

    void Update()
    {
        // 캘리브레이션 시작 키 입력 감지: 무표정 → (자동 전환) → 한계각도 순서로 이어서 진행됨
        if (Input.GetKeyDown(calibrationKey) && !isCalibratingSequence)
        {
            StartCoroutine(CalibrateSequence());
        }

        // 각도 임계값만 단독으로 다시 측정하고 싶을 때 (무표정 기준값은 그대로 두고 한계각도만 재조정)
        if (Input.GetKeyDown(angleThresholdCalibrationKey) && !isCalibratingAngleThreshold && !isCalibratingSequence)
        {
            StartCoroutine(CalibrateAngleThreshold());
        }
    }

    // FaceLandmarkerResult -> List<Vector3> 변환
    private void HandleResult(FaceLandmarkerResult result)
    {
        if (result.faceLandmarks == null || result.faceLandmarks.Count == 0)
        {
            return; // 얼굴 미감지 시 조용히 무시 (로그 남기고 싶으면 여기에 Debug.Log 추가)
        }

        var faceLandmarks = result.faceLandmarks[0]; // 첫 번째 얼굴만 사용
        var landmarksList = new List<Vector3>(faceLandmarks.landmarks.Count);

        foreach (var lm in faceLandmarks.landmarks)
        {
            landmarksList.Add(new Vector3(lm.x, lm.y, lm.z));
        }

        OnFaceLandmarksDetected(landmarksList);
    }

    [Header("저장 간격")]
    [Tooltip("점수를 저장할 최소 간격(분 단위)")]
    public float logIntervalMinutes = 3f;

    private System.DateTime lastLoggedTime = System.DateTime.MinValue;

    public void OnFaceLandmarksDetected(List<Vector3> landmarks)
    {
        latestLandmarks = landmarks; // 캘리브레이션 코루틴이 최신 랜드마크를 참조할 수 있도록 캐싱

        Debug.Log("[fass] 1단계 성공: 미디어파이프로부터 얼굴 좌표를 전달받았습니다!");

        if (landmarks == null)
        {
            Debug.LogWarning("[fass] 에러: landmarks 데이터가 null(비어있음)입니다.");
            return;
        }

        if (landmarks.Count < 468)
        {
            Debug.LogWarning($"[fass] 에러: 인식된 랜드마크 개수가 부족합니다. (현재 개수: {landmarks.Count}/최소 요구: 468)");
            return;
        }

        // 얼굴 각도 게이트: 옆으로 너무 돌아가거나(Yaw) 위아래로 너무 숙여지면(Pitch)
        // 랜드마크 왜곡으로 점수가 부정확해지므로 이번 프레임 분석 자체를 건너뜀.
        if (enableAngleGate && IsFaceTooAngled(landmarks, out string angleReason))
        {
            isFaceTooAngled = true;
            Debug.Log($"[fass] 분석 스킵: 얼굴 각도가 허용 범위를 벗어났습니다 ({angleReason}). 정면을 바라봐주세요.");
            return;
        }
        isFaceTooAngled = false;

        // 프레임 노이즈 완화: 매 프레임 점수를 계산해서 슬라이딩 윈도우에 쌓아둠.
        // 이렇게 하면 저장 시점에 하필 랜드마크가 튄 프레임 하나만 캡처하는 대신
        // 최근 N프레임의 평균값을 사용할 수 있음.
        float frameSmile = CalculateSmile(landmarks);
        float frameSurprise = CalculateSurprise(landmarks);
        float frameAngry = CalculateAngry(landmarks);

        PushToBuffer(smileBuffer, frameSmile);
        PushToBuffer(surpriseBuffer, frameSurprise);
        PushToBuffer(angryBuffer, frameAngry);

        // 저장 간격 체크: 아직 간격이 안 지났으면 버퍼만 쌓고 저장은 스킵
        System.DateTime now = System.DateTime.Now;
        if ((now - lastLoggedTime).TotalMinutes < logIntervalMinutes)
        {
            return;
        }
        lastLoggedTime = now;

        // 저장 시점에는 단일 프레임 값이 아니라 최근 윈도우의 평균값을 사용
        // (감정별 개별 점수는 여전히 종합 평가 계산에는 쓰이지만, 저장 자체는 하지 않음)
        float smileScore = Average(smileBuffer);
        float surpriseScore = Average(surpriseBuffer);
        float angryScore = Average(angryBuffer);

        // 감정별 누적 합계/평균 갱신
        smileTotal += smileScore;
        surpriseTotal += surpriseScore;
        angryTotal += angryScore;
        smileCount++;
        surpriseCount++;
        angryCount++;

        Debug.Log($"[fass] 2단계 성공: 점수 계산 완료 (미소:{smileScore:F1}, 놀람:{surpriseScore:F1}, 분노:{angryScore:F1}, 샘플수:{smileBuffer.Count})");

        // 감정별 합계/평균을 각각 따로 출력 (디버그용, 저장 대상 아님)
        Debug.Log($"[fass][기쁨] 이번 구간 평균:{smileScore:F2} | 누적 합계:{smileTotal:F1} | 누적 평균:{SmileAverage:F2} (총 {smileCount}회 기록)");
        Debug.Log($"[fass][놀람] 이번 구간 평균:{surpriseScore:F2} | 누적 합계:{surpriseTotal:F1} | 누적 평균:{SurpriseAverage:F2} (총 {surpriseCount}회 기록)");
        Debug.Log($"[fass][분노] 이번 구간 평균:{angryScore:F2} | 누적 합계:{angryTotal:F1} | 누적 평균:{AngryAverage:F2} (총 {angryCount}회 기록)");

        // ===================================================================
        // 면접용 종합 평가 실행
        // ===================================================================
        var (grade, summary, detail, improvementNotes, normalizedScore) = EvaluateExpression(smileScore, surpriseScore, angryScore);
        latestGrade = grade;
        latestEvaluationSummary = summary;
        latestEvaluationDetail = detail;
        latestImprovementNotes = improvementNotes;
        latestEvaluationScore = normalizedScore;

        Debug.Log($"[fass][평가] 종합 평가: {summary} ({normalizedScore:F1}/5.0)\n{detail}\n{improvementNotes}");
        // ===================================================================

        // [수정] 저장 시에는 미소/놀람/분노 개별 점수와 평가등급(summary)을 제거하고,
        // 종합 점수(normalizedScore) + 총평(detail) + 개선사항(improvementNotes) + 시간(now)을 전달.
        if (csvLogger != null)
        {
            csvLogger.SaveScoreToCSV(now, normalizedScore, detail, improvementNotes);
            Debug.Log("[fass] 3단계 성공: CSV Logger에 데이터 저장을 전송했습니다!");
        }
        else
        {
            Debug.LogError("[fass] 에러: csvLogger 컴포넌트가 인스펙터에 연결되지 않았습니다.");
        }

        if (dbLogger != null)
        {
            dbLogger.SaveScoreToDB(now, normalizedScore, detail, improvementNotes);
            Debug.Log("[fass] 4단계 성공: DB Logger에 데이터 저장을 전송했습니다!");
        }
        else
        {
            Debug.LogError("[fass] 에러: dbLogger 컴포넌트가 인스펙터에 연결되지 않았습니다.");
        }
    }

    // 버퍼에 새 값을 넣고, 윈도우 크기를 넘으면 가장 오래된 값을 제거
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
        foreach (float v in buffer)
        {
            sum += v;
        }
        return sum / buffer.Count;
    }

    // ===== Raw ratio 계산 (캘리브레이션과 점수 계산 양쪽에서 재사용) =====

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

    // ===== 얼굴 각도(Head Pose) 판정 =====
    // MediaPipe 랜드마크의 z값은 카메라 방향 깊이를 나타내므로,
    // 좌우 대칭점(양쪽 귀) 또는 상하 대칭점(이마/턱)의 z 차이가 클수록
    // 얼굴이 카메라 정면이 아니라 옆/위아래로 돌아가 있다는 뜻.

    // Yaw(좌우 회전) 비율만 단독으로 계산 (캘리브레이션에서 재사용)
    private float GetRawYawRatio(List<Vector3> landmarks)
    {
        float faceWidth = Vector3.Distance(landmarks[234], landmarks[454]);
        float leftEarZ = landmarks[234].z;
        float rightEarZ = landmarks[454].z;
        return Mathf.Abs(leftEarZ - rightEarZ) / (faceWidth > 0 ? faceWidth : 1f);
    }

    // Pitch(상하 회전) 비율만 단독으로 계산 (캘리브레이션에서 재사용)
    private float GetRawPitchRatio(List<Vector3> landmarks)
    {
        float faceHeight = Vector3.Distance(landmarks[10], landmarks[152]);
        float foreheadZ = landmarks[10].z;
        float chinZ = landmarks[152].z;
        return Mathf.Abs(foreheadZ - chinZ) / (faceHeight > 0 ? faceHeight : 1f);
    }

    private bool IsFaceTooAngled(List<Vector3> landmarks, out string reason)
    {
        float yawRatio = GetRawYawRatio(landmarks);
        float pitchRatio = GetRawPitchRatio(landmarks);

        if (yawRatio > maxYawRatio)
        {
            reason = $"좌우 회전 {yawRatio:F2} (허용 한계 {maxYawRatio:F2} 초과)";
            return true;
        }

        if (pitchRatio > maxPitchRatio)
        {
            reason = $"상하 회전 {pitchRatio:F2} (허용 한계 {maxPitchRatio:F2} 초과)";
            return true;
        }

        reason = "";
        return false;
    }

    // ===== 점수 계산 (raw ratio를 baseline과 비교 후 증폭) =====

    private float CalculateSmile(List<Vector3> landmarks)
    {
        float ratio = GetRawSmileRatio(landmarks);
        float score = (ratio - neutralSmileRatio) * 35f;
        return Mathf.Clamp(score, 0f, 5f);
    }

    private float CalculateSurprise(List<Vector3> landmarks)
    {
        float ratio = GetRawSurpriseRatio(landmarks);
        float score = (ratio - neutralSurpriseRatio) * 50f;
        return Mathf.Clamp(score, 0f, 5f);
    }

    private float CalculateAngry(List<Vector3> landmarks)
    {
        float ratio = GetRawAngryRatio(landmarks);
        float score = (neutralAngryRatio - ratio) * 60f;
        return Mathf.Clamp(score, 0f, 5f);
    }

    // ===================================================================
    // 종합 평가 (면접 상황 기준)
    // ===================================================================
    private (ExpressionGrade grade, string summary, string detail, string improvementNotes, float normalizedScore) EvaluateExpression(
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
            detail = "침착하고 신뢰감 있는 표정을 유지하고 있습니다. 면접관에게 좋은 인상을 줄 가능성이 높습니다.";
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
            detail = "긴장하거나 동요하는 표정이 감지되었습니다. 심호흡을 하고 속도를 조절해보세요.";
        }
        else
        {
            grade = ExpressionGrade.Poor;
            summary = "불안정";
            detail = "면접 태도에 부정적으로 작용할 수 있는 표정 변화가 감지되었습니다.";
        }

        // 개별 감정별 구체적인 개선사항 (총평과는 별도로 모아서 반환)
        var improvementNotes = new System.Text.StringBuilder();

        if (angry >= 2.5f)
            improvementNotes.AppendLine($"- 미간/눈썹에 긴장이 감지됩니다 ({angry:F1}/5). 질문을 들을 때 표정을 편하게 풀어보세요.");
        if (surprise >= 3f)
            improvementNotes.AppendLine($"- 예상 밖 반응이 자주 감지됩니다 ({surprise:F1}/5). 답변 전 잠깐의 여유를 가져보세요.");
        if (smile < 0.5f)
            improvementNotes.AppendLine($"- 표정이 다소 경직되어 있습니다 ({smile:F1}/5). 자연스러운 미소를 시도해보세요.");
        if (smile > 4f)
            improvementNotes.AppendLine($"- 미소가 다소 과도하게 유지되고 있습니다 ({smile:F1}/5). 상황에 맞는 톤 조절이 필요할 수 있습니다.");

        return (grade, summary, detail, improvementNotes.ToString(), normalizedScore);
    }
    // ===================================================================

    // ===== 자동 캘리브레이션 =====

    private System.Collections.IEnumerator CalibrateNeutralRatios()
    {
        isCalibrating = true;
        Debug.Log($"[fass] 캘리브레이션 시작! {calibrationDuration}초 동안 무표정을 유지해주세요...");

        float smileSum = 0f;
        float surpriseSum = 0f;
        float angrySum = 0f;
        int sampleCount = 0;
        float elapsed = 0f;

        while (elapsed < calibrationDuration)
        {
            if (latestLandmarks != null && latestLandmarks.Count >= 468
                && !(enableAngleGate && IsFaceTooAngled(latestLandmarks, out _)))
            {
                smileSum += GetRawSmileRatio(latestLandmarks);
                surpriseSum += GetRawSurpriseRatio(latestLandmarks);
                angrySum += GetRawAngryRatio(latestLandmarks);
                sampleCount++;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (sampleCount > 0)
        {
            neutralSmileRatio = smileSum / sampleCount;
            neutralSurpriseRatio = surpriseSum / sampleCount;
            neutralAngryRatio = angrySum / sampleCount;

            Debug.Log($"[fass] 캘리브레이션 완료! (샘플 {sampleCount}개)\n" +
                       $"neutralSmileRatio = {neutralSmileRatio:F4}\n" +
                       $"neutralSurpriseRatio = {neutralSurpriseRatio:F4}\n" +
                       $"neutralAngryRatio = {neutralAngryRatio:F4}");
        }
        else
        {
            Debug.LogWarning("[fass] 캘리브레이션 실패: 유효한 랜드마크 샘플을 얻지 못했습니다. 얼굴이 카메라에 잘 잡히는지 확인하세요.");
        }

        isCalibrating = false;
    }

    // Play 모드에서 angleThresholdCalibrationKey(기본 Tab)를 누른 채로
    // "여기까지는 허용하고 싶다"는 가장 심한 각도를 angleThresholdCalibrationDuration(기본 3초) 동안
    // 유지하면, 그 구간에서 관측된 최대 Yaw/Pitch 비율을 그대로 임계값으로 반영함.
    private System.Collections.IEnumerator CalibrateAngleThreshold()
    {
        isCalibratingAngleThreshold = true;
        Debug.Log($"[fass] 각도 임계값 캘리브레이션 시작! {angleThresholdCalibrationDuration}초 동안 " +
                  "허용하고 싶은 가장 심한 각도로 고개를 유지해주세요...");

        float maxObservedYaw = 0f;
        float maxObservedPitch = 0f;
        int sampleCount = 0;
        float elapsed = 0f;

        while (elapsed < angleThresholdCalibrationDuration)
        {
            if (latestLandmarks != null && latestLandmarks.Count >= 468)
            {
                float yaw = GetRawYawRatio(latestLandmarks);
                float pitch = GetRawPitchRatio(latestLandmarks);

                if (yaw > maxObservedYaw) maxObservedYaw = yaw;
                if (pitch > maxObservedPitch) maxObservedPitch = pitch;

                sampleCount++;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (sampleCount > 0)
        {
            maxYawRatio = maxObservedYaw * angleThresholdSafetyMargin;
            maxPitchRatio = maxObservedPitch * angleThresholdSafetyMargin;

            Debug.Log($"[fass] 각도 임계값 캘리브레이션 완료! (샘플 {sampleCount}개)\n" +
                       $"관측된 최대 Yaw={maxObservedYaw:F3}, Pitch={maxObservedPitch:F3}\n" +
                       $"안전 여유율 {angleThresholdSafetyMargin:F2} 적용 후 → " +
                       $"maxYawRatio={maxYawRatio:F3}, maxPitchRatio={maxPitchRatio:F3}");
        }
        else
        {
            Debug.LogWarning("[fass] 각도 임계값 캘리브레이션 실패: 유효한 랜드마크 샘플을 얻지 못했습니다. 얼굴이 카메라에 잘 잡히는지 확인하세요.");
        }

        isCalibratingAngleThreshold = false;
    }

    // calibrationKey(기본 Space) 한 번으로 무표정 캘리브레이션과 한계각도 캘리브레이션을
    // 순서대로 자동 진행함: 1) 무표정 유지(calibrationDuration) → 2) 전환 대기(angleTransitionDelay,
    // 이 시간 동안 한계각도로 고개를 돌리면 됨) → 3) 한계각도 유지(angleThresholdCalibrationDuration)
    private System.Collections.IEnumerator CalibrateSequence()
    {
        isCalibratingSequence = true;

        Debug.Log("[fass] 통합 캘리브레이션 시작! 1단계: 무표정 기준값을 측정합니다.");
        yield return StartCoroutine(CalibrateNeutralRatios());

        Debug.Log($"[fass] 2단계 준비: {angleTransitionDelay}초 안에 허용하고 싶은 " +
                  "가장 심한 각도로 고개를 돌려 그 자세를 유지해주세요.");
        yield return new WaitForSeconds(angleTransitionDelay);

        Debug.Log($"[fass] 2단계 측정 시작! {angleThresholdCalibrationDuration}초 동안 " +
                  "지금 자세(한계각도)를 그대로 유지해주세요.");
        yield return StartCoroutine(CalibrateAngleThreshold());

        Debug.Log("[fass] 통합 캘리브레이션 완료! 무표정 기준값과 각도 임계값이 모두 설정되었습니다.");
        isCalibratingSequence = false;
    }
}