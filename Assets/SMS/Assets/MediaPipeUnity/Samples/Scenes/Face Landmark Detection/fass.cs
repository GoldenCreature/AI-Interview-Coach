using System.Collections.Generic;
using Mediapipe.Tasks.Vision.FaceLandmarker;
using Mediapipe.Unity.Sample.FaceLandmarkDetection;
using UnityEngine;

public class fass : MonoBehaviour
{
    [Header("CSV Logger 연결")]
    [Tooltip("유니티 인스펙터 창에서 CustomPathCSVLogger 오브젝트를 여기에 드래그 앤 드롭 하세요.")]
    public CustomPathCSVLogger csvLogger;

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

    /// <summary>
    /// 미디어파이프에서 얼굴 좌표를 받으면 호출되는 실시간 분석 메인 함수
    /// </summary>
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
    }

    private float CalculateSmile(List<Vector3> landmarks)
    {
        float mouthWidth = Vector3.Distance(landmarks[61], landmarks[291]);
        float mouthHeight = Vector3.Distance(landmarks[13], landmarks[14]);
        float ratio = mouthHeight / (mouthWidth > 0 ? mouthWidth : 1f);
        return Mathf.Clamp(ratio * 350f, 0f, 100f);
    }

    private float CalculateSurprise(List<Vector3> landmarks)
    {
       
        float leftEyeOpen = Vector3.Distance(landmarks[159], landmarks[145]);
        float rightEyeOpen = Vector3.Distance(landmarks[386], landmarks[374]);
        float avgEyeOpen = (leftEyeOpen + rightEyeOpen) / 2f;
        float faceWidth = Vector3.Distance(landmarks[234], landmarks[454]);
        float ratio = avgEyeOpen / (faceWidth > 0 ? faceWidth : 1f);
        float score = ratio * 1000f;
        return Mathf.Clamp(score, 0f, 100f);
    }

    private float CalculateAngry(List<Vector3> landmarks)
    {
        float eyebrowDist = Vector3.Distance(landmarks[55], landmarks[285]);
        float faceWidth = Vector3.Distance(landmarks[234], landmarks[454]);
        float ratio = eyebrowDist / (faceWidth > 0 ? faceWidth : 1f);
        float score = (0.23f - ratio) * 1200f;
        return Mathf.Clamp(score, 0f, 100f);
    }
}