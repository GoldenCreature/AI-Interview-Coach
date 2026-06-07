# 🎙️ AI 기반 면접 코칭 시스템

> Unity Windows Standalone 기반 AI 면접 코칭 애플리케이션  
> 하이테크 2팀 캡스톤 프로젝트

---

## 📌 프로젝트 개요

- **플랫폼** : Windows Standalone (Unity 기반 PC 독립형 앱)
- **구조** : Python 서버 없음 — Unity 단일 허브에서 모든 API 직접 제어
- **Unity 버전** : 2022.3.21f1


---

## 🛠️ 기술 스택

| 파트 | 기술 |
|------|------|
| LLM (면접관 AI) | Gemini API |
| STT (음성인식) | Google Cloud STT |
| TTS (음성합성) | Google Cloud TTS |
| 로컬 DB | SQLite (gilzoide/unity-sqlite-net) |
| UI | Unity uGUI |
| 얼굴 분석 / 시선 감지 | MediaPipe Unity Plugin (테스트 중) |

---

## 👥 팀원

| 이름 | 역할 |
|------|------|
| 한종수 (팀장) | Unity 통합 / AI 파트 리드 |
| 신모세 | AI 파트 / 얼굴 분석 (MediaPipe 테스트) |
| 한효준 | Unity UI 구현 |
| 정재웅 | UX/UI 기획 |
| 이재혁 | DB 파트 |

---

## 📅 개발 단계

- **1단계** : Gemini 면접관 모드 + STT/TTS 한국어 동작
- **2단계** : SQLite 연동 + 결과 리포트 UI
- **3단계** : 얼굴 분석 / 시선 감지 + 마무리 + 데모 준비

---

## ⚠️ 주의사항

- Unity 버전 **2022.3.21f1 전원 통일 필수**
- API 키는 절대 커밋하지 말 것
- `Library/` 폴더는 `.gitignore`로 제외됨
