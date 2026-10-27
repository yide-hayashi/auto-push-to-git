using System.ComponentModel;

namespace Project_auto_push_to_gitlab.Alerts.Presentation;

/// <summary>Designer可編輯的院區集計卡片，只顯示外部提供的件數及狀態，不查詢資料。</summary>
public partial class CampusSummaryCard : UserControl
{
    /// <summary>建立設計時可用的靜態卡片，不連線DB或讀取檔案。</summary>
    /// <remarks>UI：FrmAlerts的cardKTGH00、cardHPK210、cardTSHIS直接建立。</remarks>
    public CampusSummaryCard()
    {
        // 1151005 codex GPT-6_しずくちゃん：Designerで移動や配置変更できる院区カードを作ります。
        InitializeComponent();
    }
    // 1151005 codex GPT-6_しずくちゃん：Designerでも編集できる表示プロパティを公開します。
    [Category("告警顯示"), DefaultValue("KTGH00")]
    public string Campus { get => lblCampus.Text; set => lblCampus.Text = value; }
    [Category("告警顯示"), DefaultValue("未執行")]
    public string StateText { get => lblState.Text; set => lblState.Text = value; }
    [Category("告警顯示"), DefaultValue("0")]
    public string CountText { get => lblCount.Text; set => lblCount.Text = value; }
}
