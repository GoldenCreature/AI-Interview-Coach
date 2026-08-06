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

    [Header("노이즈 완화 (이동평균)")]
    [Tooltip("저장 시점에 사용할 최근 프레임 점수의 개수. 클수록 부드럽지만 반응이 느려짐.")]
    public int smoothingWindowSize = 30;

    // 매 프레임 계산된 점수를 담아두는 슬라이딩 윈도우
    private readonly Queue<float> smileBuffer = new Queue<float>();
    private readonly Queue<float> surpriseBuffer = new Queue<float>();
    private readonly Queue<float> angryBuffer = new Queue<float>();

    // [추가] 감정별 누적 통계 (저장이 일어날 때마다 누적됨)
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

        // [수정] 프레임 노이즈 완화: 매 프레임 점수를 계산해서 슬라이딩 윈도우에 쌓아둠.
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
        float smileScore = Average(smileBuffer);
        float surpriseScore = Average(surpriseBuffer);
        float angryScore = Average(angryBuffer);

        // [추가] 감정별 누적 합계/평균 갱신
        smileTotal += smileScore;
        surpriseTotal += surpriseScore;
        angryTotal += angryScore;
        smileCount++;
        surpriseCount++;
        angryCount++;

        Debug.Log($"[fass] 2단계 성공: 점수 계산 완료 (미소:{smileScore:F1}, 놀람:{surpriseScore:F1}, 분노:{angryScore:F1}, 샘플수:{smileBuffer.Count})");

        // [추가] 감정별 합계/평균을 각각 따로 출력
        Debug.Log($"[fass][기쁨] 이번 구간 평균:{smileScore:F2} | 누적 합계:{smileTotal:F1} | 누적 평균:{SmileAverage:F2} (총 {smileCount}회 기록)");
        Debug.Log($"[fass][놀람] 이번 구간 평균:{surpriseScore:F2} | 누적 합계:{surpriseTotal:F1} | 누적 평균:{SurpriseAverage:F2} (총 {surpriseCount}회 기록)");
        Debug.Log($"[fass][분노] 이번 구간 평균:{angryScore:F2} | 누적 합계:{angryTotal:F1} | 누적 평균:{AngryAverage:F2} (총 {angryCount}회 기록)");

        if (csvLogger != null)
        {
            csvLogger.SaveScoreToCSV(
                smileScore, surpriseScore, angryScore,
                smileTotal, SmileAverage,
                surpriseTotal, SurpriseAverage,
                angryTotal, AngryAverage);
            Debug.Log("[fass] 3단계 성공: CSV Logger에 데이터 저장을 전송했습니다!");
        }
        else
        {
            Debug.LogError("[fass] 에러: csvLogger 컴포넌트가 인스펙터에 연결되지 않았습니다.");
        }

        if (dbLogger != null)
        {
            dbLogger.SaveScoreToDB(
                smileScore, surpriseScore, angryScore,
                smileTotal, SmileAverage,
                surpriseTotal, SurpriseAverage,
                angryTotal, AngryAverage);
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

    // [수정] mouthWidth 대신 faceWidth로 정규화 -> Surprise/Angry와 기준 통일.
    // 기존 방식(mouthHeight / mouthWidth)은 웃을 때 입이 가로로도 벌어지면서
    // 분모가 같이 커져 ratio 증가폭이 둔해지는 문제가 있었음.
    // [수정] neutralSmileRatio를 빼서 무표정 상태의 baseline을 0으로 맞춤.
    private float CalculateSmile(List<Vector3> landmarks)
    {
        float mouthHeight = Vector3.Distance(landmarks[13], landmarks[14]);
        float faceWidth = Vector3.Distance(landmarks[234], landmarks[454]);
        float ratio = mouthHeight / (faceWidth > 0 ? faceWidth : 1f);

        // TODO: 아래 계수(35f)는 실제 테스트 데이터를 보며 재조정 필요.
        // 무표정/활짝 웃는 표정일 때 ratio 값을 각각 Debug.Log로 확인한 뒤 배율 조정 권장.
        float score = (ratio - neutralSmileRatio) * 35f;
        return Mathf.Clamp(score, 0f, 5f);
    }

    // [수정] neutralSurpriseRatio를 빼서 무표정 상태의 baseline을 0으로 맞춤.
    private float CalculateSurprise(List<Vector3> landmarks)
    {
        // 눈 뜬 정도(절대 거리)
        float leftEyeOpen = Vector3.Distance(landmarks[159], landmarks[145]);
        float rightEyeOpen = Vector3.Distance(landmarks[386], landmarks[374]);
        float avgEyeOpen = (leftEyeOpen + rightEyeOpen) / 2f;

        // 얼굴 너비로 정규화 -> 카메라 거리/얼굴 크기 영향을 줄여서
        // SurpriseScore가 표정과 무관하게 포화되는 문제를 완화
        float faceWidth = Vector3.Distance(landmarks[234], landmarks[454]);
        float ratio = avgEyeOpen / (faceWidth > 0 ? faceWidth : 1f);

        // TODO: 아래 계수(50f)는 실제 테스트 데이터를 보며 재조정 필요.
        // 평상시 표정과 놀란 표정일 때의 ratio 값을 각각 Debug.Log로 확인한 뒤
        // 그 사이 구간에 맞게 배율을 잡는 것을 권장.
        float score = (ratio - neutralSurpriseRatio) * 50f;
        return Mathf.Clamp(score, 0f, 5f);
    }

    private float CalculateAngry(List<Vector3> landmarks)
    {
        float eyebrowDist = Vector3.Distance(landmarks[55], landmarks[285]);
        float faceWidth = Vector3.Distance(landmarks[234], landmarks[454]);
        float ratio = eyebrowDist / (faceWidth > 0 ? faceWidth : 1f);
        float score = (0.23f - ratio) * 60f;
        return Mathf.Clamp(score, 0f, 5f);
    }
}