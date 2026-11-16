namespace ZSheet;

/// 서버 관리 창의 상품 도구: 단위 우선순위, 중복 바코드 지정
static class ProductForms
{
    public static readonly string[] ProductHeads = { "바코드", "상품명", "단위", "입고단가", "판매가1", "판매가2", "판매가3", "판매가4", "판매가5", "판매가6", "판매가7", "판매가8", "판매가9", "판매가10" };

    static Form Dlg(string title, int w, int h) => new Form { Text = title, Width = w, Height = h, StartPosition = FormStartPosition.CenterParent, ShowInTaskbar = false, MinimizeBox = false, Font = new Font("Malgun Gothic", 9.5f), BackColor = Color.White };

    /// 같은 바코드가 여러 단위로 있을 때 어느 단위를 쓸지 순서 정하기 (위에 있을수록 우선)
    public static void UnitOrder(IWin32Window owner, Db db, long sid)
    {
        var (_, uk, _) = Product.Cols(db, sid);
        if (uk == null) { MessageBox.Show("이 시트에는 '단위' 열이 없습니다."); return; }
        var units = Product.Units(db, sid);
        var list = Product.UnitOrder(db, sid).ToList();
        foreach (var u in units) if (!list.Contains(u, StringComparer.OrdinalIgnoreCase)) list.Add(u);
        using var f = Dlg("단위 우선순위", 480, 440); f.FormBorderStyle = FormBorderStyle.FixedDialog; f.MaximizeBox = false;
        f.Controls.Add(new Label { Left = 14, Top = 12, Width = 440, Height = 60, Text = "같은 바코드가 여러 단위(개, 갑, 박스…)로 등록되어 있을 때, 맨 위 단위의 행만 바코드 스캔 결과로 쓰입니다.\n▲▼ 로 순서를 바꾼 뒤 [저장]을 누르세요." });
        var lb = new ListBox { Left = 14, Top = 80, Width = 300, Height = 260, Font = new Font("Malgun Gothic", 11f) };
        foreach (var u in list) lb.Items.Add(u);
        if (lb.Items.Count > 0) lb.SelectedIndex = 0;
        f.Controls.Add(lb);
        void Move(int d)
        {
            int i = lb.SelectedIndex, j = i + d; if (i < 0 || j < 0 || j >= lb.Items.Count) return;
            var x = lb.Items[i]; lb.Items[i] = lb.Items[j]; lb.Items[j] = x; lb.SelectedIndex = j;
        }
        var up = Ui.Btn("▲ 위로", (_, _) => Move(-1)); up.Left = 326; up.Top = 80; f.Controls.Add(up);
        var dn = Ui.Btn("▼ 아래로", (_, _) => Move(1)); dn.Left = 326; dn.Top = 120; f.Controls.Add(dn);
        var save = Ui.Btn("저장", (_, _) =>
        {
            Product.SetUnitOrder(db, sid, lb.Items.Cast<object>().Select(x => x.ToString()).ToList(), Store.Admin);
            MessageBox.Show("저장했습니다.\n우선순위: " + string.Join(" > ", lb.Items.Cast<object>()), "단위 우선순위"); f.Close();
        }, 1);
        save.Left = 326; save.Top = 300; f.Controls.Add(save);
        var close = Ui.Btn("닫기", (_, _) => f.Close()); close.Left = 326; close.Top = 340; f.Controls.Add(close);
        f.ShowDialog(owner);
    }

    static string StateText(string st) => st switch { "pick" => "관리자 지정", "unit" => "단위 우선순위로 결정", "conflict" => "충돌 — 지정 필요", _ => st };

