namespace Project_auto_push_to_gitlab
{
    partial class frm_Sql_idLoggingGrid
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
            this.GridLogging = new System.Windows.Forms.DataGridView();
            ((System.ComponentModel.ISupportInitialize)(this.GridLogging)).BeginInit();
            this.SuspendLayout();
            // 
            // GridLogging
            // 
            this.GridLogging.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.GridLogging.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.GridLogging.Location = new System.Drawing.Point(0, 2);
            this.GridLogging.Name = "GridLogging";
            this.GridLogging.RowTemplate.Height = 25;
            this.GridLogging.Size = new System.Drawing.Size(799, 450);
            this.GridLogging.TabIndex = 0;
            this.GridLogging.DoubleClick += new System.EventHandler(this.GridLogging_DoubleClick);
            // 
            // frm_Sql_idLoggingGrid
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.GridLogging);
            this.Name = "frm_Sql_idLoggingGrid";
            this.Text = "frm_Sql_idLoggingGrid";
            this.Load += new System.EventHandler(this.frm_Sql_idLoggingGrid_Load);
            ((System.ComponentModel.ISupportInitialize)(this.GridLogging)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private DataGridView GridLogging;
    }
}