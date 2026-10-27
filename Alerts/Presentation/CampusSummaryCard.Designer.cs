#nullable disable
namespace Project_auto_push_to_gitlab.Alerts.Presentation;

partial class CampusSummaryCard
{
    private System.ComponentModel.IContainer components;
    private Label lblCampus;
    private Label lblCount;
    private Label lblState;
    protected override void Dispose(bool disposing)
    {
        if (disposing) components?.Dispose();
        base.Dispose(disposing);
    }
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        lblCampus = new Label();
        lblCount = new Label();
        lblState = new Label();
        SuspendLayout();
        lblCampus.AutoSize = true;
        lblCampus.Font = new Font("Microsoft JhengHei UI", 11F, FontStyle.Bold);
        lblCampus.Location = new Point(14, 9);
        lblCampus.Name = "lblCampus";
        lblCampus.Text = "KTGH00";
        lblCount.AutoSize = true;
        lblCount.Font = new Font("Microsoft JhengHei UI", 24F, FontStyle.Bold);
        lblCount.Location = new Point(12, 31);
        lblCount.Name = "lblCount";
        lblCount.Text = "0";
        lblState.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        lblState.Location = new Point(128, 45);
        lblState.Size = new Size(152, 32);
        lblState.TextAlign = ContentAlignment.MiddleRight;
        lblState.Name = "lblState";
        lblState.Text = "未執行";
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.FromArgb(238, 249, 244);
        BorderStyle = BorderStyle.FixedSingle;
        Controls.Add(lblCampus);
        Controls.Add(lblCount);
        Controls.Add(lblState);
        MinimumSize = new Size(290, 90);
        Name = "CampusSummaryCard";
        Size = new Size(302, 90);
        ResumeLayout(false);
        PerformLayout();
    }
}