    /// 같은 바코드가 여러 줄인 것을 보고, 사용할 행(단위+상품명)을 관리자가 지정
    public static void Dups(IWin32Window owner, Db db, long sid)
    {
        var (ck, uk, nk) = Product.Cols(db, sid);
        if (ck == null) { MessageBox.Show("이 시트에는 바코드 열이 없습니다."); return; }
        var cols = Store.Columns(db, sid).Where(x => x.Key != ck).ToList();
        using var f = Dlg("중복 바코드 지정", 1180, 660); f.MinimumSize = new Size(900, 480);

        var chk = new CheckBox { Text = "충돌(지정 필요)만 보기", Checked = true, AutoSize = true, Margin = new Padding(8, 9, 8, 0) };
        var gL = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, MultiSelect = false }; Ui.StyleGrid(gL); gL.SelectionMode = DataGridViewSelectionMode.FullRowSelect; gL.RowHeadersVisible = false;
        gL.Columns.Add("code", "바코드"); gL.Columns.Add("state", "상태"); gL.Columns.Add("n", "후보");
        gL.Columns[0].Width = 150; gL.Columns[1].Width = 150; gL.Columns[2].Width = 50;
        var left = new Panel { Dock = DockStyle.Left, Width = 380 };
        var topL = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, BackColor = Ui.BlueSoft }; topL.Controls.Add(chk);
        left.Controls.Add(gL); left.Controls.Add(topL);

        var gR = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, MultiSelect = false }; Ui.StyleGrid(gR); gR.SelectionMode = DataGridViewSelectionMode.FullRowSelect; gR.RowHeadersVisible = false;
        gR.Columns.Add("res", "스캔 결과");
        foreach (var c in cols) gR.Columns.Add(c.Key, c.Name);
        gR.Columns[0].Width = 90;
        var lbl = new Label { Dock = DockStyle.Top, Height = 46, Padding = new Padding(8, 6, 8, 0), Text = "왼쪽에서 바코드를 고르면 후보가 보입니다. 사용할 행을 선택하고 [이 행으로 지정]을 누르면, 그 바코드는 그 행의 상품으로만 스캔됩니다." };
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(6), BackColor = Ui.BlueSoft };
        var right = new Panel { Dock = DockStyle.Fill };
        right.Controls.Add(gR); right.Controls.Add(lbl); right.Controls.Add(bar);

        List<DupInfo> dups = new(); DupInfo cur = null; bool filling = false;
        void ShowCands()
        {
            gR.Rows.Clear(); cur = null;
            if (gL.CurrentRow == null || gL.CurrentRow.Tag is not DupInfo di) return;
            cur = di;
            foreach (var cd in di.Cands)
            {
                var vals = new object[cols.Count + 1];
                vals[0] = di.Resolved != null && di.Resolved.Id == cd.Id ? "✔ 사용" : "";
                for (int j = 0; j < cols.Count; j++) vals[j + 1] = cd.Data[cols[j].Key]?.ToString() ?? "";
                int ri = gR.Rows.Add(vals); gR.Rows[ri].Tag = cd;
                if (vals[0].ToString() != "") gR.Rows[ri].DefaultCellStyle.BackColor = Color.FromArgb(223, 244, 232);
            }
        }
        void Fill(string keep)
        {
            filling = true;
            try
            {
                dups = Product.Dups(db, sid); gL.Rows.Clear();
                foreach (var d in dups)
                {
                    if (chk.Checked && d.State != "conflict") continue;
                    int i = gL.Rows.Add(d.Code, StateText(d.State), d.Cands.Count); gL.Rows[i].Tag = d;
                    if (d.State == "conflict") gL.Rows[i].DefaultCellStyle.ForeColor = Ui.Danger;
                    if (keep != null && string.Equals(d.Code, keep, StringComparison.OrdinalIgnoreCase)) gL.CurrentCell = gL.Rows[i].Cells[0];
                }
                f.Text = "중복 바코드 지정 — 충돌 " + dups.Count(x => x.State == "conflict") + "건 / 중복 전체 " + dups.Count + "건";
            }
            finally { filling = false; }
            ShowCands();
        }
        gL.SelectionChanged += (_, _) => { if (!filling) ShowCands(); };
        chk.CheckedChanged += (_, _) => Fill(null);

        bar.Controls.Add(Ui.Btn("이 행으로 지정", (_, _) =>
        {
            if (cur == null || gR.CurrentRow?.Tag is not Cand cd) { MessageBox.Show("지정할 행을 선택하세요."); return; }
            Product.SetPick(db, sid, cur.Code, cd.Unit, cd.Name, Store.Admin); Fill(cur.Code);
        }, 1));
        bar.Controls.Add(Ui.Btn("지정 해제", (_, _) => { if (cur == null) return; Product.ClearPick(db, sid, cur.Code, Store.Admin); Fill(cur.Code); }));
        bar.Controls.Add(Ui.Btn("닫기", (_, _) => f.Close()));
        bar.Controls.Add(new Label { Text = "※ 지정해도 다른 행은 지워지지 않습니다. 엑셀 동기화를 해도 지정은 유지됩니다.", AutoSize = true, ForeColor = Color.Gray, Margin = new Padding(12, 9, 0, 0) });

        f.Controls.Add(right); f.Controls.Add(left);   // 가운데(Fill) 먼저
        f.Load += (_, _) => Fill(null);
        f.ShowDialog(owner);
    }
}
