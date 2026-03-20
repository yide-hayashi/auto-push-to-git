namespace Project_auto_push_to_gitlab
{
    partial class frm_SessionGrid
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frm_SessionGrid));
            notifyIcon = new NotifyIcon(components);
            contextMenuStrip = new ContextMenuStrip(components);
            closeToolStripMenuItem = new ToolStripMenuItem();
            GridforKTGH = new DataGridView();
            WatchModel = new CheckBox();
            txtWatchTime = new TextBox();
            label1 = new Label();
            statusStrip1 = new StatusStrip();
            toolStripSplitButton1 = new ToolStripSplitButton();
            sqlidLogginToolStripMenuItem = new ToolStripMenuItem();
            lblmsg = new ToolStripStatusLabel();
            Gridsql_stauts = new DataGridView();
            txtsql_id = new TextBox();
            label2 = new Label();
            Getsql_id = new Button();
            tabControl1 = new TabControl();
            tabPage1 = new TabPage();
            tabPage2 = new TabPage();
            GridForHPK210 = new DataGridView();
            tabPage3 = new TabPage();
            tableLayoutPanel1 = new TableLayoutPanel();
            watchingUseing = new CheckBox();
            GridKTGH_PGA = new DataGridView();
            GridHPK_PGA = new DataGridView();
            GridKTGH_sessionUsing = new DataGridView();
            GridHPK_sessionUsing = new DataGridView();
            cbHspArea = new ComboBox();
            addWatchingProgressToolStripMenuItem = new ToolStripMenuItem();
            contextMenuStrip.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)GridforKTGH).BeginInit();
            statusStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)Gridsql_stauts).BeginInit();
            tabControl1.SuspendLayout();
            tabPage1.SuspendLayout();
            tabPage2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)GridForHPK210).BeginInit();
            tabPage3.SuspendLayout();
            tableLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)GridKTGH_PGA).BeginInit();
            ((System.ComponentModel.ISupportInitialize)GridHPK_PGA).BeginInit();
            ((System.ComponentModel.ISupportInitialize)GridKTGH_sessionUsing).BeginInit();
            ((System.ComponentModel.ISupportInitialize)GridHPK_sessionUsing).BeginInit();
            SuspendLayout();
            // 
            // notifyIcon
            // 
            notifyIcon.ContextMenuStrip = contextMenuStrip;
            notifyIcon.Icon = (Icon)resources.GetObject("notifyIcon.Icon");
            notifyIcon.Text = "くそたれ...執行中...";
            notifyIcon.Visible = true;
            notifyIcon.DoubleClick += notifyIcon_DoubleClick;
            // 
            // contextMenuStrip
            // 
            contextMenuStrip.Items.AddRange(new ToolStripItem[] { closeToolStripMenuItem });
            contextMenuStrip.Name = "contextMenuStrip";
            contextMenuStrip.Size = new Size(106, 26);
            // 
            // closeToolStripMenuItem
            // 
            closeToolStripMenuItem.Name = "closeToolStripMenuItem";
            closeToolStripMenuItem.Size = new Size(105, 22);
            closeToolStripMenuItem.Text = "Close";
            closeToolStripMenuItem.Click += closeToolStripMenuItem_Click;
            // 
            // GridforKTGH
            // 
            GridforKTGH.AllowUserToAddRows = false;
            GridforKTGH.AllowUserToDeleteRows = false;
            GridforKTGH.AllowUserToOrderColumns = true;
            GridforKTGH.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            GridforKTGH.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            GridforKTGH.Location = new Point(0, 4);
            GridforKTGH.Name = "GridforKTGH";
            GridforKTGH.ReadOnly = true;
            GridforKTGH.RowTemplate.Height = 25;
            GridforKTGH.Size = new Size(1590, 425);
            GridforKTGH.TabIndex = 1;
            GridforKTGH.DoubleClick += Grid_DoubleClick;
            // 
            // WatchModel
            // 
            WatchModel.AutoSize = true;
            WatchModel.Location = new Point(12, 12);
            WatchModel.Name = "WatchModel";
            WatchModel.Size = new Size(170, 19);
            WatchModel.TabIndex = 2;
            WatchModel.Text = "監視模式(每1~3s更新一次)";
            WatchModel.UseVisualStyleBackColor = true;
            WatchModel.CheckedChanged += WatchModel_CheckedChanged;
            // 
            // txtWatchTime
            // 
            txtWatchTime.Location = new Point(188, 10);
            txtWatchTime.Name = "txtWatchTime";
            txtWatchTime.Size = new Size(49, 23);
            txtWatchTime.TabIndex = 3;
            txtWatchTime.Text = "2500";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(243, 13);
            label1.Name = "label1";
            label1.Size = new Size(31, 15);
            label1.TabIndex = 4;
            label1.Text = "(ms)";
            // 
            // statusStrip1
            // 
            statusStrip1.Items.AddRange(new ToolStripItem[] { toolStripSplitButton1, lblmsg });
            statusStrip1.Location = new Point(0, 623);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Size = new Size(1601, 22);
            statusStrip1.TabIndex = 5;
            statusStrip1.Text = "statusStrip1";
            // 
            // toolStripSplitButton1
            // 
            toolStripSplitButton1.DisplayStyle = ToolStripItemDisplayStyle.Image;
            toolStripSplitButton1.DropDownItems.AddRange(new ToolStripItem[] { sqlidLogginToolStripMenuItem, addWatchingProgressToolStripMenuItem });
            toolStripSplitButton1.Image = (Image)resources.GetObject("toolStripSplitButton1.Image");
            toolStripSplitButton1.ImageTransparentColor = Color.Magenta;
            toolStripSplitButton1.Name = "toolStripSplitButton1";
            toolStripSplitButton1.Size = new Size(32, 20);
            toolStripSplitButton1.Text = "toolStripSplitButton1";
            // 
            // sqlidLogginToolStripMenuItem
            // 
            sqlidLogginToolStripMenuItem.Name = "sqlidLogginToolStripMenuItem";
            sqlidLogginToolStripMenuItem.Size = new Size(206, 22);
            sqlidLogginToolStripMenuItem.Text = "Sql_id Loggin";
            sqlidLogginToolStripMenuItem.Click += sqlidLogginToolStripMenuItem_Click;
            // 
            // lblmsg
            // 
            lblmsg.Name = "lblmsg";
            lblmsg.Size = new Size(31, 17);
            lblmsg.Text = "就緒";
            // 
            // Gridsql_stauts
            // 
            Gridsql_stauts.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Gridsql_stauts.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            Gridsql_stauts.Location = new Point(0, 39);
            Gridsql_stauts.Name = "Gridsql_stauts";
            Gridsql_stauts.RowTemplate.Height = 25;
            Gridsql_stauts.Size = new Size(1601, 117);
            Gridsql_stauts.TabIndex = 6;
            Gridsql_stauts.DoubleClick += Gridsql_stauts_DoubleClick;
            // 
            // txtsql_id
            // 
            txtsql_id.Location = new Point(337, 10);
            txtsql_id.Name = "txtsql_id";
            txtsql_id.Size = new Size(180, 23);
            txtsql_id.TabIndex = 7;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(289, 16);
            label2.Name = "label2";
            label2.Size = new Size(39, 15);
            label2.TabIndex = 8;
            label2.Text = "sql_id";
            // 
            // Getsql_id
            // 
            Getsql_id.Location = new Point(650, 10);
            Getsql_id.Name = "Getsql_id";
            Getsql_id.Size = new Size(76, 23);
            Getsql_id.TabIndex = 9;
            Getsql_id.Text = "取得";
            Getsql_id.UseVisualStyleBackColor = true;
            Getsql_id.Click += Getsql_id_Click;
            // 
            // tabControl1
            // 
            tabControl1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tabControl1.Controls.Add(tabPage1);
            tabControl1.Controls.Add(tabPage2);
            tabControl1.Controls.Add(tabPage3);
            tabControl1.Location = new Point(0, 160);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(1601, 460);
            tabControl1.TabIndex = 10;
            // 
            // tabPage1
            // 
            tabPage1.Controls.Add(GridforKTGH);
            tabPage1.Location = new Point(4, 24);
            tabPage1.Name = "tabPage1";
            tabPage1.Padding = new Padding(3);
            tabPage1.Size = new Size(1593, 432);
            tabPage1.TabIndex = 0;
            tabPage1.Text = "KTGH00_session";
            tabPage1.UseVisualStyleBackColor = true;
            // 
            // tabPage2
            // 
            tabPage2.Controls.Add(GridForHPK210);
            tabPage2.Location = new Point(4, 24);
            tabPage2.Name = "tabPage2";
            tabPage2.Padding = new Padding(3);
            tabPage2.Size = new Size(1593, 432);
            tabPage2.TabIndex = 1;
            tabPage2.Text = "HPK210_session";
            tabPage2.UseVisualStyleBackColor = true;
            // 
            // GridForHPK210
            // 
            GridForHPK210.AllowUserToAddRows = false;
            GridForHPK210.AllowUserToDeleteRows = false;
            GridForHPK210.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            GridForHPK210.Dock = DockStyle.Fill;
            GridForHPK210.Location = new Point(3, 3);
            GridForHPK210.Name = "GridForHPK210";
            GridForHPK210.ReadOnly = true;
            GridForHPK210.RowTemplate.Height = 25;
            GridForHPK210.Size = new Size(1587, 426);
            GridForHPK210.TabIndex = 0;
            GridForHPK210.DoubleClick += GridForHPK210_DoubleClick;
            // 
            // tabPage3
            // 
            tabPage3.Controls.Add(tableLayoutPanel1);
            tabPage3.Location = new Point(4, 24);
            tabPage3.Name = "tabPage3";
            tabPage3.Size = new Size(1593, 432);
            tabPage3.TabIndex = 2;
            tabPage3.Text = "PGA";
            tabPage3.UseVisualStyleBackColor = true;
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tableLayoutPanel1.ColumnCount = 2;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 794F));
            tableLayoutPanel1.Controls.Add(watchingUseing, 0, 0);
            tableLayoutPanel1.Controls.Add(GridKTGH_PGA, 0, 1);
            tableLayoutPanel1.Controls.Add(GridHPK_PGA, 1, 1);
            tableLayoutPanel1.Controls.Add(GridKTGH_sessionUsing, 0, 2);
            tableLayoutPanel1.Controls.Add(GridHPK_sessionUsing, 1, 2);
            tableLayoutPanel1.Location = new Point(3, 3);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 3;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 210F));
            tableLayoutPanel1.Size = new Size(1587, 426);
            tableLayoutPanel1.TabIndex = 0;
            // 
            // watchingUseing
            // 
            watchingUseing.AutoSize = true;
            watchingUseing.Location = new Point(3, 3);
            watchingUseing.Name = "watchingUseing";
            watchingUseing.Size = new Size(160, 19);
            watchingUseing.TabIndex = 12;
            watchingUseing.Text = "PAG/Session 使用量監視";
            watchingUseing.UseVisualStyleBackColor = true;
            watchingUseing.CheckedChanged += watchingUseing_CheckedChanged;
            // 
            // GridKTGH_PGA
            // 
            GridKTGH_PGA.AllowUserToDeleteRows = false;
            GridKTGH_PGA.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            GridKTGH_PGA.Dock = DockStyle.Fill;
            GridKTGH_PGA.Location = new Point(3, 29);
            GridKTGH_PGA.Name = "GridKTGH_PGA";
            GridKTGH_PGA.RowTemplate.Height = 25;
            GridKTGH_PGA.Size = new Size(787, 184);
            GridKTGH_PGA.TabIndex = 0;
            // 
            // GridHPK_PGA
            // 
            GridHPK_PGA.AllowUserToDeleteRows = false;
            GridHPK_PGA.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            GridHPK_PGA.Dock = DockStyle.Fill;
            GridHPK_PGA.Location = new Point(796, 29);
            GridHPK_PGA.Name = "GridHPK_PGA";
            GridHPK_PGA.RowTemplate.Height = 25;
            GridHPK_PGA.Size = new Size(788, 184);
            GridHPK_PGA.TabIndex = 1;
            // 
            // GridKTGH_sessionUsing
            // 
            GridKTGH_sessionUsing.AllowUserToDeleteRows = false;
            GridKTGH_sessionUsing.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            GridKTGH_sessionUsing.Dock = DockStyle.Fill;
            GridKTGH_sessionUsing.Location = new Point(3, 219);
            GridKTGH_sessionUsing.Name = "GridKTGH_sessionUsing";
            GridKTGH_sessionUsing.RowTemplate.Height = 25;
            GridKTGH_sessionUsing.Size = new Size(787, 204);
            GridKTGH_sessionUsing.TabIndex = 13;
            // 
            // GridHPK_sessionUsing
            // 
            GridHPK_sessionUsing.AllowUserToDeleteRows = false;
            GridHPK_sessionUsing.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            GridHPK_sessionUsing.Location = new Point(796, 219);
            GridHPK_sessionUsing.Name = "GridHPK_sessionUsing";
            GridHPK_sessionUsing.RowTemplate.Height = 25;
            GridHPK_sessionUsing.Size = new Size(787, 204);
            GridHPK_sessionUsing.TabIndex = 14;
            // 
            // cbHspArea
            // 
            cbHspArea.FormattingEnabled = true;
            cbHspArea.Items.AddRange(new object[] { "KTGH00", "HPK210" });
            cbHspArea.Location = new Point(523, 10);
            cbHspArea.Name = "cbHspArea";
            cbHspArea.Size = new Size(121, 23);
            cbHspArea.TabIndex = 11;
            cbHspArea.Text = "KTGH00";
            // 
            // addWatchingProgressToolStripMenuItem
            // 
            addWatchingProgressToolStripMenuItem.Name = "addWatchingProgressToolStripMenuItem";
            addWatchingProgressToolStripMenuItem.Size = new Size(206, 22);
            addWatchingProgressToolStripMenuItem.Text = "Add Watching Progress";
            addWatchingProgressToolStripMenuItem.Click += addWatchingProgressToolStripMenuItem_Click;
            // 
            // frm_SessionGrid
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1601, 645);
            Controls.Add(cbHspArea);
            Controls.Add(tabControl1);
            Controls.Add(Getsql_id);
            Controls.Add(label2);
            Controls.Add(txtsql_id);
            Controls.Add(Gridsql_stauts);
            Controls.Add(statusStrip1);
            Controls.Add(label1);
            Controls.Add(txtWatchTime);
            Controls.Add(WatchModel);
            Name = "frm_SessionGrid";
            Text = "AutoCallGit";
            FormClosing += Form1_FormClosing;
            FormClosed += Form1_FormClosed;
            Load += Form1_Load;
            contextMenuStrip.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)GridforKTGH).EndInit();
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)Gridsql_stauts).EndInit();
            tabControl1.ResumeLayout(false);
            tabPage1.ResumeLayout(false);
            tabPage2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)GridForHPK210).EndInit();
            tabPage3.ResumeLayout(false);
            tableLayoutPanel1.ResumeLayout(false);
            tableLayoutPanel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)GridKTGH_PGA).EndInit();
            ((System.ComponentModel.ISupportInitialize)GridHPK_PGA).EndInit();
            ((System.ComponentModel.ISupportInitialize)GridKTGH_sessionUsing).EndInit();
            ((System.ComponentModel.ISupportInitialize)GridHPK_sessionUsing).EndInit();
            ResumeLayout(false);
            PerformLayout();

        }

        #endregion

        private NotifyIcon notifyIcon;
        private ContextMenuStrip contextMenuStrip;
        private ToolStripMenuItem closeToolStripMenuItem;
        private DataGridView GridforKTGH;
        private CheckBox WatchModel;
        private TextBox txtWatchTime;
        private Label label1;
        private StatusStrip statusStrip1;
        private ToolStripStatusLabel lblmsg;
        private DataGridView Gridsql_stauts;
        private ToolStripSplitButton toolStripSplitButton1;
        private ToolStripMenuItem sqlidLogginToolStripMenuItem;
        private TextBox txtsql_id;
        private Label label2;
        private Button Getsql_id;
        private TabControl tabControl1;
        private TabPage tabPage1;
        private TabPage tabPage2;
        private DataGridView GridForHPK210;
        private ComboBox cbHspArea;
        private TabPage tabPage3;
        private TableLayoutPanel tableLayoutPanel1;
        private DataGridView GridKTGH_PGA;
        private DataGridView GridHPK_PGA;
        private CheckBox watchingUseing;
        private DataGridView GridKTGH_sessionUsing;
        private DataGridView GridHPK_sessionUsing;
        private ToolStripMenuItem addWatchingProgressToolStripMenuItem;
    }
}