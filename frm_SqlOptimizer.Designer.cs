namespace Project_auto_push_to_gitlab
{
    partial class frm_SqlOptimizer
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            rootLayout = new TableLayoutPanel();
            splitMain = new SplitContainer();
            inputLayout = new TableLayoutPanel();
            optionsLayout = new TableLayoutPanel();
            lblHospitalArea = new Label();
            cmbHospitalArea = new ComboBox();
            lblGoal = new Label();
            cmbGoal = new ComboBox();
            lblTimeout = new Label();
            numTimeoutSeconds = new NumericUpDown();
            lblTimeoutUnit = new Label();
            lblPreviewRows = new Label();
            numPreviewRows = new NumericUpDown();
            lblPreviewRowsUnit = new Label();
            lblMaxCandidates = new Label();
            numMaxCandidates = new NumericUpDown();
            btnHelp = new Button();
            grpCodexExecutionSettings = new GroupBox();
            codexOptionsLayout = new TableLayoutPanel();
            lblCodexModel = new Label();
            cmbCodexModel = new ComboBox();
            lblCodexReasoning = new Label();
            cmbCodexReasoning = new ComboBox();
            lblCodexSpeed = new Label();
            cmbCodexSpeed = new ComboBox();
            btnRefreshCodexModels = new Button();
            lblCodexCatalogStatus = new Label();
            editorLayout = new TableLayoutPanel();
            grpSql = new GroupBox();
            txtSql = new TextBox();
            grpBindings = new GroupBox();
            gridBindings = new DataGridView();
            colBindName = new DataGridViewTextBoxColumn();
            colBindType = new DataGridViewComboBoxColumn();
            colBindNull = new DataGridViewCheckBoxColumn();
            colBindValue = new DataGridViewTextBoxColumn();
            colBindSize = new DataGridViewTextBoxColumn();
            commandPanel = new FlowLayoutPanel();
            btnApprove = new Button();
            btnStartCodex = new Button();
            btnCopySessionId = new Button();
            btnConfigureMcp = new Button();
            btnStop = new Button();
            lblSavedSessions = new Label();
            cmbSavedSessions = new ComboBox();
            lblExplainWarning = new Label();
            resultLayout = new TableLayoutPanel();
            grpHistory = new GroupBox();
            gridHistory = new DataGridView();
            colRound = new DataGridViewTextBoxColumn();
            colCandidate = new DataGridViewTextBoxColumn();
            colIterationStatus = new DataGridViewTextBoxColumn();
            colCost = new DataGridViewTextBoxColumn();
            colElapsed = new DataGridViewTextBoxColumn();
            colRows = new DataGridViewTextBoxColumn();
            colPlanHash = new DataGridViewTextBoxColumn();
            colUpdatedAt = new DataGridViewTextBoxColumn();
            detailsTabs = new TabControl();
            tabSqlDiff = new TabPage();
            txtSqlDiff = new TextBox();
            tabExplainPlan = new TabPage();
            txtExplainPlan = new TextBox();
            tabAiAnalysis = new TabPage();
            txtAiAnalysis = new TextBox();
            tabActualPlan = new TabPage();
            txtActualPlan = new TextBox();
            tabResultPreview = new TabPage();
            previewLayout = new TableLayoutPanel();
            lblResultNotice = new Label();
            gridResultPreview = new DataGridView();
            tabActivity = new TabPage();
            txtActivity = new TextBox();
            feedbackLayout = new TableLayoutPanel();
            lblFeedback = new Label();
            txtFeedback = new TextBox();
            feedbackButtons = new FlowLayoutPanel();
            btnAccept = new Button();
            btnContinue = new Button();
            statusStrip = new StatusStrip();
            lblState = new ToolStripStatusLabel();
            lblSessionId = new ToolStripStatusLabel();
            lblMcpStatus = new ToolStripStatusLabel();
            lblDetail = new ToolStripStatusLabel();
            rootLayout.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitMain).BeginInit();
            splitMain.Panel1.SuspendLayout();
            splitMain.Panel2.SuspendLayout();
            splitMain.SuspendLayout();
            inputLayout.SuspendLayout();
            optionsLayout.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numTimeoutSeconds).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numPreviewRows).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numMaxCandidates).BeginInit();
            grpCodexExecutionSettings.SuspendLayout();
            codexOptionsLayout.SuspendLayout();
            editorLayout.SuspendLayout();
            grpSql.SuspendLayout();
            grpBindings.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)gridBindings).BeginInit();
            commandPanel.SuspendLayout();
            resultLayout.SuspendLayout();
            grpHistory.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)gridHistory).BeginInit();
            detailsTabs.SuspendLayout();
            tabSqlDiff.SuspendLayout();
            tabExplainPlan.SuspendLayout();
            tabAiAnalysis.SuspendLayout();
            tabActualPlan.SuspendLayout();
            tabResultPreview.SuspendLayout();
            previewLayout.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)gridResultPreview).BeginInit();
            tabActivity.SuspendLayout();
            feedbackLayout.SuspendLayout();
            feedbackButtons.SuspendLayout();
            statusStrip.SuspendLayout();
            SuspendLayout();
            // 
            // rootLayout
            // 
            rootLayout.ColumnCount = 1;
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rootLayout.Controls.Add(splitMain, 0, 0);
            rootLayout.Controls.Add(statusStrip, 0, 1);
            rootLayout.Dock = DockStyle.Fill;
            rootLayout.Location = new Point(0, 0);
            rootLayout.Margin = new Padding(0);
            rootLayout.Name = "rootLayout";
            rootLayout.RowCount = 2;
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            rootLayout.Size = new Size(1480, 900);
            rootLayout.TabIndex = 0;
            // 
            // splitMain
            // 
            splitMain.Dock = DockStyle.Fill;
            splitMain.FixedPanel = FixedPanel.Panel1;
            splitMain.Location = new Point(3, 3);
            splitMain.Name = "splitMain";
            splitMain.Orientation = Orientation.Horizontal;
            // 
            // splitMain.Panel1
            // 
            splitMain.Panel1.Controls.Add(inputLayout);
            splitMain.Panel1MinSize = 340;
            // 
            // splitMain.Panel2
            // 
            splitMain.Panel2.Controls.Add(resultLayout);
            splitMain.Panel2MinSize = 300;
            splitMain.Size = new Size(1474, 870);
            splitMain.SplitterDistance = 393;
            splitMain.TabIndex = 0;
            // 
            // inputLayout
            // 
            inputLayout.ColumnCount = 1;
            inputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            inputLayout.Controls.Add(optionsLayout, 0, 0);
            inputLayout.Controls.Add(grpCodexExecutionSettings, 0, 1);
            inputLayout.Controls.Add(editorLayout, 0, 2);
            inputLayout.Controls.Add(commandPanel, 0, 3);
            inputLayout.Controls.Add(lblExplainWarning, 0, 4);
            inputLayout.Dock = DockStyle.Fill;
            inputLayout.Location = new Point(0, 0);
            inputLayout.Name = "inputLayout";
            inputLayout.RowCount = 5;
            inputLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 39F));
            inputLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
            inputLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            inputLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            inputLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            inputLayout.Size = new Size(1474, 393);
            inputLayout.TabIndex = 0;
            // 
            // optionsLayout
            // 
            optionsLayout.ColumnCount = 14;
            optionsLayout.ColumnStyles.Add(new ColumnStyle());
            optionsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125F));
            optionsLayout.ColumnStyles.Add(new ColumnStyle());
            optionsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135F));
            optionsLayout.ColumnStyles.Add(new ColumnStyle());
            optionsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70F));
            optionsLayout.ColumnStyles.Add(new ColumnStyle());
            optionsLayout.ColumnStyles.Add(new ColumnStyle());
            optionsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85F));
            optionsLayout.ColumnStyles.Add(new ColumnStyle());
            optionsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 75F));
            optionsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 75F));
            optionsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            optionsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96F));
            optionsLayout.Controls.Add(lblHospitalArea, 0, 0);
            optionsLayout.Controls.Add(cmbHospitalArea, 1, 0);
            optionsLayout.Controls.Add(lblGoal, 2, 0);
            optionsLayout.Controls.Add(cmbGoal, 3, 0);
            optionsLayout.Controls.Add(lblTimeout, 4, 0);
            optionsLayout.Controls.Add(numTimeoutSeconds, 5, 0);
            optionsLayout.Controls.Add(lblTimeoutUnit, 6, 0);
            optionsLayout.Controls.Add(lblPreviewRows, 7, 0);
            optionsLayout.Controls.Add(numPreviewRows, 8, 0);
            optionsLayout.Controls.Add(lblPreviewRowsUnit, 9, 0);
            optionsLayout.Controls.Add(lblMaxCandidates, 10, 0);
            optionsLayout.Controls.Add(numMaxCandidates, 11, 0);
            optionsLayout.Controls.Add(btnHelp, 13, 0);
            optionsLayout.Dock = DockStyle.Fill;
            optionsLayout.Location = new Point(3, 3);
            optionsLayout.Name = "optionsLayout";
            optionsLayout.RowCount = 1;
            optionsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            optionsLayout.Size = new Size(1468, 33);
            optionsLayout.TabIndex = 0;
            // 
            // lblHospitalArea
            // 
            lblHospitalArea.Anchor = AnchorStyles.Left;
            lblHospitalArea.AutoSize = true;
            lblHospitalArea.Location = new Point(3, 9);
            lblHospitalArea.Name = "lblHospitalArea";
            lblHospitalArea.Size = new Size(43, 15);
            lblHospitalArea.TabIndex = 0;
            lblHospitalArea.Text = "接続先";
            // 
            // cmbHospitalArea
            // 
            cmbHospitalArea.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            cmbHospitalArea.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbHospitalArea.FormattingEnabled = true;
            cmbHospitalArea.Items.AddRange(new object[] { "KTGH00", "HPK210", "TSHIS" });
            cmbHospitalArea.Location = new Point(52, 5);
            cmbHospitalArea.Name = "cmbHospitalArea";
            cmbHospitalArea.Size = new Size(119, 23);
            cmbHospitalArea.TabIndex = 1;
            // 
            // lblGoal
            // 
            lblGoal.Anchor = AnchorStyles.Left;
            lblGoal.AutoSize = true;
            lblGoal.Location = new Point(177, 9);
            lblGoal.Name = "lblGoal";
            lblGoal.Size = new Size(31, 15);
            lblGoal.TabIndex = 2;
            lblGoal.Text = "目的";
            // 
            // cmbGoal
            // 
            cmbGoal.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            cmbGoal.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbGoal.FormattingEnabled = true;
            cmbGoal.Items.AddRange(new object[] { "バランス", "応答時間", "I/O", "計画安定性" });
            cmbGoal.Location = new Point(214, 5);
            cmbGoal.Name = "cmbGoal";
            cmbGoal.Size = new Size(129, 23);
            cmbGoal.TabIndex = 3;
            // 
            // lblTimeout
            // 
            lblTimeout.Anchor = AnchorStyles.Left;
            lblTimeout.AutoSize = true;
            lblTimeout.Location = new Point(349, 9);
            lblTimeout.Name = "lblTimeout";
            lblTimeout.Size = new Size(79, 15);
            lblTimeout.TabIndex = 4;
            lblTimeout.Text = "最終実行上限";
            // 
            // numTimeoutSeconds
            // 
            numTimeoutSeconds.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            numTimeoutSeconds.Location = new Point(434, 5);
            numTimeoutSeconds.Maximum = new decimal(new int[] { 3600, 0, 0, 0 });
            numTimeoutSeconds.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numTimeoutSeconds.Name = "numTimeoutSeconds";
            numTimeoutSeconds.Size = new Size(64, 23);
            numTimeoutSeconds.TabIndex = 5;
            numTimeoutSeconds.TextAlign = HorizontalAlignment.Right;
            numTimeoutSeconds.Value = new decimal(new int[] { 60, 0, 0, 0 });
            // 
            // lblTimeoutUnit
            // 
            lblTimeoutUnit.Anchor = AnchorStyles.Left;
            lblTimeoutUnit.AutoSize = true;
            lblTimeoutUnit.Location = new Point(504, 9);
            lblTimeoutUnit.Name = "lblTimeoutUnit";
            lblTimeoutUnit.Size = new Size(19, 15);
            lblTimeoutUnit.TabIndex = 6;
            lblTimeoutUnit.Text = "秒";
            // 
            // lblPreviewRows
            // 
            lblPreviewRows.Anchor = AnchorStyles.Left;
            lblPreviewRows.AutoSize = true;
            lblPreviewRows.Location = new Point(529, 9);
            lblPreviewRows.Name = "lblPreviewRows";
            lblPreviewRows.Size = new Size(55, 15);
            lblPreviewRows.TabIndex = 7;
            lblPreviewRows.Text = "表示上限";
            // 
            // numPreviewRows
            // 
            numPreviewRows.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            numPreviewRows.Increment = new decimal(new int[] { 50, 0, 0, 0 });
            numPreviewRows.Location = new Point(590, 5);
            numPreviewRows.Maximum = new decimal(new int[] { 1000, 0, 0, 0 });
            numPreviewRows.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numPreviewRows.Name = "numPreviewRows";
            numPreviewRows.Size = new Size(79, 23);
            numPreviewRows.TabIndex = 8;
            numPreviewRows.TextAlign = HorizontalAlignment.Right;
            numPreviewRows.Value = new decimal(new int[] { 200, 0, 0, 0 });
            // 
            // lblPreviewRowsUnit
            // 
            lblPreviewRowsUnit.Anchor = AnchorStyles.Left;
            lblPreviewRowsUnit.AutoSize = true;
            lblPreviewRowsUnit.Location = new Point(675, 9);
            lblPreviewRowsUnit.Name = "lblPreviewRowsUnit";
            lblPreviewRowsUnit.Size = new Size(19, 15);
            lblPreviewRowsUnit.TabIndex = 9;
            lblPreviewRowsUnit.Text = "行";
            // 
            // lblMaxCandidates
            // 
            lblMaxCandidates.Anchor = AnchorStyles.Left;
            lblMaxCandidates.AutoSize = true;
            lblMaxCandidates.Location = new Point(700, 1);
            lblMaxCandidates.Name = "lblMaxCandidates";
            lblMaxCandidates.Size = new Size(60, 30);
            lblMaxCandidates.TabIndex = 10;
            lblMaxCandidates.Text = "候補/ラウンド";
            // 
            // numMaxCandidates
            // 
            numMaxCandidates.Anchor = AnchorStyles.Left;
            numMaxCandidates.Location = new Point(775, 5);
            numMaxCandidates.Maximum = new decimal(new int[] { 10, 0, 0, 0 });
            numMaxCandidates.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numMaxCandidates.Name = "numMaxCandidates";
            numMaxCandidates.Size = new Size(69, 23);
            numMaxCandidates.TabIndex = 11;
            numMaxCandidates.TextAlign = HorizontalAlignment.Right;
            numMaxCandidates.Value = new decimal(new int[] { 10, 0, 0, 0 });
            // 
            // btnHelp
            // 
            btnHelp.Dock = DockStyle.Fill;
            btnHelp.Location = new Point(1375, 3);
            btnHelp.Name = "btnHelp";
            btnHelp.Size = new Size(90, 27);
            btnHelp.TabIndex = 12;
            btnHelp.Text = "使い方";
            btnHelp.UseVisualStyleBackColor = true;
            btnHelp.Click += btnHelp_Click;
            // 
            // grpCodexExecutionSettings
            // 
            grpCodexExecutionSettings.Controls.Add(codexOptionsLayout);
            grpCodexExecutionSettings.Dock = DockStyle.Fill;
            grpCodexExecutionSettings.Location = new Point(3, 42);
            grpCodexExecutionSettings.Name = "grpCodexExecutionSettings";
            grpCodexExecutionSettings.Padding = new Padding(6, 3, 6, 5);
            grpCodexExecutionSettings.Size = new Size(1468, 52);
            grpCodexExecutionSettings.TabIndex = 1;
            grpCodexExecutionSettings.TabStop = false;
            grpCodexExecutionSettings.Text = "Codex実行設定";
            // 
            // codexOptionsLayout
            // 
            codexOptionsLayout.ColumnCount = 8;
            codexOptionsLayout.ColumnStyles.Add(new ColumnStyle());
            codexOptionsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230F));
            codexOptionsLayout.ColumnStyles.Add(new ColumnStyle());
            codexOptionsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125F));
            codexOptionsLayout.ColumnStyles.Add(new ColumnStyle());
            codexOptionsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125F));
            codexOptionsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96F));
            codexOptionsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            codexOptionsLayout.Controls.Add(lblCodexModel, 0, 0);
            codexOptionsLayout.Controls.Add(cmbCodexModel, 1, 0);
            codexOptionsLayout.Controls.Add(lblCodexReasoning, 2, 0);
            codexOptionsLayout.Controls.Add(cmbCodexReasoning, 3, 0);
            codexOptionsLayout.Controls.Add(lblCodexSpeed, 4, 0);
            codexOptionsLayout.Controls.Add(cmbCodexSpeed, 5, 0);
            codexOptionsLayout.Controls.Add(btnRefreshCodexModels, 6, 0);
            codexOptionsLayout.Controls.Add(lblCodexCatalogStatus, 7, 0);
            codexOptionsLayout.Dock = DockStyle.Fill;
            codexOptionsLayout.Location = new Point(6, 19);
            codexOptionsLayout.Name = "codexOptionsLayout";
            codexOptionsLayout.RowCount = 1;
            codexOptionsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            codexOptionsLayout.Size = new Size(1456, 28);
            codexOptionsLayout.TabIndex = 0;
            // 
            // lblCodexModel
            // 
            lblCodexModel.Anchor = AnchorStyles.Left;
            lblCodexModel.AutoSize = true;
            lblCodexModel.Location = new Point(3, 6);
            lblCodexModel.Name = "lblCodexModel";
            lblCodexModel.Size = new Size(43, 15);
            lblCodexModel.TabIndex = 0;
            lblCodexModel.Text = "モデル";
            // 
            // cmbCodexModel
            // 
            cmbCodexModel.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            cmbCodexModel.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbCodexModel.FormattingEnabled = true;
            cmbCodexModel.Location = new Point(52, 3);
            cmbCodexModel.Name = "cmbCodexModel";
            cmbCodexModel.Size = new Size(224, 23);
            cmbCodexModel.TabIndex = 1;
            // 
            // lblCodexReasoning
            // 
            lblCodexReasoning.Anchor = AnchorStyles.Left;
            lblCodexReasoning.AutoSize = true;
            lblCodexReasoning.Location = new Point(282, 6);
            lblCodexReasoning.Name = "lblCodexReasoning";
            lblCodexReasoning.Size = new Size(43, 15);
            lblCodexReasoning.TabIndex = 2;
            lblCodexReasoning.Text = "推論量";
            // 
            // cmbCodexReasoning
            // 
            cmbCodexReasoning.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            cmbCodexReasoning.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbCodexReasoning.FormattingEnabled = true;
            cmbCodexReasoning.Location = new Point(331, 3);
            cmbCodexReasoning.Name = "cmbCodexReasoning";
            cmbCodexReasoning.Size = new Size(119, 23);
            cmbCodexReasoning.TabIndex = 3;
            // 
            // lblCodexSpeed
            // 
            lblCodexSpeed.Anchor = AnchorStyles.Left;
            lblCodexSpeed.AutoSize = true;
            lblCodexSpeed.Location = new Point(456, 6);
            lblCodexSpeed.Name = "lblCodexSpeed";
            lblCodexSpeed.Size = new Size(31, 15);
            lblCodexSpeed.TabIndex = 4;
            lblCodexSpeed.Text = "速度";
            // 
            // cmbCodexSpeed
            // 
            cmbCodexSpeed.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            cmbCodexSpeed.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbCodexSpeed.FormattingEnabled = true;
            cmbCodexSpeed.Location = new Point(493, 3);
            cmbCodexSpeed.Name = "cmbCodexSpeed";
            cmbCodexSpeed.Size = new Size(119, 23);
            cmbCodexSpeed.TabIndex = 5;
            // 
            // btnRefreshCodexModels
            // 
            btnRefreshCodexModels.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            btnRefreshCodexModels.Location = new Point(618, 3);
            btnRefreshCodexModels.Name = "btnRefreshCodexModels";
            btnRefreshCodexModels.Size = new Size(90, 22);
            btnRefreshCodexModels.TabIndex = 6;
            btnRefreshCodexModels.Text = "一覧更新";
            btnRefreshCodexModels.UseVisualStyleBackColor = true;
            // 
            // lblCodexCatalogStatus
            // 
            lblCodexCatalogStatus.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            lblCodexCatalogStatus.AutoEllipsis = true;
            lblCodexCatalogStatus.ForeColor = Color.DimGray;
            lblCodexCatalogStatus.Location = new Point(714, 5);
            lblCodexCatalogStatus.Name = "lblCodexCatalogStatus";
            lblCodexCatalogStatus.Size = new Size(739, 18);
            lblCodexCatalogStatus.TabIndex = 7;
            lblCodexCatalogStatus.Text = "Codex全体設定を継承します。";
            lblCodexCatalogStatus.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // editorLayout
            // 
            editorLayout.ColumnCount = 2;
            editorLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58F));
            editorLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42F));
            editorLayout.Controls.Add(grpSql, 0, 0);
            editorLayout.Controls.Add(grpBindings, 1, 0);
            editorLayout.Dock = DockStyle.Fill;
            editorLayout.Location = new Point(3, 100);
            editorLayout.Name = "editorLayout";
            editorLayout.RowCount = 1;
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            editorLayout.Size = new Size(1468, 216);
            editorLayout.TabIndex = 2;
            // 
            // grpSql
            // 
            grpSql.Controls.Add(txtSql);
            grpSql.Dock = DockStyle.Fill;
            grpSql.Location = new Point(3, 3);
            grpSql.Name = "grpSql";
            grpSql.Padding = new Padding(8);
            grpSql.Size = new Size(845, 210);
            grpSql.TabIndex = 0;
            grpSql.TabStop = false;
            grpSql.Text = "SELECT SQL（単一文のみ）";
            // 
            // txtSql
            // 
            txtSql.AcceptsReturn = true;
            txtSql.AcceptsTab = true;
            txtSql.Dock = DockStyle.Fill;
            txtSql.Font = new Font("Consolas", 10F);
            txtSql.Location = new Point(8, 24);
            txtSql.MaxLength = 0;
            txtSql.Multiline = true;
            txtSql.Name = "txtSql";
            txtSql.ScrollBars = ScrollBars.Both;
            txtSql.Size = new Size(829, 178);
            txtSql.TabIndex = 0;
            txtSql.WordWrap = false;
            // 
            // grpBindings
            // 
            grpBindings.Controls.Add(gridBindings);
            grpBindings.Dock = DockStyle.Fill;
            grpBindings.Location = new Point(854, 3);
            grpBindings.Name = "grpBindings";
            grpBindings.Padding = new Padding(8);
            grpBindings.Size = new Size(611, 210);
            grpBindings.TabIndex = 1;
            grpBindings.TabStop = false;
            grpBindings.Text = "バインド変数（値はAgentへ送信・保存しません）";
            // 
            // gridBindings
            // 
            gridBindings.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            gridBindings.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            gridBindings.Columns.AddRange(new DataGridViewColumn[] { colBindName, colBindType, colBindNull, colBindValue, colBindSize });
            gridBindings.Dock = DockStyle.Fill;
            gridBindings.Location = new Point(8, 24);
            gridBindings.Name = "gridBindings";
            gridBindings.RowHeadersWidth = 30;
            gridBindings.Size = new Size(595, 178);
            gridBindings.TabIndex = 0;
            // 
            // colBindName
            // 
            colBindName.FillWeight = 80F;
            colBindName.HeaderText = "名前";
            colBindName.Name = "colBindName";
            // 
            // colBindType
            // 
            colBindType.FlatStyle = FlatStyle.Flat;
            colBindType.HeaderText = "Oracle型";
            colBindType.Items.AddRange(new object[] { "Varchar2", "NVarchar2", "Char", "NChar", "Int16", "Int32", "Int64", "Decimal", "Single", "Double", "Date", "TimeStamp", "TimeStampWithTimeZone", "TimeStampWithLocalTimeZone", "Raw", "Blob", "Clob", "NClob", "Guid", "Boolean" });
            colBindType.Name = "colBindType";
            // 
            // colBindNull
            // 
            colBindNull.FillWeight = 40F;
            colBindNull.HeaderText = "NULL";
            colBindNull.Name = "colBindNull";
            // 
            // colBindValue
            // 
            colBindValue.FillWeight = 130F;
            colBindValue.HeaderText = "値";
            colBindValue.Name = "colBindValue";
            // 
            // colBindSize
            // 
            colBindSize.FillWeight = 55F;
            colBindSize.HeaderText = "Size";
            colBindSize.Name = "colBindSize";
            // 
            // commandPanel
            // 
            commandPanel.Controls.Add(btnApprove);
            commandPanel.Controls.Add(btnStartCodex);
            commandPanel.Controls.Add(btnCopySessionId);
            commandPanel.Controls.Add(btnConfigureMcp);
            commandPanel.Controls.Add(btnStop);
            commandPanel.Controls.Add(lblSavedSessions);
            commandPanel.Controls.Add(cmbSavedSessions);
            commandPanel.Dock = DockStyle.Fill;
            commandPanel.Location = new Point(3, 322);
            commandPanel.Name = "commandPanel";
            commandPanel.Padding = new Padding(4);
            commandPanel.Size = new Size(1468, 38);
            commandPanel.TabIndex = 3;
            // 
            // btnApprove
            // 
            btnApprove.Location = new Point(7, 7);
            btnApprove.Name = "btnApprove";
            btnApprove.Size = new Size(96, 27);
            btnApprove.TabIndex = 0;
            btnApprove.Text = "初回承認";
            btnApprove.UseVisualStyleBackColor = true;
            btnApprove.Click += btnApprove_Click;
            // 
            // btnStartCodex
            // 
            btnStartCodex.Location = new Point(109, 7);
            btnStartCodex.Name = "btnStartCodex";
            btnStartCodex.Size = new Size(112, 27);
            btnStartCodex.TabIndex = 1;
            btnStartCodex.Text = "Codexで開始";
            btnStartCodex.UseVisualStyleBackColor = true;
            btnStartCodex.Click += btnStartCodex_Click;
            // 
            // btnCopySessionId
            // 
            btnCopySessionId.Location = new Point(227, 7);
            btnCopySessionId.Name = "btnCopySessionId";
            btnCopySessionId.Size = new Size(154, 27);
            btnCopySessionId.TabIndex = 2;
            btnCopySessionId.Text = "外部Agent用IDをコピー";
            btnCopySessionId.UseVisualStyleBackColor = true;
            btnCopySessionId.Click += btnCopySessionId_Click;
            // 
            // btnConfigureMcp
            // 
            btnConfigureMcp.Location = new Point(387, 7);
            btnConfigureMcp.Name = "btnConfigureMcp";
            btnConfigureMcp.Size = new Size(105, 27);
            btnConfigureMcp.TabIndex = 3;
            btnConfigureMcp.Text = "MCP設定";
            btnConfigureMcp.UseVisualStyleBackColor = true;
            btnConfigureMcp.Click += btnConfigureMcp_Click;
            // 
            // btnStop
            // 
            btnStop.Location = new Point(498, 7);
            btnStop.Name = "btnStop";
            btnStop.Size = new Size(88, 27);
            btnStop.TabIndex = 4;
            btnStop.Text = "停止";
            btnStop.UseVisualStyleBackColor = true;
            btnStop.Click += btnStop_Click;
            // 
            // lblSavedSessions
            // 
            lblSavedSessions.AutoSize = true;
            lblSavedSessions.Location = new Point(598, 13);
            lblSavedSessions.Margin = new Padding(9, 9, 3, 0);
            lblSavedSessions.Name = "lblSavedSessions";
            lblSavedSessions.Size = new Size(31, 15);
            lblSavedSessions.TabIndex = 5;
            lblSavedSessions.Text = "履歴";
            // 
            // cmbSavedSessions
            // 
            cmbSavedSessions.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbSavedSessions.FormattingEnabled = true;
            cmbSavedSessions.Location = new Point(635, 8);
            cmbSavedSessions.Margin = new Padding(3, 4, 3, 3);
            cmbSavedSessions.Name = "cmbSavedSessions";
            cmbSavedSessions.Size = new Size(225, 23);
            cmbSavedSessions.TabIndex = 6;
            cmbSavedSessions.SelectionChangeCommitted += cmbSavedSessions_SelectionChangeCommitted;
            // 
            // lblExplainWarning
            // 
            lblExplainWarning.Dock = DockStyle.Fill;
            lblExplainWarning.ForeColor = Color.DarkGoldenrod;
            lblExplainWarning.Location = new Point(3, 363);
            lblExplainWarning.Margin = new Padding(3, 0, 3, 3);
            lblExplainWarning.Name = "lblExplainWarning";
            lblExplainWarning.Padding = new Padding(8, 0, 0, 0);
            lblExplainWarning.Size = new Size(1468, 27);
            lblExplainWarning.TabIndex = 4;
            lblExplainWarning.Text = "注意: EXPLAIN PLANはバインドピーキングを再現しない場合があります。最終判断は実測計画で行います。";
            lblExplainWarning.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // resultLayout
            // 
            resultLayout.ColumnCount = 1;
            resultLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            resultLayout.Controls.Add(grpHistory, 0, 0);
            resultLayout.Controls.Add(detailsTabs, 0, 1);
            resultLayout.Controls.Add(feedbackLayout, 0, 2);
            resultLayout.Dock = DockStyle.Fill;
            resultLayout.Location = new Point(0, 0);
            resultLayout.Name = "resultLayout";
            resultLayout.RowCount = 3;
            resultLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 35F));
            resultLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 65F));
            resultLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76F));
            resultLayout.Size = new Size(1474, 473);
            resultLayout.TabIndex = 0;
            // 
            // grpHistory
            // 
            grpHistory.Controls.Add(gridHistory);
            grpHistory.Dock = DockStyle.Fill;
            grpHistory.Location = new Point(3, 3);
            grpHistory.Name = "grpHistory";
            grpHistory.Padding = new Padding(6);
            grpHistory.Size = new Size(1468, 132);
            grpHistory.TabIndex = 0;
            grpHistory.TabStop = false;
            grpHistory.Text = "最適化履歴";
            // 
            // gridHistory
            // 
            gridHistory.AllowUserToAddRows = false;
            gridHistory.AllowUserToDeleteRows = false;
            gridHistory.AllowUserToOrderColumns = true;
            gridHistory.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            gridHistory.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            gridHistory.Columns.AddRange(new DataGridViewColumn[] { colRound, colCandidate, colIterationStatus, colCost, colElapsed, colRows, colPlanHash, colUpdatedAt });
            gridHistory.Dock = DockStyle.Fill;
            gridHistory.Location = new Point(6, 22);
            gridHistory.MultiSelect = false;
            gridHistory.Name = "gridHistory";
            gridHistory.ReadOnly = true;
            gridHistory.RowHeadersVisible = false;
            gridHistory.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridHistory.Size = new Size(1456, 104);
            gridHistory.TabIndex = 0;
            gridHistory.SelectionChanged += gridHistory_SelectionChanged;
            // 
            // colRound
            // 
            colRound.FillWeight = 45F;
            colRound.HeaderText = "Round";
            colRound.Name = "colRound";
            colRound.ReadOnly = true;
            // 
            // colCandidate
            // 
            colCandidate.FillWeight = 50F;
            colCandidate.HeaderText = "候補";
            colCandidate.Name = "colCandidate";
            colCandidate.ReadOnly = true;
            // 
            // colIterationStatus
            // 
            colIterationStatus.FillWeight = 85F;
            colIterationStatus.HeaderText = "状態";
            colIterationStatus.Name = "colIterationStatus";
            colIterationStatus.ReadOnly = true;
            // 
            // colCost
            // 
            colCost.FillWeight = 60F;
            colCost.HeaderText = "Cost";
            colCost.Name = "colCost";
            colCost.ReadOnly = true;
            // 
            // colElapsed
            // 
            colElapsed.FillWeight = 75F;
            colElapsed.HeaderText = "実行ms";
            colElapsed.Name = "colElapsed";
            colElapsed.ReadOnly = true;
            // 
            // colRows
            // 
            colRows.FillWeight = 65F;
            colRows.HeaderText = "行数";
            colRows.Name = "colRows";
            colRows.ReadOnly = true;
            // 
            // colPlanHash
            // 
            colPlanHash.HeaderText = "Plan Hash";
            colPlanHash.Name = "colPlanHash";
            colPlanHash.ReadOnly = true;
            // 
            // colUpdatedAt
            // 
            colUpdatedAt.HeaderText = "更新日時";
            colUpdatedAt.Name = "colUpdatedAt";
            colUpdatedAt.ReadOnly = true;
            // 
            // detailsTabs
            // 
            detailsTabs.Controls.Add(tabSqlDiff);
            detailsTabs.Controls.Add(tabExplainPlan);
            detailsTabs.Controls.Add(tabAiAnalysis);
            detailsTabs.Controls.Add(tabActualPlan);
            detailsTabs.Controls.Add(tabResultPreview);
            detailsTabs.Controls.Add(tabActivity);
            detailsTabs.Dock = DockStyle.Fill;
            detailsTabs.Location = new Point(3, 141);
            detailsTabs.Name = "detailsTabs";
            detailsTabs.SelectedIndex = 0;
            detailsTabs.Size = new Size(1468, 252);
            detailsTabs.TabIndex = 1;
            // 
            // tabSqlDiff
            // 
            tabSqlDiff.Controls.Add(txtSqlDiff);
            tabSqlDiff.Location = new Point(4, 24);
            tabSqlDiff.Name = "tabSqlDiff";
            tabSqlDiff.Padding = new Padding(3);
            tabSqlDiff.Size = new Size(1460, 224);
            tabSqlDiff.TabIndex = 0;
            tabSqlDiff.Text = "SQL差分";
            tabSqlDiff.UseVisualStyleBackColor = true;
            // 
            // txtSqlDiff
            // 
            txtSqlDiff.Dock = DockStyle.Fill;
            txtSqlDiff.Font = new Font("Consolas", 9F);
            txtSqlDiff.Location = new Point(3, 3);
            txtSqlDiff.Multiline = true;
            txtSqlDiff.Name = "txtSqlDiff";
            txtSqlDiff.ReadOnly = true;
            txtSqlDiff.ScrollBars = ScrollBars.Both;
            txtSqlDiff.Size = new Size(1454, 218);
            txtSqlDiff.TabIndex = 0;
            txtSqlDiff.WordWrap = false;
            // 
            // tabExplainPlan
            // 
            tabExplainPlan.Controls.Add(txtExplainPlan);
            tabExplainPlan.Location = new Point(4, 24);
            tabExplainPlan.Name = "tabExplainPlan";
            tabExplainPlan.Padding = new Padding(3);
            tabExplainPlan.Size = new Size(1460, 224);
            tabExplainPlan.TabIndex = 1;
            tabExplainPlan.Text = "EXPLAIN";
            tabExplainPlan.UseVisualStyleBackColor = true;
            // 
            // txtExplainPlan
            // 
            txtExplainPlan.Dock = DockStyle.Fill;
            txtExplainPlan.Font = new Font("Consolas", 9F);
            txtExplainPlan.Location = new Point(3, 3);
            txtExplainPlan.Multiline = true;
            txtExplainPlan.Name = "txtExplainPlan";
            txtExplainPlan.ReadOnly = true;
            txtExplainPlan.ScrollBars = ScrollBars.Both;
            txtExplainPlan.Size = new Size(1454, 218);
            txtExplainPlan.TabIndex = 0;
            txtExplainPlan.WordWrap = false;
            // 
            // tabAiAnalysis
            // 
            tabAiAnalysis.Controls.Add(txtAiAnalysis);
            tabAiAnalysis.Location = new Point(4, 24);
            tabAiAnalysis.Name = "tabAiAnalysis";
            tabAiAnalysis.Padding = new Padding(3);
            tabAiAnalysis.Size = new Size(1460, 224);
            tabAiAnalysis.TabIndex = 2;
            tabAiAnalysis.Text = "AI分析";
            tabAiAnalysis.UseVisualStyleBackColor = true;
            // 
            // txtAiAnalysis
            // 
            txtAiAnalysis.Dock = DockStyle.Fill;
            txtAiAnalysis.Location = new Point(3, 3);
            txtAiAnalysis.Multiline = true;
            txtAiAnalysis.Name = "txtAiAnalysis";
            txtAiAnalysis.ReadOnly = true;
            txtAiAnalysis.ScrollBars = ScrollBars.Both;
            txtAiAnalysis.Size = new Size(1454, 218);
            txtAiAnalysis.TabIndex = 0;
            // 
            // tabActualPlan
            // 
            tabActualPlan.Controls.Add(txtActualPlan);
            tabActualPlan.Location = new Point(4, 24);
            tabActualPlan.Name = "tabActualPlan";
            tabActualPlan.Padding = new Padding(3);
            tabActualPlan.Size = new Size(1460, 224);
            tabActualPlan.TabIndex = 3;
            tabActualPlan.Text = "実測計画";
            tabActualPlan.UseVisualStyleBackColor = true;
            // 
            // txtActualPlan
            // 
            txtActualPlan.Dock = DockStyle.Fill;
            txtActualPlan.Font = new Font("Consolas", 9F);
            txtActualPlan.Location = new Point(3, 3);
            txtActualPlan.Multiline = true;
            txtActualPlan.Name = "txtActualPlan";
            txtActualPlan.ReadOnly = true;
            txtActualPlan.ScrollBars = ScrollBars.Both;
            txtActualPlan.Size = new Size(1454, 218);
            txtActualPlan.TabIndex = 0;
            txtActualPlan.WordWrap = false;
            // 
            // tabResultPreview
            // 
            tabResultPreview.Controls.Add(previewLayout);
            tabResultPreview.Location = new Point(4, 24);
            tabResultPreview.Name = "tabResultPreview";
            tabResultPreview.Padding = new Padding(3);
            tabResultPreview.Size = new Size(1460, 224);
            tabResultPreview.TabIndex = 4;
            tabResultPreview.Text = "結果プレビュー";
            tabResultPreview.UseVisualStyleBackColor = true;
            // 
            // previewLayout
            // 
            previewLayout.ColumnCount = 1;
            previewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            previewLayout.Controls.Add(lblResultNotice, 0, 0);
            previewLayout.Controls.Add(gridResultPreview, 0, 1);
            previewLayout.Dock = DockStyle.Fill;
            previewLayout.Location = new Point(3, 3);
            previewLayout.Name = "previewLayout";
            previewLayout.RowCount = 2;
            previewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
            previewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            previewLayout.Size = new Size(1454, 218);
            previewLayout.TabIndex = 0;
            // 
            // lblResultNotice
            // 
            lblResultNotice.Anchor = AnchorStyles.Left;
            lblResultNotice.AutoSize = true;
            lblResultNotice.ForeColor = Color.DarkSlateGray;
            lblResultNotice.Location = new Point(3, 5);
            lblResultNotice.Name = "lblResultNotice";
            lblResultNotice.Size = new Size(437, 15);
            lblResultNotice.TabIndex = 0;
            lblResultNotice.Text = "結果プレビューはありません。バインド値と結果行はAgentへ送信されません。";
            // 
            // gridResultPreview
            // 
            gridResultPreview.AllowUserToAddRows = false;
            gridResultPreview.AllowUserToDeleteRows = false;
            gridResultPreview.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            gridResultPreview.Dock = DockStyle.Fill;
            gridResultPreview.Location = new Point(3, 29);
            gridResultPreview.Name = "gridResultPreview";
            gridResultPreview.ReadOnly = true;
            gridResultPreview.RowHeadersVisible = false;
            gridResultPreview.Size = new Size(1448, 186);
            gridResultPreview.TabIndex = 1;
            // 
            // tabActivity
            // 
            tabActivity.Controls.Add(txtActivity);
            tabActivity.Location = new Point(4, 24);
            tabActivity.Name = "tabActivity";
            tabActivity.Padding = new Padding(3);
            tabActivity.Size = new Size(1460, 224);
            tabActivity.TabIndex = 5;
            tabActivity.Text = "Agent進捗";
            tabActivity.UseVisualStyleBackColor = true;
            // 
            // txtActivity
            // 
            txtActivity.Dock = DockStyle.Fill;
            txtActivity.Font = new Font("Consolas", 9F);
            txtActivity.Location = new Point(3, 3);
            txtActivity.Multiline = true;
            txtActivity.Name = "txtActivity";
            txtActivity.ReadOnly = true;
            txtActivity.ScrollBars = ScrollBars.Both;
            txtActivity.Size = new Size(1454, 218);
            txtActivity.TabIndex = 0;
            txtActivity.WordWrap = false;
            // 
            // feedbackLayout
            // 
            feedbackLayout.ColumnCount = 3;
            feedbackLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85F));
            feedbackLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            feedbackLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230F));
            feedbackLayout.Controls.Add(lblFeedback, 0, 0);
            feedbackLayout.Controls.Add(txtFeedback, 1, 0);
            feedbackLayout.Controls.Add(feedbackButtons, 2, 0);
            feedbackLayout.Dock = DockStyle.Fill;
            feedbackLayout.Location = new Point(3, 399);
            feedbackLayout.Name = "feedbackLayout";
            feedbackLayout.RowCount = 1;
            feedbackLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            feedbackLayout.Size = new Size(1468, 71);
            feedbackLayout.TabIndex = 2;
            // 
            // lblFeedback
            // 
            lblFeedback.Anchor = AnchorStyles.Left;
            lblFeedback.AutoSize = true;
            lblFeedback.Location = new Point(3, 20);
            lblFeedback.Name = "lblFeedback";
            lblFeedback.Size = new Size(79, 30);
            lblFeedback.TabIndex = 0;
            lblFeedback.Text = "フィードバック";
            // 
            // txtFeedback
            // 
            txtFeedback.Dock = DockStyle.Fill;
            txtFeedback.Location = new Point(88, 3);
            txtFeedback.Multiline = true;
            txtFeedback.Name = "txtFeedback";
            txtFeedback.ReadOnly = true;
            txtFeedback.ScrollBars = ScrollBars.Vertical;
            txtFeedback.Size = new Size(1147, 65);
            txtFeedback.TabIndex = 1;
            // 
            // feedbackButtons
            // 
            feedbackButtons.Controls.Add(btnAccept);
            feedbackButtons.Controls.Add(btnContinue);
            feedbackButtons.Dock = DockStyle.Fill;
            feedbackButtons.Location = new Point(1241, 3);
            feedbackButtons.Name = "feedbackButtons";
            feedbackButtons.Padding = new Padding(3, 17, 3, 3);
            feedbackButtons.Size = new Size(224, 65);
            feedbackButtons.TabIndex = 2;
            // 
            // btnAccept
            // 
            btnAccept.Location = new Point(6, 20);
            btnAccept.Name = "btnAccept";
            btnAccept.Size = new Size(84, 29);
            btnAccept.TabIndex = 0;
            btnAccept.Text = "採用";
            btnAccept.UseVisualStyleBackColor = true;
            btnAccept.Click += btnAccept_Click;
            // 
            // btnContinue
            // 
            btnContinue.Location = new Point(96, 20);
            btnContinue.Name = "btnContinue";
            btnContinue.Size = new Size(116, 29);
            btnContinue.TabIndex = 1;
            btnContinue.Text = "改善を継続";
            btnContinue.UseVisualStyleBackColor = true;
            btnContinue.Click += btnContinue_Click;
            // 
            // statusStrip
            // 
            statusStrip.Dock = DockStyle.Fill;
            statusStrip.Items.AddRange(new ToolStripItem[] { lblState, lblSessionId, lblMcpStatus, lblDetail });
            statusStrip.Location = new Point(0, 876);
            statusStrip.Name = "statusStrip";
            statusStrip.Size = new Size(1480, 24);
            statusStrip.SizingGrip = false;
            statusStrip.TabIndex = 1;
            // 
            // lblState
            // 
            lblState.BorderSides = ToolStripStatusLabelBorderSides.Right;
            lblState.Name = "lblState";
            lblState.Size = new Size(69, 19);
            lblState.Text = "状態: Draft";
            // 
            // lblSessionId
            // 
            lblSessionId.BorderSides = ToolStripStatusLabelBorderSides.Right;
            lblSessionId.Name = "lblSessionId";
            lblSessionId.Size = new Size(95, 19);
            lblSessionId.Text = "Session: 未作成";
            // 
            // lblMcpStatus
            // 
            lblMcpStatus.BorderSides = ToolStripStatusLabelBorderSides.Right;
            lblMcpStatus.Name = "lblMcpStatus";
            lblMcpStatus.Size = new Size(80, 19);
            lblMcpStatus.Text = "MCP: 未接続";
            // 
            // lblDetail
            // 
            lblDetail.Name = "lblDetail";
            lblDetail.Size = new Size(1221, 19);
            lblDetail.Spring = true;
            lblDetail.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // frm_SqlOptimizer
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1480, 900);
            Controls.Add(rootLayout);
            MinimumSize = new Size(1100, 720);
            Name = "frm_SqlOptimizer";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "AI SQL最適化";
            Load += frm_SqlOptimizer_Load;
            rootLayout.ResumeLayout(false);
            rootLayout.PerformLayout();
            splitMain.Panel1.ResumeLayout(false);
            splitMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitMain).EndInit();
            splitMain.ResumeLayout(false);
            inputLayout.ResumeLayout(false);
            optionsLayout.ResumeLayout(false);
            optionsLayout.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)numTimeoutSeconds).EndInit();
            ((System.ComponentModel.ISupportInitialize)numPreviewRows).EndInit();
            ((System.ComponentModel.ISupportInitialize)numMaxCandidates).EndInit();
            grpCodexExecutionSettings.ResumeLayout(false);
            codexOptionsLayout.ResumeLayout(false);
            codexOptionsLayout.PerformLayout();
            editorLayout.ResumeLayout(false);
            grpSql.ResumeLayout(false);
            grpSql.PerformLayout();
            grpBindings.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)gridBindings).EndInit();
            commandPanel.ResumeLayout(false);
            commandPanel.PerformLayout();
            resultLayout.ResumeLayout(false);
            grpHistory.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)gridHistory).EndInit();
            detailsTabs.ResumeLayout(false);
            tabSqlDiff.ResumeLayout(false);
            tabSqlDiff.PerformLayout();
            tabExplainPlan.ResumeLayout(false);
            tabExplainPlan.PerformLayout();
            tabAiAnalysis.ResumeLayout(false);
            tabAiAnalysis.PerformLayout();
            tabActualPlan.ResumeLayout(false);
            tabActualPlan.PerformLayout();
            tabResultPreview.ResumeLayout(false);
            previewLayout.ResumeLayout(false);
            previewLayout.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)gridResultPreview).EndInit();
            tabActivity.ResumeLayout(false);
            tabActivity.PerformLayout();
            feedbackLayout.ResumeLayout(false);
            feedbackLayout.PerformLayout();
            feedbackButtons.ResumeLayout(false);
            statusStrip.ResumeLayout(false);
            statusStrip.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel rootLayout;
        private SplitContainer splitMain;
        private TableLayoutPanel inputLayout;
        private TableLayoutPanel optionsLayout;
        private Label lblHospitalArea;
        private ComboBox cmbHospitalArea;
        private Label lblGoal;
        private ComboBox cmbGoal;
        private Label lblTimeout;
        private NumericUpDown numTimeoutSeconds;
        private Label lblTimeoutUnit;
        private Label lblPreviewRows;
        private NumericUpDown numPreviewRows;
        private Label lblPreviewRowsUnit;
        private Label lblMaxCandidates;
        private NumericUpDown numMaxCandidates;
        private Button btnHelp;
        private GroupBox grpCodexExecutionSettings;
        private TableLayoutPanel codexOptionsLayout;
        private Label lblCodexModel;
        private ComboBox cmbCodexModel;
        private Label lblCodexReasoning;
        private ComboBox cmbCodexReasoning;
        private Label lblCodexSpeed;
        private ComboBox cmbCodexSpeed;
        private Button btnRefreshCodexModels;
        private Label lblCodexCatalogStatus;
        private TableLayoutPanel editorLayout;
        private GroupBox grpSql;
        private TextBox txtSql;
        private GroupBox grpBindings;
        private DataGridView gridBindings;
        private DataGridViewTextBoxColumn colBindName;
        private DataGridViewComboBoxColumn colBindType;
        private DataGridViewCheckBoxColumn colBindNull;
        private DataGridViewTextBoxColumn colBindValue;
        private DataGridViewTextBoxColumn colBindSize;
        private FlowLayoutPanel commandPanel;
        private Button btnApprove;
        private Button btnStartCodex;
        private Button btnCopySessionId;
        private Button btnConfigureMcp;
        private Button btnStop;
        private Label lblSavedSessions;
        private ComboBox cmbSavedSessions;
        private Label lblExplainWarning;
        private TableLayoutPanel resultLayout;
        private GroupBox grpHistory;
        private DataGridView gridHistory;
        private DataGridViewTextBoxColumn colRound;
        private DataGridViewTextBoxColumn colCandidate;
        private DataGridViewTextBoxColumn colIterationStatus;
        private DataGridViewTextBoxColumn colCost;
        private DataGridViewTextBoxColumn colElapsed;
        private DataGridViewTextBoxColumn colRows;
        private DataGridViewTextBoxColumn colPlanHash;
        private DataGridViewTextBoxColumn colUpdatedAt;
        private TabControl detailsTabs;
        private TabPage tabSqlDiff;
        private TextBox txtSqlDiff;
        private TabPage tabExplainPlan;
        private TextBox txtExplainPlan;
        private TabPage tabAiAnalysis;
        private TextBox txtAiAnalysis;
        private TabPage tabActualPlan;
        private TextBox txtActualPlan;
        private TabPage tabResultPreview;
        private TableLayoutPanel previewLayout;
        private Label lblResultNotice;
        private DataGridView gridResultPreview;
        private TabPage tabActivity;
        private TextBox txtActivity;
        private TableLayoutPanel feedbackLayout;
        private Label lblFeedback;
        private TextBox txtFeedback;
        private FlowLayoutPanel feedbackButtons;
        private Button btnAccept;
        private Button btnContinue;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel lblState;
        private ToolStripStatusLabel lblSessionId;
        private ToolStripStatusLabel lblMcpStatus;
        private ToolStripStatusLabel lblDetail;
    }
}
