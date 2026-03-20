using Project_auto_push_to_gitlab.cls;
using Project_auto_push_to_gitlab.extension;
using Project_auto_push_to_gitlab.model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static Project_auto_push_to_gitlab.frm_SessionGrid;

namespace Project_auto_push_to_gitlab
{
    public partial class frm_Sql_idLoggingGrid : Form
    {
        public frm_SessionGrid frm_Session;
        public frm_Sql_idLoggingGrid()
        {
            InitializeComponent();
        }

        private void frm_Sql_idLoggingGrid_Load(object sender, EventArgs e)
        {
            GridLogging.DataSource = LoggingData.cls_Loggings;
            GridLogging.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
     
        }

        private void GridLogging_DoubleClick(object sender, EventArgs e)
        {
            if (GridLogging.CurrentCell != null && GridLogging.CurrentRow.Cells["sql_id"].Value != null)
            {
                int col = GridLogging.CurrentCell.ColumnIndex;
                string title = GridLogging.Columns[col].HeaderText;
                GetDBsqlStautsData getDBsqlStautsData = new GetDBsqlStautsData();
                getDBsqlStautsData.hsparea = GridLogging.CurrentRow.Cells["hspArea"].Value.ToString();
                getDBsqlStautsData.sql_id = GridLogging.CurrentRow.Cells["sql_id"].Value.ToString();
                getDBsqlStautsData.client_info = "N/A";
                getDBsqlStautsData.RunningExeName = "手動查詢";
                frm_Session.GetDBsqlStauts(getDBsqlStautsData);
                // MessageBox.Show("列タイトル = " + title);
            }
        }
    }
}
