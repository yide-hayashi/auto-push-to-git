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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frm_SessionGrid));
            this.notifyIcon = new System.Windows.Forms.NotifyIcon(this.components);
            this.contextMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.closeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.GridforKTGH = new System.Windows.Forms.DataGridView();
            this.WatchModel = new System.Windows.Forms.CheckBox();
            this.txtWatchTime = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.toolStripSplitButton1 = new System.Windows.Forms.ToolStripSplitButton();
            this.sqlidLogginToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.lblmsg = new System.Windows.Forms.ToolStripStatusLabel();
            this.Gridsql_stauts = new System.Windows.Forms.DataGridView();
            this.txtsql_id = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.Getsql_id = new System.Windows.Forms.Button();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.GridForHPK210 = new System.Windows.Forms.DataGridView();
            this.tabPage3 = new System.Windows.Forms.TabPage();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.watchingUseing = new System.Windows.Forms.CheckBox();
            this.GridKTGH_PGA = new System.Windows.Forms.DataGridView();
            this.GridHPK_PGA = new System.Windows.Forms.DataGridView();
            this.cbHspArea = new System.Windows.Forms.ComboBox();
            this.GridKTGH_sessionUsing = new System.Windows.Forms.DataGridView();
            this.GridHPK_sessionUsing = new System.Windows.Forms.DataGridView();
            this.contextMenuStrip.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.GridforKTGH)).BeginInit();
            this.statusStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.Gridsql_stauts)).BeginInit();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.GridForHPK210)).BeginInit();
            this.tabPage3.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.GridKTGH_PGA)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.GridHPK_PGA)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.GridKTGH_sessionUsing)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.GridHPK_sessionUsing)).BeginInit();
            this.SuspendLayout();
            // 
            // notifyIcon
            // 
            this.notifyIcon.ContextMenuStrip = this.contextMenuStrip;
            this.notifyIcon.Icon = ((System.Drawing.Icon)(resources.GetObject("notifyIcon.Icon")));
            this.notifyIcon.Text = "くそたれ...執行中...";
            this.notifyIcon.Visible = true;
            this.notifyIcon.DoubleClick += new System.EventHandler(this.notifyIcon_DoubleClick);
            // 
            // contextMenuStrip
            // 
            this.contextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.closeToolStripMenuItem});
            this.contextMenuStrip.Name = "contextMenuStrip";
            this.contextMenuStrip.Size = new System.Drawing.Size(106, 26);
            // 
            // closeToolStripMenuItem
            // 
            this.closeToolStripMenuItem.Name = "closeToolStripMenuItem";
            this.closeToolStripMenuItem.Size = new System.Drawing.Size(105, 22);
            this.closeToolStripMenuItem.Text = "Close";
            this.closeToolStripMenuItem.Click += new System.EventHandler(this.closeToolStripMenuItem_Click);
            // 
            // GridforKTGH
            // 
            this.GridforKTGH.AllowUserToAddRows = false;
            this.GridforKTGH.AllowUserToDeleteRows = false;
            this.GridforKTGH.AllowUserToOrderColumns = true;
            this.GridforKTGH.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.GridforKTGH.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.GridforKTGH.Location = new System.Drawing.Point(0, 4);
            this.GridforKTGH.Name = "GridforKTGH";
            this.GridforKTGH.ReadOnly = true;
            this.GridforKTGH.RowTemplate.Height = 25;
            this.GridforKTGH.Size = new System.Drawing.Size(1590, 425);
            this.GridforKTGH.TabIndex = 1;
            this.GridforKTGH.DoubleClick += new System.EventHandler(this.Grid_DoubleClick);
            // 
            // WatchModel
            // 
            this.WatchModel.AutoSize = true;
            this.WatchModel.Location = new System.Drawing.Point(12, 12);
            this.WatchModel.Name = "WatchModel";
            this.WatchModel.Size = new System.Drawing.Size(170, 19);
            this.WatchModel.TabIndex = 2;
            this.WatchModel.Text = "監視模式(每1~3s更新一次)";
            this.WatchModel.UseVisualStyleBackColor = true;
            this.WatchModel.CheckedChanged += new System.EventHandler(this.WatchModel_CheckedChanged);
            // 
            // txtWatchTime
            // 
            this.txtWatchTime.Location = new System.Drawing.Point(188, 10);
            this.txtWatchTime.Name = "txtWatchTime";
            this.txtWatchTime.Size = new System.Drawing.Size(49, 23);
            this.txtWatchTime.TabIndex = 3;
            this.txtWatchTime.Text = "2500";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(243, 13);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(31, 15);
            this.label1.TabIndex = 4;
            this.label1.Text = "(ms)";
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripSplitButton1,
            this.lblmsg});
            this.statusStrip1.Location = new System.Drawing.Point(0, 623);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(1601, 22);
            this.statusStrip1.TabIndex = 5;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // toolStripSplitButton1
            // 
            this.toolStripSplitButton1.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.toolStripSplitButton1.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.sqlidLogginToolStripMenuItem});
            this.toolStripSplitButton1.Image = ((System.Drawing.Image)(resources.GetObject("toolStripSplitButton1.Image")));
            this.toolStripSplitButton1.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripSplitButton1.Name = "toolStripSplitButton1";
            this.toolStripSplitButton1.Size = new System.Drawing.Size(32, 20);
            this.toolStripSplitButton1.Text = "toolStripSplitButton1";
            // 
            // sqlidLogginToolStripMenuItem
            // 
            this.sqlidLogginToolStripMenuItem.Name = "sqlidLogginToolStripMenuItem";
            this.sqlidLogginToolStripMenuItem.Size = new System.Drawing.Size(151, 22);
            this.sqlidLogginToolStripMenuItem.Text = "Sql_id Loggin";
            this.sqlidLogginToolStripMenuItem.Click += new System.EventHandler(this.sqlidLogginToolStripMenuItem_Click);
            // 
            // lblmsg
            // 
            this.lblmsg.Name = "lblmsg";
            this.lblmsg.Size = new System.Drawing.Size(31, 17);
            this.lblmsg.Text = "就緒";
            // 
            // Gridsql_stauts
            // 
            this.Gridsql_stauts.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.Gridsql_stauts.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.Gridsql_stauts.Location = new System.Drawing.Point(0, 39);
            this.Gridsql_stauts.Name = "Gridsql_stauts";
            this.Gridsql_stauts.RowTemplate.Height = 25;
            this.Gridsql_stauts.Size = new System.Drawing.Size(1601, 117);
            this.Gridsql_stauts.TabIndex = 6;
            this.Gridsql_stauts.DoubleClick += new System.EventHandler(this.Gridsql_stauts_DoubleClick);
            // 
            // txtsql_id
            // 
            this.txtsql_id.Location = new System.Drawing.Point(337, 10);
            this.txtsql_id.Name = "txtsql_id";
            this.txtsql_id.Size = new System.Drawing.Size(180, 23);
            this.txtsql_id.TabIndex = 7;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(289, 16);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(39, 15);
            this.label2.TabIndex = 8;
            this.label2.Text = "sql_id";
            // 
            // Getsql_id
            // 
            this.Getsql_id.Location = new System.Drawing.Point(650, 10);
            this.Getsql_id.Name = "Getsql_id";
            this.Getsql_id.Size = new System.Drawing.Size(76, 23);
            this.Getsql_id.TabIndex = 9;
            this.Getsql_id.Text = "取得";
            this.Getsql_id.UseVisualStyleBackColor = true;
            this.Getsql_id.Click += new System.EventHandler(this.Getsql_id_Click);
            // 
            // tabControl1
            // 
            this.tabControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Controls.Add(this.tabPage3);
            this.tabControl1.Location = new System.Drawing.Point(0, 160);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(1601, 460);
            this.tabControl1.TabIndex = 10;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.GridforKTGH);
            this.tabPage1.Location = new System.Drawing.Point(4, 24);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(1593, 432);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "KTGH00_session";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.GridForHPK210);
            this.tabPage2.Location = new System.Drawing.Point(4, 24);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(1593, 432);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "HPK210_session";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // GridForHPK210
            // 
            this.GridForHPK210.AllowUserToAddRows = false;
            this.GridForHPK210.AllowUserToDeleteRows = false;
            this.GridForHPK210.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.GridForHPK210.Dock = System.Windows.Forms.DockStyle.Fill;
            this.GridForHPK210.Location = new System.Drawing.Point(3, 3);
            this.GridForHPK210.Name = "GridForHPK210";
            this.GridForHPK210.ReadOnly = true;
            this.GridForHPK210.RowTemplate.Height = 25;
            this.GridForHPK210.Size = new System.Drawing.Size(1587, 426);
            this.GridForHPK210.TabIndex = 0;
            this.GridForHPK210.DoubleClick += new System.EventHandler(this.GridForHPK210_DoubleClick);
            // 
            // tabPage3
            // 
            this.tabPage3.Controls.Add(this.tableLayoutPanel1);
            this.tabPage3.Location = new System.Drawing.Point(4, 24);
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.Size = new System.Drawing.Size(1593, 432);
            this.tabPage3.TabIndex = 2;
            this.tabPage3.Text = "PGA";
            this.tabPage3.UseVisualStyleBackColor = true;
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 794F));
            this.tableLayoutPanel1.Controls.Add(this.watchingUseing, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.GridKTGH_PGA, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.GridHPK_PGA, 1, 1);
            this.tableLayoutPanel1.Controls.Add(this.GridKTGH_sessionUsing, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.GridHPK_sessionUsing, 1, 2);
            this.tableLayoutPanel1.Location = new System.Drawing.Point(3, 3);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 3;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 210F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(1587, 426);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // watchingUseing
            // 
            this.watchingUseing.AutoSize = true;
            this.watchingUseing.Location = new System.Drawing.Point(3, 3);
            this.watchingUseing.Name = "watchingUseing";
            this.watchingUseing.Size = new System.Drawing.Size(160, 19);
            this.watchingUseing.TabIndex = 12;
            this.watchingUseing.Text = "PAG/Session 使用量監視";
            this.watchingUseing.UseVisualStyleBackColor = true;
            this.watchingUseing.CheckedChanged += new System.EventHandler(this.watchingUseing_CheckedChanged);
            // 
            // GridKTGH_PGA
            // 
            this.GridKTGH_PGA.AllowUserToDeleteRows = false;
            this.GridKTGH_PGA.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.GridKTGH_PGA.Dock = System.Windows.Forms.DockStyle.Fill;
            this.GridKTGH_PGA.Location = new System.Drawing.Point(3, 29);
            this.GridKTGH_PGA.Name = "GridKTGH_PGA";
            this.GridKTGH_PGA.RowTemplate.Height = 25;
            this.GridKTGH_PGA.Size = new System.Drawing.Size(787, 184);
            this.GridKTGH_PGA.TabIndex = 0;
            // 
            // GridHPK_PGA
            // 
            this.GridHPK_PGA.AllowUserToDeleteRows = false;
            this.GridHPK_PGA.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.GridHPK_PGA.Dock = System.Windows.Forms.DockStyle.Fill;
            this.GridHPK_PGA.Location = new System.Drawing.Point(796, 29);
            this.GridHPK_PGA.Name = "GridHPK_PGA";
            this.GridHPK_PGA.RowTemplate.Height = 25;
            this.GridHPK_PGA.Size = new System.Drawing.Size(788, 184);
            this.GridHPK_PGA.TabIndex = 1;
            // 
            // cbHspArea
            // 
            this.cbHspArea.FormattingEnabled = true;
            this.cbHspArea.Items.AddRange(new object[] {
            "KTGH00",
            "HPK210"});
            this.cbHspArea.Location = new System.Drawing.Point(523, 10);
            this.cbHspArea.Name = "cbHspArea";
            this.cbHspArea.Size = new System.Drawing.Size(121, 23);
            this.cbHspArea.TabIndex = 11;
            this.cbHspArea.Text = "KTGH00";
            // 
            // GridKTGH_sessionUsing
            // 
            this.GridKTGH_sessionUsing.AllowUserToDeleteRows = false;
            this.GridKTGH_sessionUsing.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.GridKTGH_sessionUsing.Dock = System.Windows.Forms.DockStyle.Fill;
            this.GridKTGH_sessionUsing.Location = new System.Drawing.Point(3, 219);
            this.GridKTGH_sessionUsing.Name = "GridKTGH_sessionUsing";
            this.GridKTGH_sessionUsing.RowTemplate.Height = 25;
            this.GridKTGH_sessionUsing.Size = new System.Drawing.Size(787, 204);
            this.GridKTGH_sessionUsing.TabIndex = 13;
            // 
            // GridHPK_sessionUsing
            // 
            this.GridHPK_sessionUsing.AllowUserToDeleteRows = false;
            this.GridHPK_sessionUsing.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.GridHPK_sessionUsing.Location = new System.Drawing.Point(796, 219);
            this.GridHPK_sessionUsing.Name = "GridHPK_sessionUsing";
            this.GridHPK_sessionUsing.RowTemplate.Height = 25;
            this.GridHPK_sessionUsing.Size = new System.Drawing.Size(787, 204);
            this.GridHPK_sessionUsing.TabIndex = 14;
            // 
            // frm_SessionGrid
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1601, 645);
            this.Controls.Add(this.cbHspArea);
            this.Controls.Add(this.tabControl1);
            this.Controls.Add(this.Getsql_id);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.txtsql_id);
            this.Controls.Add(this.Gridsql_stauts);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.txtWatchTime);
            this.Controls.Add(this.WatchModel);
            this.Name = "frm_SessionGrid";
            this.Text = "AutoCallGit";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.Form1_FormClosed);
            this.Load += new System.EventHandler(this.Form1_Load);
            this.contextMenuStrip.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.GridforKTGH)).EndInit();
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.Gridsql_stauts)).EndInit();
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.GridForHPK210)).EndInit();
            this.tabPage3.ResumeLayout(false);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.GridKTGH_PGA)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.GridHPK_PGA)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.GridKTGH_sessionUsing)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.GridHPK_sessionUsing)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

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
    }
}