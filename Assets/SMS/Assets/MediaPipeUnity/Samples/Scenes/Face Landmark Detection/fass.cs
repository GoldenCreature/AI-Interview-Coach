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

        // 3분 간격 체크: 아직 간격이 안 지났으면 계산/저장 자체를 스킵
        System.DateTime now = System.DateTime.Now;
        if ((now - lastLoggedTime).TotalMinutes < logIntervalMinutes)
        {
            return;
        }
        lastLoggedTime = now;

        float smileScore = CalculateSmile(landmarks);
        float surpriseScore = CalculateSurprise(landmarks);
        float angryScore = CalculateAngry(landmarks);

        Debug.Log($"[fass] 2단계 성공: 점수 계산 완료 (미소:{smileScore:F1}, 놀람:{surpriseScore:F1}, 분노:{angryScore:F1})");

        if (csvLogger != null)
        {
            csvLogger.SaveScoreToCSV(smileScore, surpriseScore, angryScore);
            Debug.Log("[fass] 3단계 성공: CSV Logger에 데이터 저장을 전송했습니다!");
        }
        else
        {
            Debug.LogError("[fass] 에러: csvLogger 컴포넌트가 인스펙터에 연결되지 않았습니다.");
        }

        if (dbLogger != null)
        {
            dbLogger.SaveScoreToDB(smileScore, surpriseScore, angryScore);
            Debug.Log("[fass] 4단계 성공: DB Logger에 데이터 저장을 전송했습니다!");
        }
        else
        {
            Debug.LogError("[fass] 에러: dbLogger 컴포넌트가 인스펙터에 연결되지 않았습니다.");
        }
    }

    private float CalculateSmile(List<Vector3> landmarks)
    {
        float mouthWidth = Vector3.Distance(landmarks[61], landmarks[291]);
        float mouthHeight = Vector3.Distance(landmarks[13], landmarks[14]);
        float ratio = mouthHeight / (mouthWidth > 0 ? mouthWidth : 1f);
        return Mathf.Clamp(ratio * 17.5f, 0f, 5f);
    }

    private float CalculateSurprise(List<Vector3> landmarks)
    {
        // 눈 뜬 정도(절대 거리)
        float leftEyeOpen = Vector3.Distance(landmarks[159], landmarks[145]);
        float rightEyeOpen = Vector3.Distance(landmarks[386], landmarks[374]);
        float avgEyeOpen = (leftEyeOpen + rightEyeOpen) / 2f;

        // [수정] 얼굴 너비로 정규화 -> 카메라 거리/얼굴 크기 영향을 줄여서
        // SurpriseScore가 표정과 무관하게 100에 포화되는 문제를 완화
        float faceWidth = Vector3.Distance(landmarks[234], landmarks[454]);
        float ratio = avgEyeOpen / (faceWidth > 0 ? faceWidth : 1f);

        // TODO: 아래 계수(1000f)는 실제 테스트 데이터를 보며 재조정 필요.
        // 평상시 표정과 놀란 표정일 때의 ratio 값을 각각 Debug.Log로 확인한 뒤
        // 그 사이 구간에 맞게 배율을 잡는 것을 권장.
        float score = ratio * 50f;
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