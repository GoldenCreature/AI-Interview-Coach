using System.Collections.Generic;
using UnityEngine;
using Mediapipe.Tasks.Vision.FaceLandmarker;

public class MediaPipeFaceScorer : MonoBehaviour
{
    public float SmileScore { get; private set; }
    public float SurpriseScore { get; private set; }
    public float AngryScore { get; private set; }

    public void ProcessFaceBlendshapes(FaceLandmarkerResult result)
    {
        // 1. 얼굴 데이터가 비어있는지 리스트 카운트로 확인
        if (result.faceBlendshapes == null || result.faceBlendshapes.Count == 0)
            return;

        // 2. 첫 번째 감지된 얼굴의 표정 카테고리 데이터 가져오기
        var blendshapeCategories = result.faceBlendshapes[0].categories;

        if (blendshapeCategories == null)
            return;

        Dictionary<string, float> shapes = new Dictionary<string, float>();

        // 3. Category는 구조체이므로 null 체크 없이 바로 접근합니다.
        foreach (var category in blendshapeCategories)
        {
            // 카테고리 이름이 비어있지 않은지만 가볍게 체크해줍니다.
            if (!string.IsNullOrEmpty(category.categoryName))
            {
                shapes[category.categoryName] = category.score;
            }
        }

        // 4. 미소 점수 계산
        if (shapes.TryGetValue("mouthSmileLeft", out float smileL) &&
            shapes.TryGetValue("mouthSmileRight", out float smileR))
        {
            SmileScore = ((smileL + smileR) / 2f) * 100f;
        }

        // 5. 놀람 점수 계산
        if (shapes.TryGetValue("jawOpen", out float jawOpen) &&
            shapes.TryGetValue("eyeWideLeft", out float eyeWideL) &&
            shapes.TryGetValue("eyeWideRight", out float eyeWideR))
        {
            SurpriseScore = ((jawOpen + ((eyeWideL + eyeWideR) / 2f)) / 2f) * 100f;
        }

        // 6. 화남 점수 계산
        if (shapes.TryGetValue("browDownLeft", out float browDownL) &&
            shapes.TryGetValue("browDownRight", out float browDownR))
        {
            AngryScore = ((browDownL + browDownR) / 2f) * 100f;
        }
    }
}