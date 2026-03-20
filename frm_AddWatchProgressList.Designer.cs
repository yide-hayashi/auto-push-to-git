namespace Project_auto_push_to_gitlab
{
    partial class frm_AddWatchProgressList
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            Grid = new DataGridView();
            label1 = new Label();
            cbList = new ComboBox();
            btnAdd = new Button();
            refrash = new Button();
            ((System.ComponentModel.ISupportInitialize)Grid).BeginInit();
            SuspendLayout();
            // 
            // Grid
            // 
            Grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            Grid.Location = new Point(12, 64);
            Grid.Name = "Grid";
            Grid.RowTemplate.Height = 25;
            Grid.Size = new Size(415, 374);
            Grid.TabIndex = 0;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(12, 9);
            label1.Name = "label1";
            label1.Size = new Size(58, 15);
            label1.TabIndex = 1;
            label1.Text = "選擇程式:";
            // 
            // cbList
            // 
            cbList.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            cbList.AutoCompleteSource = AutoCompleteSource.ListItems;
            cbList.FormattingEnabled = true;
            cbList.Location = new Point(76, 6);
            cbList.Name = "cbList";
            cbList.Size = new Size(176, 23);
            cbList.TabIndex = 2;
            // 
            // btnAdd
            // 
            btnAdd.Location = new Point(347, 6);
            btnAdd.Name = "btnAdd";
            btnAdd.Size = new Size(75, 23);
            btnAdd.TabIndex = 3;
            btnAdd.Text = "新增";
            btnAdd.UseVisualStyleBackColor = true;
            btnAdd.Click += btnAdd_Click;
            // 
            // refrash
            // 
            refrash.Location = new Point(258, 6);
            refrash.Name = "refrash";
            refrash.Size = new Size(56, 23);
            refrash.TabIndex = 4;
            refrash.Text = "refrash";
            refrash.UseVisualStyleBackColor = true;
            refrash.Click += refrash_Click;
            // 
            // frm_AddWatchProgressList
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(434, 450);
            Controls.Add(refrash);
            Controls.Add(btnAdd);
            Controls.Add(cbList);
            Controls.Add(label1);
            Controls.Add(Grid);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Name = "frm_AddWatchProgressList";
            Text = "frm_AddWatchProgressList";
            Load += frm_AddWatchProgressList_Load;
            ((System.ComponentModel.ISupportInitialize)Grid).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private DataGridView Grid;
        private Label label1;
        private ComboBox cbList;
        private Button btnAdd;
        private Button refrash;
    }
}