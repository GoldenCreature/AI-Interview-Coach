// ============================================================
// LegacyModels.cs
// ------------------------------------------------------------
// App_Setting은 현재 스키마에서 완전히 제거되었지만, Legacy 폴더의
// SchemaCrudTester.cs는 여전히 이전 원본 스키마(SchemaBootstrap.cs)를
// 대상으로 테스트하며, 그 원본 스키마에는 App_Setting이 그대로 남아있음.
// 그래서 이 클래스만 따로 이 파일로 옮겨, "현재 쓰는" InterviewDbModels.cs는
// 깨끗하게 유지하면서도 Legacy 스크립트는 수정 없이 계속 컴파일되도록 했음.
//
// ⚠  현재 스키마엔 이 테이블이 없기 떄문에, 신규 코드에서는이 클래스를 참조하면 안됨. 
// ============================================================
using SQLite;

namespace InterviewDb.Models
{
    /// <summary>[Legacy 전용] App_Setting — 5차 원본 스키마에서만 존재. 현재 스키마엔 없음.</summary>
    [Table("App_Setting")]
    public class AppSetting
    {
        [PrimaryKey, Column("setting_id")]
        public int SettingId { get; set; }

        [Column("volume_master")]
        public double VolumeMaster { get; set; }

        [Column("device_input")]
        public string DeviceInput { get; set; }

        [Column("device_output")]
        public string DeviceOutput { get; set; }

        [Column("resolution")]
        public string Resolution { get; set; }

        [Column("is_fullscreen")]
        public int IsFullscreen { get; set; }
    }
}
