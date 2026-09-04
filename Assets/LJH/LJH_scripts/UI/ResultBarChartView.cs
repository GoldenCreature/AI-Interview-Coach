// ============================================================
// ResultBarChartView.cs
// ------------------------------------------------------------
// 결과/피드백 리포트 화면의 5점 척도 바 그래프 데이터(음성/내용/태도 점수)를
// 컴포넌트에 저장 및 보관만 담당하는 데이터 홀더(Data Container) 클래스.
// (uGUI 막대 크기 조작, 라벨 갱신 등 렌더링 로직은 UI 파트 담당 영역으로 이관)
// ============================================================
using UnityEngine;
using InterviewDb.Models;

namespace InterviewDb.UI
{
    public class ResultBarChartView : MonoBehaviour
    {
        [Header("5점 척도 저장 데이터 (음성 / 내용 / 태도)")]
        [SerializeField] private double? audioScore;
        [SerializeField] private double? contentScore;
        [SerializeField] private double? attitudeScore;

        // 외부(UI 렌더러 등)에서 읽을 수 있는 읽기 전용 프로퍼티
        public double? AudioScore => audioScore;
        public double? ContentScore => contentScore;
        public double? AttitudeScore => attitudeScore;

        /// <summary>
        /// DB 세션 리포트(SessionReportRow) 객체에서 3대 점수 데이터를 추출하여 저장.
        /// </summary>
        public void StoreReport(SessionReportRow report)
        {
            if (report == null)
            {
                ClearData();
                return;
            }

            StoreScores(report.ScoreAudio, report.ScoreContent, report.ScoreAttitude);
        }

        /// <summary>
        /// 영역별 점수 데이터를 직접 저장.
        /// </summary>
        public void StoreScores(double? audio, double? content, double? attitude)
        {
            audioScore = audio;
            contentScore = content;
            attitudeScore = attitude;
        }

        /// <summary>
        /// 저장된 점수 데이터를 초기화.
        /// </summary>
        public void ClearData()
        {
            audioScore = null;
            contentScore = null;
            attitudeScore = null;
        }

        // ------------------------------------------------------------
        // 기존 타 스크립트 호출 호환성 유지용 (Alias)[cite: 1]
        // ------------------------------------------------------------
        public void DisplayReport(SessionReportRow report) => StoreReport(report);
        public void SetScores(double? audioScore, double? contentScore, double? attitudeScore)
            => StoreScores(audioScore, contentScore, attitudeScore);
    }
}